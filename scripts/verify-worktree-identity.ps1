#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Verifies the worktree-scoped app identity derivation and its no-op invariants.

.DESCRIPTION
    Worktree identity must never reach a release artifact and must be a no-op outside a
    linked git worktree. Both guarantees are easy to break by moving a Condition, so they
    are asserted here rather than trusted. See "Worktree builds" in docs/releasing.md.

    CI doesn't run it: tests/DailyPlants.Tests covers the same rules on a scratch project. Run it
    by hand after touching WorktreeIdentity.props/.targets, from the main checkout and from a
    linked worktree. From the main checkout the worktree-only assertions are skipped.

.EXAMPLE
    pwsh scripts/verify-worktree-identity.ps1
#>
[CmdletBinding()]
param(
    [string]$Project = 'src/DailyPlants/DailyPlants.csproj',
    [string]$TargetFramework = 'net11.0-desktop'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot

# MSBuild reads environment variables as properties, so a runner's ambient CI=true would switch the
# identity off for every assertion: the clone checks would pass vacuously and the worktree ones fail.
# The CI gate is still covered - I3 passes it explicitly with -p:.
$ambientCiVars = @{}
foreach ($name in 'CI', 'ContinuousIntegrationBuild') {
    $ambientCiVars[$name] = [Environment]::GetEnvironmentVariable($name)
    [Environment]::SetEnvironmentVariable($name, $null)
}

$script:failures = 0

function Get-Identity {
    param([string[]]$MSBuildArgs = @())

    $raw = & dotnet msbuild $Project "-p:TargetFramework=$TargetFramework" `
        '-getProperty:ApplicationId' '-getProperty:ApplicationTitle' @MSBuildArgs 2>&1
    $json = ($raw | Out-String)
    if ($LASTEXITCODE -ne 0) {
        throw "msbuild failed for args '$($MSBuildArgs -join ' ')':`n$json"
    }
    return ($json | ConvertFrom-Json).Properties
}

function Assert-Identity {
    param(
        [string]$Label,
        [string[]]$MSBuildArgs,
        [scriptblock]$Check
    )

    $id = Get-Identity -MSBuildArgs $MSBuildArgs
    if (& $Check $id) {
        Write-Host ('  PASS  {0}' -f $Label) -ForegroundColor Green
        Write-Host ('          {0}  |  {1}' -f $id.ApplicationId, $id.ApplicationTitle) -ForegroundColor DarkGray
    }
    else {
        Write-Host ('  FAIL  {0}' -f $Label) -ForegroundColor Red
        Write-Host ('          {0}  |  {1}' -f $id.ApplicationId, $id.ApplicationTitle) -ForegroundColor Red
        $script:failures++
    }
}

try {
    # Determine the context from the FILESYSTEM, never from the evaluated ApplicationId.
    # Deriving it from the suffix would be circular: a suffix that leaked into the main checkout
    # is exactly the I2 regression this script exists to catch, and it would flip the context to
    # "linked worktree" and skip the check that would have caught it.
    # A linked worktree has .git as a FILE whose gitdir sits under <common>/.git/worktrees/.
    $dotGit = Join-Path $repoRoot '.git'
    $inWorktree = $false
    if (Test-Path -Path $dotGit -PathType Leaf) {
        $gitdir = (Get-Content -Raw $dotGit).Trim()
        if ($gitdir -match '^gitdir:\s*(.+)$') {
            $resolved = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($repoRoot, $Matches[1].Trim()))
            $inWorktree = (Split-Path -Leaf (Split-Path -Parent $resolved)) -eq 'worktrees'
        }
    }

    Write-Host ''
    Write-Host 'Worktree-scoped app identity invariants' -ForegroundColor Cyan
    Write-Host ('  context: {0} (from .git on disk)' -f $(if ($inWorktree) { 'linked worktree' } else { 'main checkout' })) -ForegroundColor DarkGray
    Write-Host ''

    # I1 - a release build can never carry a worktree suffix, however it is invoked.
    Assert-Identity 'I1  Prod is never suffixed' `
        @('-p:AppChannel=Prod') `
        { param($i) $i.ApplicationId -eq 'dev.mzikmund.dailyplants' }

    Assert-Identity 'I1  Prod ignores an explicit -p:AppWorktreeName' `
        @('-p:AppChannel=Prod', '-p:AppWorktreeName=oops') `
        { param($i) $i.ApplicationId -eq 'dev.mzikmund.dailyplants' }

    # I3 - CI builds are byte-identical to today.
    Assert-Identity 'I3  CI=true is never suffixed' `
        @('-p:CI=true') `
        { param($i) $i.ApplicationId -eq 'dev.mzikmund.dailyplants.dev' }

    Assert-Identity 'I3  ContinuousIntegrationBuild=true is never suffixed' `
        @('-p:ContinuousIntegrationBuild=true') `
        { param($i) $i.ApplicationId -eq 'dev.mzikmund.dailyplants.dev' }

    # The documented escape hatch.
    Assert-Identity 'Kill switch EnableWorktreeIdentity=false' `
        @('-p:EnableWorktreeIdentity=false') `
        { param($i) $i.ApplicationId -eq 'dev.mzikmund.dailyplants.dev' }

    if ($inWorktree) {
        # I5 - MSIX Identity/Name is capped at 50 chars and must be letter-first lowercase.
        Assert-Identity 'I5  suffixed id is well-formed and <= 50 chars' `
            @() `
            { param($i)
                $i.ApplicationId.Length -le 50 -and
                $i.ApplicationId -cmatch '^[a-z][a-z0-9.]*$' -and
                $i.ApplicationId -match '\.wt[a-z0-9]{14}$' }

        # I6 - MSIX uap:DefaultTile/@ShortName is capped at 40 chars.
        Assert-Identity 'I6  suffixed title is <= 40 chars' `
            @() `
            { param($i) $i.ApplicationTitle.Length -le 40 }
    }
    else {
        # I2 - the main checkout must be byte-identical to a build without this feature.
        # This is the assertion the old suffix-derived context could silently skip.
        Assert-Identity 'I2  main checkout is never suffixed' `
            @() `
            { param($i)
                $i.ApplicationId -eq 'dev.mzikmund.dailyplants.dev' -and
                $i.ApplicationTitle -eq 'Daily Plants Dev' }
    }

    Write-Host ''
    if ($script:failures -gt 0) {
        Write-Host ('{0} invariant(s) FAILED' -f $script:failures) -ForegroundColor Red
        exit 1
    }

    Write-Host 'All worktree identity invariants hold.' -ForegroundColor Green
}
finally {
    foreach ($name in $ambientCiVars.Keys) {
        [Environment]::SetEnvironmentVariable($name, $ambientCiVars[$name])
    }
    Pop-Location
}
