using DailyPlants.Build;

namespace DailyPlants.Tests.DevAssets;

[TestClass]
public sealed class DevBadgePlacementTests
{
    [TestMethod]
    public void Place_NoMask_IsFlushBottomCentre()
    {
        BadgeBox badge = DevBadge.Place(0, 0, 100, 100, DevBadgeMask.None, 1, BadgeLabel.Dev, out bool fits);

        fits.Should().BeTrue();
        CentreX(badge).Should().BeApproximately(50, 1e-9);
        badge.Bottom.Should().BeApproximately(100, 1e-9);
        badge.Height.Should().BeApproximately(28, 1e-9);
        badge.Radius.Should().BeApproximately(28 * 4 / 17.3, 1e-9);
    }

    [TestMethod]
    public void Place_DevLabel_WidthIsTextAdvancePlusPadding()
    {
        BadgeBox badge = DevBadge.Place(0, 0, 100, 100, DevBadgeMask.None, 1, BadgeLabel.Dev, out _);

        // In-app proportions at 28% height: text 28 * 10 / 17.3 em, "DEV" advance 3892 / 2048 em, padding 28 * 6 / 17.3.
        badge.Width.Should().BeApproximately(50.1798, 1e-3);
    }

    [TestMethod]
    public void Place_CiLabel_HugsItsShorterText()
    {
        BadgeBox badge = DevBadge.Place(0, 0, 100, 100, DevBadgeMask.None, 1, BadgeLabel.CI, out _);

        // Same proportions, "CI" advance 1889 / 2048 em.
        badge.Width.Should().BeApproximately(34.3504, 1e-3);
        CentreX(badge).Should().BeApproximately(50, 1e-9);
    }

    [TestMethod]
    public void Place_OffsetViewBox_AnchorsToViewBoxBottomCentre()
    {
        BadgeBox badge = DevBadge.Place(-10, -10, 120, 120, DevBadgeMask.None, 1, BadgeLabel.Dev, out _);

        CentreX(badge).Should().BeApproximately(50, 1e-9);
        badge.Bottom.Should().BeApproximately(110, 1e-9);
        badge.Height.Should().BeApproximately(33.6, 1e-9);
    }

    [TestMethod]
    public void Place_NonSquareViewBox_SizesFromShorterSide()
    {
        BadgeBox badge = DevBadge.Place(0, 0, 200, 100, DevBadgeMask.None, 1, BadgeLabel.Dev, out _);

        badge.Height.Should().BeApproximately(28, 1e-9);
        badge.Bottom.Should().BeApproximately(100, 1e-9);
        CentreX(badge).Should().BeApproximately(100, 1e-9);
    }

    [TestMethod]
    [DataRow(0.6)]
    [DataRow(0.65)]
    [DataRow(0.8)]
    public void Place_AndroidAdaptive_StaysInsideSafeZone(double scale)
    {
        BadgeBox badge = DevBadge.Place(0, 0, 100, 100, DevBadgeMask.AndroidAdaptive, scale, BadgeLabel.Dev, out bool fits);

        fits.Should().BeTrue();
        FarthestReach(badge).Should().BeLessThanOrEqualTo(100 * (33.0 / 108.0) / scale);
    }

    [TestMethod]
    [DataRow(0.85)]
    [DataRow(1.0)]
    public void Place_AndroidSplash_StaysInsideMeasuredSplashCircle(double scale)
    {
        BadgeBox badge = DevBadge.Place(0, 0, 100, 100, DevBadgeMask.AndroidSplash, scale, BadgeLabel.Dev, out bool fits);

        fits.Should().BeTrue();
        FarthestReach(badge).Should().BeLessThanOrEqualTo(100 * 0.484 / scale);
    }

    [TestMethod]
    public void Place_AndroidSplashAtTemplateScale_NeedsNoLift()
    {
        BadgeBox flush = DevBadge.Place(0, 0, 100, 100, DevBadgeMask.None, 1, BadgeLabel.Dev, out _);

        BadgeBox splash = DevBadge.Place(0, 0, 100, 100, DevBadgeMask.AndroidSplash, 0.85, BadgeLabel.Dev, out _);

        splash.Y.Should().Be(flush.Y);
    }

    [TestMethod]
    [DataRow(1.0)]
    [DataRow(0.8)]
    public void Place_IosIcon_StaysInsideRoundedSquare(double scale)
    {
        BadgeBox badge = DevBadge.Place(0, 0, 100, 100, DevBadgeMask.IosIcon, scale, BadgeLabel.Dev, out bool fits);

        fits.Should().BeTrue();
        OutlinePoints(badge).Should().OnlyContain(p => InsideIosIcon(p.X, p.Y, 50 / scale, 22.37 / scale));
    }

    [TestMethod]
    [DataRow(DevBadgeMask.AndroidAdaptive, 0.6)]
    [DataRow(DevBadgeMask.AndroidSplash, 1.0)]
    [DataRow(DevBadgeMask.IosIcon, 1.0)]
    public void Place_MaskClipsFlushBadge_LiftsItStraightUp(DevBadgeMask mask, double scale)
    {
        BadgeBox flush = DevBadge.Place(0, 0, 100, 100, DevBadgeMask.None, 1, BadgeLabel.Dev, out _);

        BadgeBox lifted = DevBadge.Place(0, 0, 100, 100, mask, scale, BadgeLabel.Dev, out _);

        lifted.X.Should().BeApproximately(flush.X, 1e-9);
        lifted.Y.Should().BeLessThan(flush.Y);
    }

    [TestMethod]
    public void Place_MaskSmallerThanBadge_ReportsNotFitting()
    {
        DevBadge.Place(0, 0, 100, 100, DevBadgeMask.AndroidAdaptive, 3.0, BadgeLabel.Dev, out bool fits);

        fits.Should().BeFalse();
    }

    [TestMethod]
    public void IsVisible_NoMask_IsAlwaysTrue()
    {
        DevBadge.IsVisible(new BadgeBox(-1, -1, 3, 3, 0), DevBadgeMask.None, 1).Should().BeTrue();
    }

    [TestMethod]
    [DataRow(DevBadgeMask.AndroidAdaptive)]
    [DataRow(DevBadgeMask.AndroidSplash)]
    [DataRow(DevBadgeMask.IosIcon)]
    public void IsVisible_SmallBadgeAtCentre_IsTrue(DevBadgeMask mask)
    {
        DevBadge.IsVisible(new BadgeBox(0.45, 0.45, 0.1, 0.1, 0.01), mask, 1).Should().BeTrue();
    }

    [TestMethod]
    [DataRow(DevBadgeMask.AndroidAdaptive)]
    [DataRow(DevBadgeMask.AndroidSplash)]
    [DataRow(DevBadgeMask.IosIcon)]
    public void IsVisible_SquareBadgeInCorner_IsFalse(DevBadgeMask mask)
    {
        DevBadge.IsVisible(new BadgeBox(0.9, 0, 0.1, 0.1, 0), mask, 1).Should().BeFalse();
    }

    static double CentreX(BadgeBox b) => (b.X + b.Right) / 2;

    static (double X, double Y)[] CornerCentres(BadgeBox b) =>
    [
        (b.X + b.Radius, b.Y + b.Radius),
        (b.Right - b.Radius, b.Y + b.Radius),
        (b.X + b.Radius, b.Bottom - b.Radius),
        (b.Right - b.Radius, b.Bottom - b.Radius),
    ];

    // Farthest point of the rounded badge from the image centre: a corner disc's centre distance plus its radius.
    static double FarthestReach(BadgeBox b) => CornerCentres(b).Max(c => Distance(c.X - 50, c.Y - 50) + b.Radius);

    // Points around the badge's rounded outline; the straight edges between them lie inside any convex mask that holds these.
    static IEnumerable<(double X, double Y)> OutlinePoints(BadgeBox b) =>
        CornerCentres(b).SelectMany(c => Enumerable.Range(0, 72).Select(i =>
            (c.X + b.Radius * Math.Cos(i * Math.PI / 36), c.Y + b.Radius * Math.Sin(i * Math.PI / 36))));

    static bool InsideIosIcon(double x, double y, double half, double corner)
    {
        double dx = Math.Abs(x - 50);
        double dy = Math.Abs(y - 50);
        double straight = half - corner;
        return dx <= half && dy <= half && (dx <= straight || dy <= straight || Distance(dx - straight, dy - straight) <= corner);
    }

    static double Distance(double dx, double dy) => Math.Sqrt(dx * dx + dy * dy);
}
