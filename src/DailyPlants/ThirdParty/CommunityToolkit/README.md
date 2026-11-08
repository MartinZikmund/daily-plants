# Community Toolkit settings controls (source copy)

`SettingsCard` and `SettingsExpander`, plus the state triggers their templates use, copied from
[CommunityToolkit/Windows](https://github.com/CommunityToolkit/Windows) at tag `v8.2.251219`, because no published
build targets Uno 7 yet. MIT licensed, see [License.md](License.md).

The folder layout and namespaces match the package, so nothing outside this folder knows it is a copy except the path
in `App.xaml`. The only edits to the upstream files:

- `using` lines at the top of the C# files, which upstream's build tooling supplies as global usings
- the `ms-appx` paths in `Themes/Generic.xaml` and `SettingsExpander.xaml`

## Going back to the package

Once a `CommunityToolkit.WinUI.Controls.SettingsControls` build targets Uno 7:

1. Delete this folder.
2. Delete the `SettingsControlsCopy_*` test in `tests/DailyPlants.Tests/Packaging/AppResourcesTests.cs`.
3. In `Directory.Packages.props`, replace the comment pointing here with
   `<PackageVersion Include="CommunityToolkit.WinUI.Controls.SettingsControls" Version="..." />`.
4. In `DailyPlants.csproj`, add `<PackageReference Include="CommunityToolkit.WinUI.Controls.SettingsControls" />`.
5. In `App.xaml`, change the merged dictionary to
   `ms-appx:///CommunityToolkit.WinUI.Controls.SettingsControls/Themes/Generic.xaml`.
