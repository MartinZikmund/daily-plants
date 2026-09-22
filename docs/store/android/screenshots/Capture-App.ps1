<#
.SYNOPSIS
Captures the app screens the Google Play slides are built from, on a phone and a tablet emulator.

.DESCRIPTION
Build the APK first:
  dotnet build src/DailyPlants/DailyPlants.csproj -c Release -f net10.0-android -r android-x64 -p:AndroidPackageFormat=apk
The script creates the dailyplants-phone (Pixel 9 Pro XL) and dailyplants-tablet (Pixel Tablet) emulators if they're missing,
reinstalls the app, seeds demo data with ../../windows/screenshots/seed-demo-data.cs, sets the settings the captures need,
cleans up the status bar and drives the app with adb. The captures land in captures/<device>/.
It only talks to the emulators it starts, by their serials, so a phone plugged in for debugging is left alone.

.PARAMETER PrepareOnly
Stops after seeding and leaves the app open, for working on the taps.
#>
[CmdletBinding()]
param(
    [ValidateSet('phone', 'tablet')]
    [string[]] $Device = @('phone', 'tablet'),
    [string] $Apk = (Join-Path $PSScriptRoot '../../../../src/DailyPlants/bin/Release/net10.0-android/android-x64/dev.mzikmund.dailyplants-Signed.apk'),
    [switch] $PrepareOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Package = 'dev.mzikmund.dailyplants'
# Google APIs images allow adb root, which seeding the app's private data needs. Google Play images don't.
$SystemImage = 'system-images;android-37.0;google_apis;x86_64'
$Devices = @{
    phone = @{ Avd = 'dailyplants-phone'; Name = 'pixel_9_pro_xl'; Width = 1344; Height = 2992; Density = 480; Orientation = 'portrait'; Port = 5570 }
    tablet = @{ Avd = 'dailyplants-tablet'; Name = 'pixel_tablet'; Width = 2560; Height = 1600; Density = 320; Orientation = 'landscape'; Port = 5572 }
}
# Uno stores ApplicationData settings as type-prefixed strings.
$Settings = [ordered]@{
    DailyDozenEnabled = 'System.Boolean:True'
    TwentyOneTweaksEnabled = 'System.Boolean:False'
    WeightTrackingEnabled = 'System.Boolean:True'
    GoalWeight = 'System.Double:72'
    HeightCm = 'System.Double:178'
    SeenTips = 'System.String:diary-log-serving,diary-day-progress,diary-past-days'
    '__Uno.PrimaryLanguageOverride' = 'System.String:en'
    ThemePreference = 'System.Int32:1'
}

function Find-Sdk {
    $candidates = @($env:ANDROID_HOME, $env:ANDROID_SDK_ROOT, "${env:ProgramFiles(x86)}\Android\android-sdk", "$env:LOCALAPPDATA\Android\Sdk")
    $image = $SystemImage -replace ';', '\'
    foreach ($candidate in $candidates | Where-Object { $_ }) {
        if (Test-Path (Join-Path $candidate "$image\system.img")) {
            return $candidate
        }
    }
    throw "No Android SDK with $SystemImage. Install it with: sdkmanager --install `"$SystemImage`" emulator platform-tools"
}

$Sdk = Find-Sdk
$Adb = Join-Path $Sdk 'platform-tools\adb.exe'
$Emulator = Join-Path $Sdk 'emulator\emulator.exe'
$Seeder = Resolve-Path (Join-Path $PSScriptRoot '../../windows/screenshots')
$Work = Join-Path ([System.IO.Path]::GetTempPath()) 'dailyplants-android-capture'

function Invoke-Adb {
    $output = & $Adb -s $script:Serial @args 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "adb $($args -join ' ') failed: $output"
    }
    return $output
}

function Invoke-Shell([string] $Command) {
    Invoke-Adb shell $Command
}

function New-Avd($Spec) {
    $avdHome = if ($env:ANDROID_AVD_HOME) { $env:ANDROID_AVD_HOME } else { Join-Path $HOME '.android\avd' }
    $dir = Join-Path $avdHome "$($Spec.Avd).avd"
    if (Test-Path $dir) {
        return
    }
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    Set-Content -Path (Join-Path $avdHome "$($Spec.Avd).ini") -Value @('avd.ini.encoding=UTF-8', "path=$dir", 'target=android-37.0')
    Set-Content -Path (Join-Path $dir 'config.ini') -Value @(
        'avd.ini.encoding=UTF-8'
        "AvdId=$($Spec.Avd)"
        'PlayStore.enabled=no'
        'abi.type=x86_64'
        'hw.cpu.arch=x86_64'
        'hw.cpu.ncore=4'
        'hw.ramSize=4096'
        'disk.dataPartition.size=6G'
        'hw.device.manufacturer=Google'
        "hw.device.name=$($Spec.Name)"
        "hw.lcd.width=$($Spec.Width)"
        "hw.lcd.height=$($Spec.Height)"
        "hw.lcd.density=$($Spec.Density)"
        "hw.initialOrientation=$($Spec.Orientation)"
        'hw.gpu.enabled=yes'
        'hw.gpu.mode=host'
        'hw.keyboard=yes'
        "image.sysdir.1=$($SystemImage -replace ';', '\')\"
        'tag.id=google_apis'
        'tag.display=Google APIs'
        'showDeviceFrame=yes'
    )
    Write-Host "Created the $($Spec.Avd) emulator"
}

function Start-Device($Spec) {
    $running = (& $Adb devices) -match "^$([regex]::Escape($script:Serial))\s+device"
    if (-not $running) {
        Start-Process -FilePath $Emulator -ArgumentList '-avd', $Spec.Avd, '-port', $Spec.Port, '-no-snapshot', '-no-boot-anim', '-no-audio'
    }
    & $Adb -s $script:Serial wait-for-device
    for ($i = 0; $i -lt 180 -and (& $Adb -s $script:Serial shell getprop sys.boot_completed 2>$null) -ne '1'; $i++) {
        Start-Sleep -Seconds 1
    }
    Invoke-Adb root | Out-Null
    Start-Sleep -Seconds 2
    & $Adb -s $script:Serial wait-for-device
    Invoke-Shell 'settings put system accelerometer_rotation 0' | Out-Null
    Invoke-Shell 'settings put system user_rotation 0' | Out-Null
}

function Start-App {
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        Invoke-Shell "am start -W -n $script:Activity" | Out-Null
        # The splash screen stays up while the app loads.
        Start-Sleep -Seconds 8
        # Now and then the first layout never finishes and the app spins with everything drawn at the top left.
        if (Find-Node 'Diary') {
            return
        }
        Write-Warning "The app didn't finish laying out the Diary, starting it again."
        Stop-App
    }
    throw "The app didn't show the Diary after 3 launches."
}

function Stop-App {
    Invoke-Shell "am force-stop $Package" | Out-Null
    Start-Sleep -Seconds 1
}

# Copies a file into the app's data folder and gives it back to the app's user.
function Push-AppFile([string] $Local, [string] $Remote) {
    Invoke-Adb push $Local $Remote | Out-Null
    $owner = (Invoke-Shell "stat -c %u:%g /data/data/$Package").Trim()
    Invoke-Shell "chown $owner '$Remote' && restorecon '$Remote'" | Out-Null
}

function Set-Settings([System.Collections.IDictionary] $Values) {
    $remote = "/data/data/$Package/shared_prefs/${Package}_preferences.xml"
    $local = Join-Path $Work 'preferences.xml'
    [xml] $prefs = '<?xml version=''1.0'' encoding=''utf-8'' standalone=''yes'' ?><map />'
    if ((Invoke-Shell "[ -f '$remote' ] && echo yes || echo no").Trim() -eq 'yes') {
        Invoke-Adb pull $remote $local | Out-Null
        $prefs = [xml] (Get-Content -Raw $local)
    }
    foreach ($key in $Values.Keys) {
        $node = $prefs.map.SelectSingleNode("string[@name='$key']")
        if (-not $node) {
            $node = $prefs.CreateElement('string')
            $node.SetAttribute('name', $key)
            $prefs.DocumentElement.AppendChild($node) | Out-Null
        }
        $node.InnerText = $Values[$key]
    }
    $writer = [System.Xml.XmlWriter]::Create($local, [System.Xml.XmlWriterSettings]@{ Encoding = [System.Text.UTF8Encoding]::new($false); Indent = $true })
    try {
        $prefs.Save($writer)
    }
    finally {
        $writer.Dispose()
    }
    Invoke-Shell "mkdir -p /data/data/$Package/shared_prefs" | Out-Null
    Push-AppFile $local $remote
    Invoke-Shell "chown $((Invoke-Shell "stat -c %u:%g /data/data/$Package").Trim()) /data/data/$Package/shared_prefs" | Out-Null
}

# Android's demo mode: a fixed clock, full battery and signal, no notification icons.
function Set-DemoStatusBar([bool] $On) {
    if (-not $On) {
        Invoke-Shell 'am broadcast -a com.android.systemui.demo -e command exit' | Out-Null
        return
    }
    Invoke-Shell 'settings put global sysui_demo_allowed 1' | Out-Null
    foreach ($command in @(
            '-e command enter'
            '-e command clock -e hhmm 0941'
            '-e command battery -e level 100 -e plugged false -e powersave false'
            '-e command network -e wifi show -e level 4 -e fully true'
            # With a SIM, the emulator's modem shows 3G whatever the demo command says.
            '-e command network -e mobile hide'
            '-e command notifications -e visible false')) {
        Invoke-Shell "am broadcast -a com.android.systemui.demo $command" | Out-Null
    }
}

# Uno's Android accessibility tree only has the first item of each list and nothing inside dialogs,
# so named taps work for some elements and the rest tap by position, in dp.
function Find-Node([string] $Name) {
    # The dump fails while the app keeps redrawing, so a stale file must not be read instead.
    Invoke-Shell 'rm -f /sdcard/ui.xml' | Out-Null
    $result = & $Adb -s $script:Serial shell uiautomator dump /sdcard/ui.xml 2>&1
    if ("$result" -notmatch 'dumped to') {
        return $null
    }
    $local = Join-Path $Work 'ui.xml'
    Invoke-Adb pull /sdcard/ui.xml $local | Out-Null
    return ([xml] (Get-Content -Raw $local)).SelectNodes('//node') |
        Where-Object { $_.'content-desc' -eq $Name -or $_.text -eq $Name } | Select-Object -First 1
}

function Invoke-TapNamed([string] $Name) {
    $node = Find-Node $Name
    if (-not $node) {
        throw "Couldn't find '$Name' on the screen."
    }
    $bounds = [regex]::Match($node.bounds, '\[(\d+),(\d+)\]\[(\d+),(\d+)\]').Groups | Select-Object -Skip 1 | ForEach-Object { [int] $_.Value }
    Invoke-Shell "input tap $(($bounds[0] + $bounds[2]) / 2) $(($bounds[1] + $bounds[3]) / 2)" | Out-Null
    Start-Sleep -Milliseconds 1500
}

# Back closes a ContentDialog wherever its buttons ended up.
function Close-Dialog {
    Invoke-Shell 'input keyevent KEYCODE_BACK' | Out-Null
    Start-Sleep -Milliseconds 1500
}

function Invoke-Tap([double] $X, [double] $Y) {
    $scale = $script:Density / 160
    Invoke-Shell "input tap $([int]($X * $scale)) $([int]($Y * $scale))" | Out-Null
    Start-Sleep -Milliseconds 1500
}

function Invoke-Swipe([double] $X, [double] $FromY, [double] $ToY) {
    $scale = $script:Density / 160
    Invoke-Shell "input swipe $([int]($X * $scale)) $([int]($FromY * $scale)) $([int]($X * $scale)) $([int]($ToY * $scale)) 600" | Out-Null
    # Lets the scroll bar fade out.
    Start-Sleep -Seconds 4
}

# Taps are in dp on a Pixel 9 Pro XL (448x997 dp).
function Invoke-PhoneFlow {
    Save-Capture 'diary'
    Invoke-TapNamed 'Beans'
    # The dialog loads the latest videos on the item.
    Start-Sleep -Seconds 4
    Save-Capture 'details'
    Close-Dialog

    Invoke-TapNamed 'Open Navigation'
    Invoke-Tap 78 196
    Save-Capture 'statistics'
    Invoke-Swipe 220 800 200
    Save-Capture 'statistics-weight'

    Invoke-TapNamed 'Open Navigation'
    Invoke-Tap 92 236
    Save-Capture 'achievements'

    Invoke-TapNamed 'Open Navigation'
    Invoke-Tap 80 156
    Start-Sleep -Seconds 4
    # The feed filter, then Recipes
    Invoke-TapNamed 'Latest'
    Invoke-Tap 86 401
    Start-Sleep -Seconds 4
    Save-Capture 'resources'
}

# Taps are in dp on a Pixel Tablet in landscape (1280x800 dp), where the navigation pane stays open.
function Invoke-TabletFlow {
    Save-Capture 'diary'
    Invoke-TapNamed 'Beans'
    Start-Sleep -Seconds 4
    Save-Capture 'details'
    Close-Dialog

    Invoke-Tap 78 167
    Save-Capture 'statistics'
    Invoke-Swipe 800 700 150
    Save-Capture 'statistics-weight'

    Invoke-Tap 92 207
    Save-Capture 'achievements'

    Invoke-Tap 80 127
    Start-Sleep -Seconds 4
    # The Recipes tab
    Invoke-Tap 790 215
    Start-Sleep -Seconds 4
    Save-Capture 'resources'
}

function Save-Capture([string] $Name) {
    Start-Sleep -Milliseconds 1500
    Invoke-Shell 'screencap -p /sdcard/capture.png' | Out-Null
    Invoke-Adb pull /sdcard/capture.png (Join-Path $script:OutDir "$Name.png") | Out-Null
    Write-Host "Captured $($script:DeviceName) $Name"
}

if (-not (Test-Path $Apk)) {
    throw "No APK at $Apk. Build the Android head first (see the top of this script)."
}
New-Item -ItemType Directory -Path $Work -Force | Out-Null

foreach ($name in $Device) {
    $spec = $Devices[$name]
    $script:DeviceName = $name
    $script:Density = $spec.Density
    $script:Serial = "emulator-$($spec.Port)"
    $script:OutDir = Join-Path $PSScriptRoot "captures\$name"
    New-Item -ItemType Directory -Path $script:OutDir -Force | Out-Null

    Write-Host "== $name ($($spec.Avd), $script:Serial)"
    New-Avd $spec
    Start-Device $spec

    & $Adb -s $script:Serial uninstall $Package 2>&1 | Out-Null
    Invoke-Adb install $Apk | Out-Null
    $script:Activity = (Invoke-Shell "cmd package resolve-activity --brief $Package" | Select-Object -Last 1).Trim()

    # The first launch creates the database.
    Start-App
    Stop-App
    $remoteDb = (Invoke-Shell "find /data/data/$Package -name dailyplants.db" | Select-Object -First 1).Trim()
    if (-not $remoteDb) {
        throw "The app didn't create its database."
    }
    $localDb = Join-Path $Work 'dailyplants.db'
    Invoke-Adb pull $remoteDb $localDb | Out-Null
    Push-Location $Seeder
    try {
        dotnet run seed-demo-data.cs -- $localDb
        if ($LASTEXITCODE -ne 0) {
            throw 'Seeding the demo data failed.'
        }
    }
    finally {
        Pop-Location
    }
    Invoke-Shell "rm -f '$remoteDb-wal' '$remoteDb-shm' '$remoteDb-journal'" | Out-Null
    Push-AppFile $localDb $remoteDb
    Set-Settings $Settings
    Set-DemoStatusBar $true

    Start-App
    if ($PrepareOnly) {
        Write-Host "The app is open with demo data on $script:Serial."
        continue
    }

    & "Invoke-$((Get-Culture).TextInfo.ToTitleCase($name))Flow"

    Stop-App
    Set-Settings @{ ThemePreference = 'System.Int32:2' }
    Start-App
    Save-Capture 'diary-dark'

    Stop-App
    Set-DemoStatusBar $false
    Invoke-Adb emu kill | Out-Null
}
Write-Host "Captures are in $(Join-Path $PSScriptRoot 'captures')"
