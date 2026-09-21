<#
.SYNOPSIS
Renders the store slides from slides.html into images/DesktopScreenshot<N>.png with headless Edge.
#>
[CmdletBinding()]
param(
    [string] $Browser = "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"
)

$ErrorActionPreference = 'Stop'

# The order they appear in the Store. Keep ScreenshotCaptions in listing/*.md in the same order.
$Slides = 'hero', 'details', 'statistics', 'achievements', 'resources', 'dark'

$imagesDir = Resolve-Path (Join-Path $PSScriptRoot '../images')
$page = ([System.Uri](Join-Path $PSScriptRoot 'slides.html')).AbsoluteUri
$profileDir = Join-Path ([System.IO.Path]::GetTempPath()) 'dailyplants-slides-edge'

for ($i = 0; $i -lt $Slides.Count; $i++) {
    $out = Join-Path $imagesDir "DesktopScreenshot$($i + 1).png"
    Remove-Item -LiteralPath $out -ErrorAction SilentlyContinue
    # 1920x1080 CSS pixels at 4/3 is 2560x1440, sharp enough for the enlarged crops without upscaling them much.
    & $Browser --headless=new --disable-gpu --hide-scrollbars --allow-file-access-from-files --user-data-dir="$profileDir" `
        --force-device-scale-factor=1.3333333 --window-size=1920,1080 --virtual-time-budget=3000 `
        --screenshot="$out" "$page`?slide=$($Slides[$i])" 2>$null | Out-Null
    if (-not (Test-Path -LiteralPath $out)) {
        throw "Rendering '$($Slides[$i])' failed."
    }
    Write-Host "$($Slides[$i]) -> $out"
}
