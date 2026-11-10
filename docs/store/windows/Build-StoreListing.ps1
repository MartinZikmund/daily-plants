<#
.SYNOPSIS
Builds a folder for Partner Center's "Import listings > Upload folder" from the sources next to this script.

.DESCRIPTION
Text comes from listing/<lang>.md. The DesktopScreenshot slides come from artifacts/store/windows/images, where
screenshots/Render-Slides.ps1 renders them, and other images from images/ (images/<lang>/ overrides a file for one language).
The CSV only has rows for what these sources cover, and Partner Center leaves every row it doesn't get,
such as trailers or hardware requirements, as it was.

.EXAMPLE
./Build-StoreListing.ps1
./Build-StoreListing.ps1 -CheckOnly
#>
[CmdletBinding()]
param(
    [string] $OutputPath = (Join-Path $PSScriptRoot '../../../artifacts/store/windows'),

    # Only check the sources against the Store's limits.
    [switch] $CheckOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# The folder you pick in Partner Center. Its name is part of every image path in the CSV.
$UploadFolderName = 'store-listing'

$TextType = 'Text'
$ImageType = 'Relative path (or URL to file in Partner Center)'

# Partner Center's field IDs and limits. The IDs come from its listing export and must match it exactly.
$TextFields = [ordered]@{
    Description                   = @{ Id = 2; Limit = 10000 }
    ReleaseNotes                  = @{ Id = 3; Limit = 1500 }
    Title                         = @{ Id = 4; Limit = 256 }
    ShortTitle                    = @{ Id = 5; Limit = 50 }
    SortTitle                     = @{ Id = 6; Limit = 255 }
    VoiceTitle                    = @{ Id = 7; Limit = 255 }
    ShortDescription              = @{ Id = 8; Limit = 1000 }
    DevStudio                     = @{ Id = 9; Limit = 255 }
    CopyrightTrademarkInformation = @{ Id = 12; Limit = 200 }
    AdditionalLicenseTerms        = @{ Id = 13; Limit = 10000 }
}
$RequiredText = 'Title', 'Description'
$ShortDescriptionVisible = 270

# Numbered fields: <Row>1 has FirstId, and each next one counts up.
$ListFields = @{
    Features           = @{ Row = 'Feature'; FirstId = 700; Max = 20; Length = 200 }
    SearchTerms        = @{ Row = 'SearchTerm'; FirstId = 900; Max = 7; Length = 30 }
    ScreenshotCaptions = @{ Row = 'DesktopScreenshotCaption'; FirstId = 150; Max = 30; Length = 200 }
}
$MaxSearchTermWords = 21
$ScreenshotFirstId = 100
$ImageFields = @{
    StoreLogo720x1080   = 600
    StoreLogo1080x1080  = 601
    StoreLogo300x300    = 602
    PromoImage1920x1080 = 606
    PromoImage2400x1200 = 607
}

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

# Where screenshots/Render-Slides.ps1 puts the DesktopScreenshot slides. They're build output, so they aren't committed.
$RenderedImagesPath = Join-Path $PSScriptRoot '../../../artifacts/store/windows/images'

# Image files for one language by field name: the rendered slides, then images/, then images/<lang>/, each winning over the last.
function Get-Images([string] $Language) {
    $images = @{}
    foreach ($dir in $RenderedImagesPath, (Join-Path $PSScriptRoot 'images'), (Join-Path $PSScriptRoot "images/$Language")) {
        if (Test-Path -LiteralPath $dir) {
            Get-ChildItem -LiteralPath $dir -File -Filter '*.png' | ForEach-Object { $images[$_.BaseName] = $_ }
        }
    }
    return $images
}

$listingDir = Join-Path $PSScriptRoot 'listing'
$listings = [ordered]@{}
$errors = [System.Collections.Generic.List[string]]::new()

foreach ($file in Get-ChildItem -LiteralPath $listingDir -Filter '*.md' | Sort-Object Name) {
    $language = $file.BaseName
    $source = Read-Listing $file.FullName
    $values = [ordered]@{}

    foreach ($name in $source.Keys) {
        if (-not $TextFields.Contains($name) -and -not $ListFields.ContainsKey($name)) {
            $errors.Add("${language}: unknown section '## $name'.")
        }
    }

    foreach ($name in $TextFields.Keys) {
        $text = if ($source.Contains($name)) { $source[$name] } else { '' }
        $limit = $TextFields[$name].Limit
        if ($name -in $RequiredText -and $text -eq '') {
            $errors.Add("${language}: '## $name' is required.")
        }
        if ($text.Length -gt $limit) {
            $errors.Add("${language}: $name is $($text.Length) characters, the limit is $limit.")
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

    $images = Get-Images $language
    $screenshots = @($images.Keys | Where-Object { $_ -match '^DesktopScreenshot\d+$' } | ForEach-Object { [int]($_ -replace '\D', '') } | Sort-Object)
    # A text-only check doesn't need the slides rendered.
    $checkScreenshots = -not ($CheckOnly -and $screenshots.Count -eq 0)
    if ($checkScreenshots -and $screenshots.Count -eq 0) {
        $errors.Add("${language}: the Store needs at least one screenshot. Run screenshots/Render-Slides.ps1 first.")
    }
    elseif ($checkScreenshots -and $screenshots[-1] -ne $screenshots.Count) {
        $errors.Add("${language}: screenshots must be numbered 1 to $($screenshots.Count) without gaps, found $($screenshots -join ', ').")
    }
    if ($checkScreenshots -and $values.ScreenshotCaptions.Count -ne $screenshots.Count) {
        $errors.Add("${language}: $($screenshots.Count) screenshots need $($screenshots.Count) ScreenshotCaptions, found $($values.ScreenshotCaptions.Count).")
    }
    foreach ($field in $images.Keys | Where-Object { $_ -notmatch '^DesktopScreenshot\d+$' -and -not $ImageFields.ContainsKey($_) }) {
        $errors.Add("${language}: images/$field.png doesn't match a Partner Center image field.")
    }

    $listings[$language] = @{ Values = $values; Images = $images; Screenshots = $screenshots.Count }
}

if ($errors.Count -gt 0) {
    throw "The listing sources have $($errors.Count) problem(s):`n  " + ($errors -join "`n  ")
}

Write-Host "Validated $($listings.Count) listings: $($listings.Keys -join ', ')"
if ($CheckOnly) {
    return
}

$uploadDir = Join-Path $OutputPath $UploadFolderName
if (Test-Path -LiteralPath $uploadDir) {
    Remove-Item -LiteralPath $uploadDir -Recurse -Force
}
New-Item -ItemType Directory -Path $uploadDir | Out-Null

function Get-UploadPath([System.IO.FileInfo] $File) {
    $relative = [System.IO.Path]::GetRelativePath($PSScriptRoot, $File.FullName) -replace '\\', '/'
    # The rendered slides live outside this folder, and go into the upload folder's images/ like the committed ones.
    if ($relative.StartsWith('../')) {
        $relative = "images/$($File.Name)"
    }
    $target = Join-Path $uploadDir $relative
    if (-not (Test-Path -LiteralPath $target)) {
        New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
        Copy-Item -LiteralPath $File.FullName -Destination $target
    }
    return "$UploadFolderName/$relative"
}

$rows = [System.Collections.Generic.List[object]]::new()
function Add-Row([string] $Field, [int] $Id, [string] $Type, [hashtable] $Values) {
    $row = [ordered]@{ 'Field' = $Field; 'ID' = $Id; 'Type (Type)' = $Type; 'default' = '' }
    foreach ($language in $listings.Keys) {
        $row[$language] = if ($Values.ContainsKey($language)) { $Values[$language] } else { '' }
    }
    $rows.Add([pscustomobject]$row)
}

foreach ($name in $TextFields.Keys) {
    $values = @{}
    foreach ($language in $listings.Keys) {
        $values[$language] = $listings[$language].Values[$name]
    }
    Add-Row $name $TextFields[$name].Id $TextType $values
}

$screenshotCount = ($listings.Values | ForEach-Object Screenshots | Measure-Object -Maximum).Maximum
foreach ($name in $ListFields.Keys) {
    $spec = $ListFields[$name]
    # Every slot up to the maximum, so an item removed from a list is cleared rather than left behind.
    $slots = if ($name -eq 'ScreenshotCaptions') { $screenshotCount } else { $spec.Max }
    for ($i = 1; $i -le $slots; $i++) {
        $values = @{}
        foreach ($language in $listings.Keys) {
            $items = $listings[$language].Values[$name]
            if ($i -le $items.Count) {
                $values[$language] = $items[$i - 1]
            }
        }
        Add-Row "$($spec.Row)$i" ($spec.FirstId + $i - 1) $TextType $values
    }
}

# An empty image cell leaves Partner Center's image alone, so images are only ever added or replaced here.
$imageRows = [ordered]@{}
for ($i = 1; $i -le $screenshotCount; $i++) {
    $imageRows["DesktopScreenshot$i"] = $ScreenshotFirstId + $i - 1
}
foreach ($field in $ImageFields.Keys) {
    $imageRows[$field] = $ImageFields[$field]
}
foreach ($field in $imageRows.Keys) {
    $values = @{}
    foreach ($language in $listings.Keys) {
        $image = $listings[$language].Images[$field]
        if ($image) {
            $values[$language] = Get-UploadPath $image
        }
    }
    if ($values.Count -gt 0) {
        Add-Row $field $imageRows[$field] $ImageType $values
    }
}

$csvPath = Join-Path $uploadDir 'listing.csv'
$rows | Sort-Object { [int]$_.ID } | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding utf8BOM -UseQuotes AsNeeded

Write-Host "Upload folder: $(Resolve-Path -LiteralPath $uploadDir)"
Write-Host "In Partner Center, open the app overview, select Import listings > Upload folder, and pick that folder."
