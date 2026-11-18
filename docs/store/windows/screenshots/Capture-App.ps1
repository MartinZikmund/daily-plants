<#
.SYNOPSIS
Captures the app screens the store slides are built from, using the packaged WinAppSDK build and demo data.

.DESCRIPTION
Build and deploy the Windows head first (F5 once is enough), keep the app language on English, and close the app.
The script backs up your database and the app's settings, seeds demo data, drives the app through UI Automation,
writes the captures to captures/ and puts your data back, even if something fails on the way.
It clicks one diary row with the mouse, so leave the mouse alone for the minute it runs.
#>
[CmdletBinding()]
param(
    [string] $OutDir = (Join-Path $PSScriptRoot 'captures')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$PackageFamily = '5143MartinZikmund.DailyPlants_4b2wsj7nzv900'
# slides.html crops by pixel, so every capture is scaled to this size.
$CaptureWidth = 2400
$CaptureHeight = 1400

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -ReferencedAssemblies System.Drawing, System.Drawing.Common, System.Drawing.Primitives, System.Private.Windows.GdiPlus, System.Private.Windows.Core @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr hwnd, ref POINT point);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("kernel32.dll")] static extern void Sleep(uint milliseconds);

    public static void MakeDpiAware() { SetProcessDpiAwarenessContext(new IntPtr(-4)); }

    public static double Scale(IntPtr hwnd) { return GetDpiForWindow(hwnd) / 96.0; }

    public static void SizeClient(IntPtr hwnd, int width, int height)
    {
        RECT window, client;
        GetWindowRect(hwnd, out window);
        GetClientRect(hwnd, out client);
        int extraWidth = (window.Right - window.Left) - client.Right;
        int extraHeight = (window.Bottom - window.Top) - client.Bottom;
        SetWindowPos(hwnd, IntPtr.Zero, 40, 0, width + extraWidth, height + extraHeight, 0x0004);
    }

    // PW_RENDERFULLCONTENT captures the DWM-composed window, so it works even when other windows overlap it.
    public static void Capture(IntPtr hwnd, string path, int width, int height)
    {
        RECT window, client;
        GetWindowRect(hwnd, out window);
        GetClientRect(hwnd, out client);
        POINT origin = new POINT();
        ClientToScreen(hwnd, ref origin);
        using (Bitmap full = new Bitmap(window.Right - window.Left, window.Bottom - window.Top, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(full))
            {
                IntPtr hdc = g.GetHdc();
                PrintWindow(hwnd, hdc, 2);
                g.ReleaseHdc(hdc);
            }
            Rectangle area = new Rectangle(origin.X - window.Left, origin.Y - window.Top, client.Right, client.Bottom);
            using (Bitmap scaled = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(scaled))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(full, new Rectangle(0, 0, width, height), area, GraphicsUnit.Pixel);
                }
                scaled.Save(path, ImageFormat.Png);
            }
        }
    }

    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

    // Refuses to click unless the point is on the app's own window, so a covered window never sends the click elsewhere.
    public static void Click(IntPtr hwnd, int x, int y)
    {
        SetForegroundWindow(hwnd);
        Sleep(400);
        POINT point = new POINT { X = x, Y = y };
        if (GetAncestor(WindowFromPoint(point), 2) != hwnd)
        {
            throw new InvalidOperationException("Another window covers the app, so the click was not sent.");
        }
        SetCursorPos(x, y);
        Sleep(120);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    }
}
'@
[Native]::MakeDpiAware()

$Auto = [System.Windows.Automation.AutomationElement]
$Scope = [System.Windows.Automation.TreeScope]

function Get-AppProcess {
    Get-Process DailyPlants -ErrorAction SilentlyContinue | Where-Object MainWindowHandle -NE 0 | Select-Object -First 1
}

function Find-Element([string] $Id) {
    $root = $Auto::FromHandle((Get-AppProcess).MainWindowHandle)
    foreach ($property in $Auto::AutomationIdProperty, $Auto::NameProperty) {
        $element = $root.FindFirst($Scope::Descendants, [System.Windows.Automation.PropertyCondition]::new($property, $Id))
        if ($element) {
            return $element
        }
    }
    throw "Couldn't find '$Id' in the app."
}

function Get-Pattern($Element, $Pattern) {
    $result = $null
    if ($Element.TryGetCurrentPattern($Pattern::Pattern, [ref] $result)) {
        return $result
    }
    return $null
}

function Invoke-Element([string] $Id) {
    $element = Find-Element $Id
    if ($p = Get-Pattern $element ([System.Windows.Automation.SelectionItemPattern])) { $p.Select() }
    elseif ($p = Get-Pattern $element ([System.Windows.Automation.InvokePattern])) { $p.Invoke() }
    else { throw "'$Id' can't be invoked." }
    Start-Sleep -Milliseconds 1500
}

function Set-Toggle([string] $Id, [bool] $On) {
    $toggle = Get-Pattern (Find-Element $Id) ([System.Windows.Automation.TogglePattern])
    if (($toggle.Current.ToggleState -eq 'On') -ne $On) {
        $toggle.Toggle()
        Start-Sleep -Milliseconds 800
    }
}

function Set-Text([string] $Id, [string] $Value) {
    $element = Find-Element $Id
    $element.SetFocus()
    (Get-Pattern $element ([System.Windows.Automation.ValuePattern])).SetValue($Value)
}

function Select-ComboItem([string] $Id, [int] $Index) {
    $combo = Find-Element $Id
    (Get-Pattern $combo ([System.Windows.Automation.ExpandCollapsePattern])).Expand()
    Start-Sleep -Milliseconds 600
    $items = $combo.FindAll($Scope::Descendants, [System.Windows.Automation.PropertyCondition]::new($Auto::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem))
    (Get-Pattern $items[$Index] ([System.Windows.Automation.SelectionItemPattern])).Select()
    Start-Sleep -Milliseconds 1200
}

function Set-ScrollPosition([double] $Percent) {
    $root = $Auto::FromHandle((Get-AppProcess).MainWindowHandle)
    $scrollable = $root.FindAll($Scope::Descendants, [System.Windows.Automation.PropertyCondition]::new($Auto::IsScrollPatternAvailableProperty, $true))
    foreach ($element in $scrollable) {
        $scroll = Get-Pattern $element ([System.Windows.Automation.ScrollPattern])
        if ($scroll.Current.VerticallyScrollable) {
            $scroll.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll, $Percent)
        }
    }
    Start-Sleep -Milliseconds 800
}

function Save-Capture([string] $Name) {
    Start-Sleep -Milliseconds 800
    [Native]::Capture((Get-AppProcess).MainWindowHandle, (Join-Path $OutDir "$Name.png"), $CaptureWidth, $CaptureHeight)
    Write-Host "Captured $Name"
}

if (Get-AppProcess) {
    throw 'Close Daily Plants first.'
}

$dataDir = Join-Path $env:LOCALAPPDATA 'DailyPlants'
$database = Join-Path $dataDir 'dailyplants.db'
$settingsDir = Join-Path $env:LOCALAPPDATA "Packages\$PackageFamily\Settings"
$backup = Join-Path ([System.IO.Path]::GetTempPath()) "dailyplants-capture-$(Get-Date -Format yyyyMMdd-HHmmss)"

New-Item -ItemType Directory -Path "$backup\db", "$backup\settings", $OutDir -Force | Out-Null
Get-ChildItem $dataDir -Filter 'dailyplants.db*' -File | Copy-Item -Destination "$backup\db"
Get-ChildItem $settingsDir -Force -File | Copy-Item -Destination "$backup\settings" -Force
Write-Host "Backed up your data to $backup"

try {
    Push-Location $PSScriptRoot
    try {
        dotnet run seed-demo-data.cs -- $database
        if ($LASTEXITCODE -ne 0) { throw 'Seeding the demo data failed.' }
    }
    finally {
        Pop-Location
    }

    Start-Process "shell:AppsFolder\$PackageFamily!App"
    for ($i = 0; $i -lt 60 -and -not (Get-AppProcess); $i++) { Start-Sleep -Milliseconds 500 }
    Start-Sleep -Seconds 4
    $hwnd = (Get-AppProcess).MainWindowHandle
    $scale = [Native]::Scale($hwnd)
    if ($scale -lt 1.5) {
        Write-Warning "Display scale is $([int]($scale * 100))%. Captures are upscaled to ${CaptureWidth}x$CaptureHeight, so use 150% or more for sharp slides."
    }
    # The slides were laid out on a 1600x933 DIP window, where the Diary shows two columns.
    [Native]::SizeClient($hwnd, [int](1600 * $scale), [int](933 * $scale))
    Start-Sleep -Seconds 2

    try { Invoke-Element 'Skip' } catch { }

    Invoke-Element 'NavItemSettings'
    Set-Toggle 'DailyDozenEnabledToggle' $true
    Set-Toggle 'TwentyOneTweaksEnabledToggle' $false
    Set-Toggle 'WeightTrackingEnabledToggle' $true
    Set-Text 'GoalWeightTextBox' '72'
    Set-Text 'HeightTextBox' '178'
    (Find-Element 'ThemeComboBox').SetFocus()
    Select-ComboItem 'ThemeComboBox' 1

    Invoke-Element 'NavItemDiary'
    Save-Capture 'diary'

    $row = (Find-Element 'Beans. Open details').Current.BoundingRectangle
    [Native]::Click($hwnd, [int]($row.X + $row.Width / 2), [int]($row.Y + $row.Height / 2))
    Start-Sleep -Seconds 2
    Find-Element 'Health Benefits' | Out-Null
    Save-Capture 'details'
    # Escape would switch the app to keyboard input and draw focus rectangles in every later capture.
    # The caption button is also called Close, and invoking that one quits the app.
    $root = $Auto::FromHandle($hwnd)
    $dialogClose = $root.FindAll($Scope::Descendants, [System.Windows.Automation.PropertyCondition]::new($Auto::NameProperty, 'Close')) |
        Where-Object { $_.Current.AutomationId -ne 'Close' } | Select-Object -First 1
    (Get-Pattern $dialogClose ([System.Windows.Automation.InvokePattern])).Invoke()
    Start-Sleep -Seconds 1

    Invoke-Element 'NavItemStatistics'
    Save-Capture 'statistics'
    Set-ScrollPosition 100
    Save-Capture 'statistics-weight'
    Set-ScrollPosition 0

    Invoke-Element 'NavItemAchievements'
    Save-Capture 'achievements'

    Invoke-Element 'NavItemResources'
    Invoke-Element 'ResourcesTabRecipesButton'
    Start-Sleep -Seconds 5
    Save-Capture 'resources'

    Invoke-Element 'NavItemSettings'
    Select-ComboItem 'ThemeComboBox' 2
    Invoke-Element 'NavItemDiary'
    Save-Capture 'diary-dark'
}
finally {
    Get-Process DailyPlants -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 2

    Get-ChildItem $dataDir -Filter 'dailyplants.db*' -File | Remove-Item -Force
    Get-ChildItem "$backup\db" -File | Copy-Item -Destination $dataDir
    foreach ($file in Get-ChildItem "$backup\settings" -Force -File) {
        $target = Join-Path $settingsDir $file.Name
        if (Test-Path -LiteralPath $target) {
            (Get-Item -LiteralPath $target -Force).Attributes = 'Normal'
        }
        Copy-Item -LiteralPath $file.FullName -Destination $target -Force
    }
    Write-Host "Restored your data. The backup stays in $backup until you delete it."
}

# The captures are committed, so keep them small. Lossless, so the slides don't change.
if (Get-Command oxipng -ErrorAction SilentlyContinue) {
    oxipng --quiet --opt 4 --strip safe (Get-ChildItem $OutDir -Filter '*.png').FullName
}
else {
    Write-Warning 'Install oxipng (https://github.com/oxipng/oxipng) to shrink the captures before committing them.'
}
