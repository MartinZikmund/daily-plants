<#
.SYNOPSIS
Makes one of the candidate icons the app icon: copies its SVG everywhere the app reads it
from and regenerates the Windows package images. Rebuild afterwards.
#>
param(
    [Parameter(Mandatory)]
    [ValidateSet('mixed-berries', 'strawberry', 'quarters', 'open-dots', 'wreath')]
    [string]$Name
)

$ErrorActionPreference = 'Stop'

# The plate behind the icon on Android and iOS. The dials sit on linen because their green dots vanish on green.
$plates = @{
    'mixed-berries' = '#4C7A56'
    'strawberry'    = '#4C7A56'
    'quarters'      = '#4C7A56'
    'open-dots'     = '#FBFAF5'
    'wreath'        = '#FBFAF5'
}

$art = Join-Path $PSScriptRoot "candidates\$Name.svg"
$app = Resolve-Path (Join-Path $PSScriptRoot '..\..\src\DailyPlants')

foreach ($target in 'Assets\Icons\icon_foreground.svg', 'Assets\Splash\splash_screen.svg', 'Assets\Svg\applogo.svg') {
    $path = Join-Path $app $target
    Copy-Item $art $path
    # Copy-Item keeps the candidate's old timestamp, and incremental builds skip files that look older than their last output.
    (Get-Item $path).LastWriteTime = Get-Date
}

@"
<svg xmlns="http://www.w3.org/2000/svg" width="456" height="456" viewBox="0 0 456 456">
  <rect width="456" height="456" fill="$($plates[$Name])"/>
</svg>
"@ | Set-Content (Join-Path $app 'Assets\Icons\icon.svg') -NoNewline

dotnet run (Join-Path $PSScriptRoot 'export-windows-assets.cs') -- $art (Join-Path $app 'Platforms\Windows')
if ($LASTEXITCODE -ne 0) {
    throw 'Exporting the Windows images failed.'
}

Write-Host "App icon set to '$Name'. Rebuild to see it."
