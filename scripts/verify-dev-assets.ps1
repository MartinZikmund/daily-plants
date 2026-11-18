#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Verifies the build-time DEV badge on the app icon and splash screen.

.DESCRIPTION
    Builds the app head as Dev, as Dev with GenerateDevAssets=false, and as Prod, and compares the PNGs
    Uno.Resizetizer renders. See "Dev artwork" in docs/releasing.md.

    CI doesn't run it. Run it by hand after touching DevAssets.targets or DevAssets.Task.cs.
    -IncludeAndroid adds D5 and needs the Android workload.

.EXAMPLE
    pwsh scripts/verify-dev-assets.ps1 -IncludeAndroid
#>
[CmdletBinding()]
param(
    [string]$Project = 'src/DailyPlants/DailyPlants.csproj',
    [string]$TargetFramework = 'net11.0-desktop',
    [switch]$IncludeAndroid
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
Add-Type -AssemblyName System.Drawing

# MSBuild reads environment variables as properties, so a runner's ambient CI=true would give every build the CI badge.
# D7 passes it explicitly instead.
foreach ($name in 'CI', 'ContinuousIntegrationBuild') {
    [Environment]::SetEnvironmentVariable($name, $null)
}

$script:failures = 0
$projectDir = Split-Path -Parent (Join-Path $repoRoot $Project)
$snapshots = Join-Path ([IO.Path]::GetTempPath()) "verify-dev-assets-$PID"
$devColor = 'FFB900'
$ciColor = '0078D4'

function Get-ObjDir([string]$Tfm) {
    Join-Path $projectDir "obj/Debug/$Tfm"
}

function Invoke-Build {
    param([string]$Tfm, [string[]]$MSBuildArgs)

    $output = & dotnet build $Project -f $Tfm -c Debug -nologo @MSBuildArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed for $Tfm $($MSBuildArgs -join ' '):`n$($output | Out-String)"
    }
}

# Builds, then copies the named Resizetizer outputs aside so the next build can't overwrite them.
function Get-Render {
    param([string]$Label, [string]$Tfm, [string[]]$MSBuildArgs, [string[]]$Files)

    Invoke-Build -Tfm $Tfm -MSBuildArgs $MSBuildArgs
    $root = Join-Path (Get-ObjDir $Tfm) 'unoresizetizer'
    $dest = Join-Path $snapshots $Label
    New-Item -ItemType Directory -Force $dest | Out-Null
    $copies = @{}
    foreach ($file in $Files) {
        $copy = Join-Path $dest ($file -replace '[\\/]', '_')
        Copy-Item (Join-Path $root $file) $copy
        $copies[$file] = $copy
    }

    $names = Get-ChildItem $root -Recurse -File | ForEach-Object { [IO.Path]::GetRelativePath($root, $_.FullName) } | Sort-Object
    [pscustomobject]@{ Files = $copies; Names = $names }
}

# Pixels that differ between two renders: how many, whether their centroid is bottom-centre, and their commonest colour.
function Compare-Render {
    param([string]$Badged, [string]$Plain)

    $a = [System.Drawing.Bitmap]::new($Badged)
    $b = [System.Drawing.Bitmap]::new($Plain)
    try {
        $count = 0
        $sumX = 0.0
        $sumY = 0.0
        $colors = @{}
        for ($y = 0; $y -lt $a.Height; $y++) {
            for ($x = 0; $x -lt $a.Width; $x++) {
                $p = $a.GetPixel($x, $y)
                if ($p.ToArgb() -ne $b.GetPixel($x, $y).ToArgb()) {
                    $count++
                    $sumX += $x
                    $sumY += $y
                    $key = '{0:X2}{1:X2}{2:X2}' -f $p.R, $p.G, $p.B
                    $colors[$key] = 1 + [int]$colors[$key]
                }
            }
        }

        $dominant = ''
        if ($count -gt 0) {
            $dominant = ($colors.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 1).Key
        }

        [pscustomobject]@{
            Changed      = $count
            BottomCentre = $count -gt 0 -and [Math]::Abs($sumX / $count - $a.Width / 2) -lt ($a.Width * 0.1) -and ($sumY / $count) -gt ($a.Height / 2)
            Dominant     = $dominant
        }
    }
    finally {
        $a.Dispose()
        $b.Dispose()
    }
}

function Assert-That {
    param([string]$Label, [bool]$Condition, [string]$Detail = '')

    if ($Condition) {
        Write-Host ('  PASS  {0}' -f $Label) -ForegroundColor Green
    }
    else {
        Write-Host ('  FAIL  {0}' -f $Label) -ForegroundColor Red
        if ($Detail) {
            Write-Host ('          {0}' -f $Detail) -ForegroundColor Red
        }
        $script:failures++
    }
}

function Test-SameBytes([string]$Left, [string]$Right) {
    (Get-FileHash $Left).Hash -eq (Get-FileHash $Right).Hash
}

function Test-Badged($Diff, [string]$Color = $devColor) {
    $Diff.Changed -gt 0 -and $Diff.BottomCentre -and $Diff.Dominant -eq $Color
}

try {
    # The desktop head renders the splash like any other image (r/); the Windows head puts it under sp/.
    $icon = 'r/Assets/Icons/icon_transparentLogo.targetsize-256.png'
    $splash = if ($TargetFramework -like '*-windows*') { 'sp/splash_screen.scale-100.png' } else { 'r/splash_screen.scale-100.png' }
    $files = @($icon, $splash)
    $obj = Get-ObjDir $TargetFramework

    # Start clean so D4 compares resource names from this run only. The stamps go too, or Resizetizer thinks it's
    # up to date and renders nothing.
    $clean = 'unoresizetizer', 'devassets', 'UnoImage.stamp', 'Unosplash.stamp' | ForEach-Object { Join-Path $obj $_ }
    Remove-Item $clean -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host "Rendering $TargetFramework as Dev (opted out), Dev and Prod..."
    $plain = Get-Render -Label 'dev-optout' -Tfm $TargetFramework -MSBuildArgs @('-p:AppChannel=Dev', '-p:GenerateDevAssets=false') -Files $files
    $dev = Get-Render -Label 'dev' -Tfm $TargetFramework -MSBuildArgs @('-p:AppChannel=Dev') -Files $files
    Remove-Item (Join-Path $obj 'devassets') -Recurse -Force -ErrorAction SilentlyContinue
    $prod = Get-Render -Label 'prod' -Tfm $TargetFramework -MSBuildArgs @('-p:AppChannel=Prod') -Files $files

    foreach ($file in $files) {
        $diff = Compare-Render -Badged $dev.Files[$file] -Plain $plain.Files[$file]
        Assert-That "D1  Dev badges $file bottom-centre" (Test-Badged $diff) "changed=$($diff.Changed) bottomCentre=$($diff.BottomCentre) dominant=$($diff.Dominant)"
    }

    Assert-That 'D2  Prod writes nothing under devassets' (-not (Test-Path (Join-Path $obj 'devassets')))

    $renamed = Compare-Object @($plain.Names) @($dev.Names)
    Assert-That 'D4  Dev keeps every generated resource name' ($null -eq $renamed) ($renamed | Out-String)

    foreach ($file in $files) {
        Assert-That "D6  GenerateDevAssets=false renders $file exactly like Prod" (Test-SameBytes $plain.Files[$file] $prod.Files[$file])
    }

    $ci = Get-Render -Label 'ci' -Tfm $TargetFramework -MSBuildArgs @('-p:AppChannel=Dev', '-p:CI=true') -Files @($icon)
    $diff = Compare-Render -Badged $ci.Files[$icon] -Plain $plain.Files[$icon]
    Assert-That 'D7  A CI build draws the blue CI badge' (Test-Badged $diff $ciColor) "changed=$($diff.Changed) bottomCentre=$($diff.BottomCentre) dominant=$($diff.Dominant)"

    # D3: change the foreground in place (a half-transparent veil over everything); an incremental build must re-render.
    New-Item -ItemType Directory -Force $snapshots | Out-Null
    $foreground = Join-Path $snapshots 'icon_foreground.svg'
    Copy-Item (Join-Path $projectDir 'Assets/Icons/icon_foreground.svg') $foreground
    $override = @('-p:AppChannel=Dev', "-p:UnoIconForegroundFile=$foreground")
    $before = Get-Render -Label 'd3-before' -Tfm $TargetFramework -MSBuildArgs $override -Files @($icon)
    $svg = Get-Content -Raw $foreground
    $veil = '<circle cx="0" cy="0" r="1000000" fill="#000000" fill-opacity="0.5" /></svg>'
    $svg.Substring(0, $svg.LastIndexOf('</svg>')) + $veil | Set-Content -NoNewline $foreground
    $after = Get-Render -Label 'd3-after' -Tfm $TargetFramework -MSBuildArgs ($override + '--no-restore') -Files @($icon)
    Assert-That 'D3  Editing the prod icon re-renders the Dev icon on an incremental build' (-not (Test-SameBytes $before.Files[$icon] $after.Files[$icon]))

    if ($IncludeAndroid) {
        Write-Host 'Rendering net11.0-android...'
        $foregroundPng = 'AppIcons/mipmap-xxxhdpi/icon_foreground.png'
        $splashXml = 'sp/drawable-v31/uno_splash_image.xml'
        $androidPlain = Get-Render -Label 'android-optout' -Tfm 'net11.0-android' -MSBuildArgs @('-p:AppChannel=Dev', '-p:GenerateDevAssets=false') -Files @($foregroundPng)
        $androidDev = Get-Render -Label 'android-dev' -Tfm 'net11.0-android' -MSBuildArgs @('-p:AppChannel=Dev') -Files @($foregroundPng, $splashXml)
        $androidBack = Get-Render -Label 'android-optout-again' -Tfm 'net11.0-android' -MSBuildArgs @('-p:AppChannel=Dev', '-p:GenerateDevAssets=false') -Files @($foregroundPng)
        $diff = Compare-Render -Badged $androidDev.Files[$foregroundPng] -Plain $androidPlain.Files[$foregroundPng]
        $keepsSplash = (Get-Content -Raw $androidDev.Files[$splashXml]).Contains('@drawable/splash_screen')
        Assert-That 'D5  Android badges the adaptive icon and keeps @drawable/splash_screen' ((Test-Badged $diff) -and $keepsSplash) "changed=$($diff.Changed) bottomCentre=$($diff.BottomCentre) dominant=$($diff.Dominant) keepsSplash=$keepsSplash"

        # Resizetizer's adaptive-icon generator ignores a switch to an older file; InvalidateStaleAndroidAppIcons covers it.
        Assert-That 'D5  Switching Android back to the prod artwork re-renders the plain icon' (Test-SameBytes $androidPlain.Files[$foregroundPng] $androidBack.Files[$foregroundPng])
    }
    else {
        Write-Host '  SKIP  D5  Android (pass -IncludeAndroid)' -ForegroundColor DarkGray
    }
}
finally {
    Remove-Item $snapshots -Recurse -Force -ErrorAction SilentlyContinue
    Pop-Location
}

if ($script:failures -gt 0) {
    Write-Host "$($script:failures) check(s) failed." -ForegroundColor Red
    exit 1
}

Write-Host 'All Dev asset checks passed.' -ForegroundColor Green
