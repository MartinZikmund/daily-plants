<#
.SYNOPSIS
Builds a Partner Center import folder for the Microsoft Store listing from the sources next to this script.

.DESCRIPTION
Text comes from listing/<lang>.md and images from images/ (images/<lang>/ overrides a file for one language).
Every other value in the export, such as trailers and hardware requirements, is kept as exported.
Run without -ExportPath to only validate the sources.

.EXAMPLE
./Build-StoreListing.ps1 -ExportPath ~/Downloads/listingData-9NKK3K501RZG-1152921505701942828.csv
#>
[CmdletBinding()]
param(
    # The CSV from "Export listings" on the app overview page in Partner Center.
    [string] $ExportPath,

    [string] $OutputPath = (Join-Path $PSScriptRoot '../../../artifacts/store/windows')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# The import folder's name is part of every image path in the CSV.
$ImportFolderName = 'store-listing'

$TextLimits = [ordered]@{
    Title                         = 256
    ShortTitle                    = 50
    SortTitle                     = 255
    VoiceTitle                    = 255
    ShortDescription              = 1000
    Description                   = 10000
    ReleaseNotes                  = 1500
    DevStudio                     = 255
    CopyrightTrademarkInformation = 200
    AdditionalLicenseTerms        = 10000
}
$RequiredText = 'Title', 'Description'
$ShortDescriptionVisible = 270

$ListFields = @{
    Features           = @{ Row = 'Feature'; Max = 20; Length = 200 }
    SearchTerms        = @{ Row = 'SearchTerm'; Max = 7; Length = 30 }
    ScreenshotCaptions = @{ Row = 'DesktopScreenshotCaption'; Max = 30; Length = 200 }
}
$MaxSearchTermWords = 21

function Read-Listing([string] $Path) {
    $sections = [ordered]@{}
    $current = $null
    foreach ($line in Get-Content -LiteralPath $Path -Encoding utf8) {
        if ($line -match '^##\s+(\S+)\s*$') {
            $current = $Matches[1]
            if ($sections.Contains($current)) {
                throw "${Path}: '## $current' appears twice."
            }
            $sections[$current] = [System.Collections.Generic.List[string]]::new()
        }
        elseif ($null -ne $current) {
            $sections[$current].Add($line.TrimEnd())
        }
    }

    $listing = [ordered]@{}
    foreach ($name in $sections.Keys) {
        $listing[$name] = ($sections[$name] -join "`n").Trim("`n")
    }
    return $listing
}

function Get-ListItems([string] $Text) {
    $items = foreach ($line in $Text -split "`n") {
        if ($line -eq '') {
            continue
        }
        if ($line -notmatch '^- (.+)$') {
            throw "every line must be a '- ' list item, found '$line'"
        }
        $Matches[1].Trim()
    }
    return , @($items)
}

function Get-ScreenshotFiles([string] $Language) {
    $files = @{}
    foreach ($dir in (Join-Path $PSScriptRoot 'images'), (Join-Path $PSScriptRoot "images/$Language")) {
        if (Test-Path -LiteralPath $dir) {
            Get-ChildItem -LiteralPath $dir -File -Filter 'DesktopScreenshot*.png' | ForEach-Object {
                $files[[int]($_.BaseName -replace '\D', '')] = $_
            }
        }
    }
    return $files
}

$listingDir = Join-Path $PSScriptRoot 'listing'
$listings = [ordered]@{}
$errors = [System.Collections.Generic.List[string]]::new()

foreach ($file in Get-ChildItem -LiteralPath $listingDir -Filter '*.md' | Sort-Object Name) {
    $language = $file.BaseName
    $source = Read-Listing $file.FullName
    $values = [ordered]@{}

    foreach ($name in $source.Keys) {
        if (-not $TextLimits.Contains($name) -and -not $ListFields.ContainsKey($name)) {
            $errors.Add("${language}: unknown section '## $name'.")
        }
    }

    foreach ($name in $TextLimits.Keys) {
        $text = if ($source.Contains($name)) { $source[$name] } else { '' }
        if ($name -in $RequiredText -and $text -eq '') {
            $errors.Add("${language}: '## $name' is required.")
        }
        if ($text.Length -gt $TextLimits[$name]) {
            $errors.Add("${language}: $name is $($text.Length) characters, the limit is $($TextLimits[$name]).")
        }
        if ($name -eq 'ShortDescription' -and $text.Length -gt $ShortDescriptionVisible) {
            Write-Warning "${language}: ShortDescription is $($text.Length) characters; some Store views cut it at $ShortDescriptionVisible."
        }
        $values[$name] = $text
    }

    foreach ($name in $ListFields.Keys) {
        $spec = $ListFields[$name]
        $items = @()
        if ($source.Contains($name)) {
            try {
                $items = Get-ListItems $source[$name]
            }
            catch {
                $errors.Add("${language}: '## $name': $_")
            }
        }
        if ($items.Count -gt $spec.Max) {
            $errors.Add("${language}: $name has $($items.Count) items, the limit is $($spec.Max).")
        }
        foreach ($item in $items | Where-Object Length -GT $spec.Length) {
            $errors.Add("${language}: '$item' in $name is $($item.Length) characters, the limit is $($spec.Length).")
        }
        $values[$name] = $items
    }

    $words = @($values.SearchTerms | ForEach-Object { $_.ToLowerInvariant() -split '\s+' } | Select-Object -Unique)
    if ($words.Count -gt $MaxSearchTermWords) {
        $errors.Add("${language}: SearchTerms use $($words.Count) different words, the limit is $MaxSearchTermWords.")
    }

    $screenshots = Get-ScreenshotFiles $language
    if ($screenshots.Count -gt 0) {
        $numbers = @($screenshots.Keys | Sort-Object)
        if ($numbers[-1] -ne $numbers.Count) {
            $errors.Add("${language}: screenshots must be numbered 1 to $($numbers.Count) without gaps, found $($numbers -join ', ').")
        }
        if ($values.ScreenshotCaptions.Count -ne $screenshots.Count) {
            $errors.Add("${language}: $($screenshots.Count) screenshots need $($screenshots.Count) ScreenshotCaptions, found $($values.ScreenshotCaptions.Count).")
        }
    }
    elseif ($values.ScreenshotCaptions.Count -gt 0) {
        $errors.Add("${language}: ScreenshotCaptions need screenshots in images/, otherwise they can't be matched to the ones in Partner Center.")
    }

    $listings[$language] = @{ Values = $values; Screenshots = $screenshots }
}

if ($errors.Count -gt 0) {
    throw "The listing sources have $($errors.Count) problem(s):`n  " + ($errors -join "`n  ")
}

Write-Host "Validated $($listings.Count) listings: $($listings.Keys -join ', ')"
if (-not $ExportPath) {
    return
}

$rows = @(Import-Csv -LiteralPath $ExportPath -Encoding utf8)
$rowByField = @{}
foreach ($row in $rows | Where-Object Field) {
    $rowByField[$row.Field] = $row
}

$languageColumns = @($rows[0].PSObject.Properties.Name | Where-Object { $_ -notin 'Field', 'ID', 'Type (Type)', 'default' })
foreach ($language in $listings.Keys | Where-Object { $_ -notin $languageColumns }) {
    throw "The export has no '$language' listing. Add the language in Partner Center, export again, and rerun."
}
foreach ($language in $languageColumns | Where-Object { -not $listings.Contains($_) }) {
    Write-Warning "There is no listing/$language.md, so the '$language' listing is left as exported."
}

$importDir = Join-Path $OutputPath $ImportFolderName
if (Test-Path -LiteralPath $importDir) {
    Remove-Item -LiteralPath $importDir -Recurse -Force
}
New-Item -ItemType Directory -Path $importDir | Out-Null

function Get-ImportPath([System.IO.FileInfo] $File) {
    $relative = [System.IO.Path]::GetRelativePath($PSScriptRoot, $File.FullName) -replace '\\', '/'
    $target = Join-Path $importDir $relative
    if (-not (Test-Path -LiteralPath $target)) {
        New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
        Copy-Item -LiteralPath $File.FullName -Destination $target
    }
    return "$ImportFolderName/$relative"
}

function Get-ListRowCount([string] $Prefix) {
    return @($rowByField.Keys | Where-Object { $_ -match "^$Prefix\d+$" }).Count
}

foreach ($language in $listings.Keys) {
    $values = $listings[$language].Values

    foreach ($name in $TextLimits.Keys) {
        $rowByField[$name].$language = $values[$name]
    }

    foreach ($name in $ListFields.Keys) {
        $prefix = $ListFields[$name].Row
        if ($name -eq 'ScreenshotCaptions' -and $listings[$language].Screenshots.Count -eq 0) {
            continue
        }
        $items = $values[$name]
        for ($i = 1; $i -le (Get-ListRowCount $prefix); $i++) {
            $rowByField["$prefix$i"].$language = if ($i -le $items.Count) { $items[$i - 1] } else { '' }
        }
    }

    $screenshots = $listings[$language].Screenshots
    if ($screenshots.Count -gt 0) {
        for ($i = 1; $i -le (Get-ListRowCount 'DesktopScreenshot'); $i++) {
            $rowByField["DesktopScreenshot$i"].$language = if ($screenshots.ContainsKey($i)) { Get-ImportPath $screenshots[$i] } else { '' }
        }
    }

    foreach ($dir in (Join-Path $PSScriptRoot 'images'), (Join-Path $PSScriptRoot "images/$language")) {
        if (-not (Test-Path -LiteralPath $dir)) {
            continue
        }
        foreach ($image in Get-ChildItem -LiteralPath $dir -File | Where-Object BaseName -NotMatch '^DesktopScreenshot\d+$') {
            if (-not $rowByField.ContainsKey($image.BaseName)) {
                throw "images/$($image.Name) doesn't match a field in the export."
            }
            $rowByField[$image.BaseName].$language = Get-ImportPath $image
        }
    }
}

$csvPath = Join-Path $importDir 'listing.csv'
$rows | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding utf8BOM -UseQuotes AsNeeded

Write-Host "Import folder: $(Resolve-Path -LiteralPath $importDir)"
Write-Host "In Partner Center, open the app overview, select Import listings > Import folder, and choose that folder."
