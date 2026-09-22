<#
.SYNOPSIS
Renders slides.html into the Google Play images with headless Edge: images/phone/<N>.png, images/tablet/<N>.png,
images/feature-graphic.png and images/icon.png.
#>
[CmdletBinding()]
param(
    [string] $Browser = "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"
)

$ErrorActionPreference = 'Stop'

# The order they appear in Google Play.
$Slides = 'hero', 'details', 'statistics', 'achievements', 'resources', 'dark'

$imagesDir = Join-Path $PSScriptRoot '../images'
$page = ([System.Uri](Join-Path $PSScriptRoot 'slides.html')).AbsoluteUri
$profileDir = Join-Path ([System.IO.Path]::GetTempPath()) 'dailyplants-play-slides-edge'

function Save-Slide([string] $Query, [string] $Out, [int] $Width, [int] $Height, [double] $Scale, [string[]] $Extra = @()) {
    New-Item -ItemType Directory -Path (Split-Path $Out) -Force | Out-Null
    Remove-Item -LiteralPath $Out -ErrorAction SilentlyContinue
    & $Browser --headless=new --disable-gpu --hide-scrollbars --allow-file-access-from-files --user-data-dir="$profileDir" `
        --force-device-scale-factor=$Scale "--window-size=$Width,$Height" --virtual-time-budget=3000 @Extra `
        --screenshot="$Out" "$page`?$Query" 2>$null | Out-Null
    if (-not (Test-Path -LiteralPath $Out)) {
        throw "Rendering '$Query' failed."
    }
    Write-Host "$Query -> $Out"
}

for ($i = 0; $i -lt $Slides.Count; $i++) {
    # 360x640 at 4x is 1440x2560, and 1280x720 at 2x is 2560x1440: 9:16 and 16:9 with room to spare over Play's 1080 minimum.
    Save-Slide "device=phone&slide=$($Slides[$i])" (Join-Path $imagesDir "phone/$($i + 1).png") 360 640 4
    Save-Slide "device=tablet&slide=$($Slides[$i])" (Join-Path $imagesDir "tablet/$($i + 1).png") 1280 720 2
}
Save-Slide 'slide=feature' (Join-Path $imagesDir 'feature-graphic.png') 1024 500 1

# Play wants the icon as a 32-bit PNG, and Edge saves an opaque page without the alpha channel.
$rendered = Join-Path ([System.IO.Path]::GetTempPath()) 'dailyplants-play-icon.png'
Save-Slide 'slide=icon' $rendered 512 512 1
Add-Type -AssemblyName System.Drawing
$source = [System.Drawing.Bitmap]::new($rendered)
try {
    $argb = $source.Clone([System.Drawing.Rectangle]::new(0, 0, $source.Width, $source.Height), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $argb.Save((Join-Path $imagesDir 'icon.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    $argb.Dispose()
}
finally {
    $source.Dispose()
    Remove-Item -LiteralPath $rendered
}
Write-Host "icon -> $(Join-Path $imagesDir 'icon.png')"
