using DailyPlants.Build;
using Microsoft.Build.Utilities;

namespace DailyPlants.Tests.DevAssets;

[TestClass]
public sealed class DevAssetPlacementTests
{
    [TestMethod]
    [DataRow(true, "android", DevBadgeMask.AndroidAdaptive)]
    [DataRow(false, "android", DevBadgeMask.AndroidSplash)]
    [DataRow(true, "ios", DevBadgeMask.IosIcon)]
    [DataRow(false, "ios", DevBadgeMask.None)]
    [DataRow(true, "tvos", DevBadgeMask.TvIcon)]
    [DataRow(false, "tvos", DevBadgeMask.None)]
    [DataRow(true, "windows", DevBadgeMask.None)]
    [DataRow(true, "browserwasm", DevBadgeMask.None)]
    [DataRow(true, "", DevBadgeMask.None)]
    public void Resolve_PlatformAndKind_PicksMask(bool isIcon, string platform, DevBadgeMask expected)
    {
        DevAssetPlacement.Resolve(isIcon, platform, new TaskItem("a.svg")).Mask.Should().Be(expected);
    }

    [TestMethod]
    public void Resolve_IconWithForegroundScale_UsesIt()
    {
        TaskItem icon = new("a.svg");
        icon.SetMetadata("ForegroundScale", "0.6");

        DevAssetPlacement.Resolve(true, "android", icon).Scale.Should().Be(0.6);
    }

    [TestMethod]
    public void Resolve_IconWithPlatformOverride_OverrideWins()
    {
        TaskItem icon = new("a.svg");
        icon.SetMetadata("ForegroundScale", "1");
        icon.SetMetadata("AndroidForegroundScale", "0.65");

        DevAssetPlacement.Resolve(true, "android", icon).Scale.Should().Be(0.65);
    }

    [TestMethod]
    public void Resolve_SplashWithScaleAndAndroidScale_AndroidScaleWins()
    {
        TaskItem splash = new("a.svg");
        splash.SetMetadata("Scale", "0.85");
        splash.SetMetadata("AndroidScale", "0.7");

        DevAssetPlacement.Resolve(false, "android", splash).Scale.Should().Be(0.7);
        DevAssetPlacement.Resolve(false, "ios", splash).Scale.Should().Be(0.85);
    }

    [TestMethod]
    public void Resolve_IconWithTvOSOverride_OverrideWins()
    {
        TaskItem icon = new("a.svg");
        icon.SetMetadata("ForegroundScale", "1");
        icon.SetMetadata("TvOSForegroundScale", "0.7");

        DevAssetPlacement.Resolve(true, "tvos", icon).Scale.Should().Be(0.7);
    }

    [TestMethod]
    public void Place_TvIcon_StaysInsideTheParallaxSafeZone()
    {
        // Apple's safe zone is 222 of the icon's 240 height; an icon is fitted to the frame's height.
        const double safeMargin = (240.0 - 222.0) / 2 / 240.0;

        var badge = DevBadge.Place(0, 0, 100, 100, DevBadgeMask.TvIcon, 1.0, BadgeLabel.Dev, out bool fits);

        fits.Should().BeTrue();
        badge.Bottom.Should().BeLessThanOrEqualTo(100 * (1 - safeMargin));
        badge.Y.Should().BeGreaterThanOrEqualTo(100 * safeMargin);
    }

    [TestMethod]
    public void Resolve_NoScaleMetadata_DefaultsToOne()
    {
        DevAssetPlacement.Resolve(true, "ios", new TaskItem("a.svg")).Scale.Should().Be(1.0);
    }
}
