# Windows (WinAppSDK) head

On `net11.0-windows10.0.26100` the app is a plain WinUI / Windows App SDK app (no Uno at
runtime). See [How Uno Platform Works](https://platform.uno/docs/articles/how-uno-works.html#windows-using-winappsdk).

## Running from the command line

### Packaged

[`Microsoft.Windows.SDK.BuildTools.WinApp`](https://www.nuget.org/packages/Microsoft.Windows.SDK.BuildTools.WinApp)
(referenced in `DailyPlants.csproj`) hooks into `dotnet run`: it builds, creates a loose-layout
package, registers it with Windows and launches it.

```powershell
# From src/DailyPlants
dotnet run -f net11.0-windows10.0.26100
```

Needs Developer Mode (or an elevated terminal) to register the package.

Handy MSBuild properties (`-p:Name=Value`):

| Property | Default | Description |
|----------|---------|-------------|
| `EnableWinAppRunSupport` | `true` | Turn the `dotnet run` integration on/off. |
| `WinAppLaunchArgs` | (empty) | Arguments passed to the app. |
| `WinAppRunUseExecutionAlias` | `false` | Launch via execution alias (keeps console I/O in the terminal). |
| `WinAppRunNoLaunch` | `false` | Register only, don't launch (attach your own debugger). |
| `WinAppRunDebugOutput` | `false` | Capture `OutputDebugString` and first-chance exceptions. |

### Unpackaged

```powershell
dotnet run -f net11.0-windows10.0.26100 -p:WindowsPackageType=None --launch-profile "DailyPlants (WinAppSDK Unpackaged)"
```

## IDE

Pick **DailyPlants (WinAppSDK Packaged)** or **DailyPlants (WinAppSDK Unpackaged)** from the
debug target dropdown.

> A [Visual Studio issue](https://aka.platform.uno/wasdk-maui-debug-profile-issue) can hide the
> unpackaged profile when iOS/Android target frameworks are present. If so, comment out the
> packaged profile in `Properties/launchSettings.json`.
