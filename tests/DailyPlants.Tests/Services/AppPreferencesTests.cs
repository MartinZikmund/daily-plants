using DailyPlants.Tests.TestDoubles;

namespace DailyPlants.Tests.Services;

[TestClass]
public class AppPreferencesTests
{
    [TestMethod]
    public void Defaults_MatchProductionExpectations()
    {
        var prefs = new AppPreferences(new InMemoryPreferences());

        prefs.DailyDozenEnabled.Should().BeTrue();
        prefs.TwentyOneTweaksEnabled.Should().BeFalse();
        prefs.WeightTrackingEnabled.Should().BeFalse();
        prefs.UseMetricUnits.Should().BeTrue();
        prefs.HeightCm.Should().BeNull();
        prefs.GoalWeight.Should().BeNull();
        prefs.ThemePreference.Should().Be(0);
        prefs.Language.Should().BeNull();
        prefs.DisabledItemIds.Should().BeEmpty();
    }

    [TestMethod]
    public void BoolProperties_RoundTrip()
    {
        var prefs = new AppPreferences(new InMemoryPreferences());

        prefs.DailyDozenEnabled = false;
        prefs.TwentyOneTweaksEnabled = true;
        prefs.WeightTrackingEnabled = true;
        prefs.UseMetricUnits = false;

        prefs.DailyDozenEnabled.Should().BeFalse();
        prefs.TwentyOneTweaksEnabled.Should().BeTrue();
        prefs.WeightTrackingEnabled.Should().BeTrue();
        prefs.UseMetricUnits.Should().BeFalse();
    }

    [TestMethod]
    public void HeightCm_NullRoundTripsThroughNanSentinel()
    {
        var prefs = new AppPreferences(new InMemoryPreferences());

        prefs.HeightCm = 170.5;
        prefs.HeightCm.Should().Be(170.5);

        prefs.HeightCm = null;
        prefs.HeightCm.Should().BeNull();
    }

    [TestMethod]
    public void GoalWeight_NullRoundTripsThroughNanSentinel()
    {
        var prefs = new AppPreferences(new InMemoryPreferences());

        prefs.GoalWeight = 70.0;
        prefs.GoalWeight.Should().Be(70.0);

        prefs.GoalWeight = null;
        prefs.GoalWeight.Should().BeNull();
    }

    [TestMethod]
    public void Language_EmptyStringReadsAsNull()
    {
        var backing = new InMemoryPreferences();
        backing.Set("Language", string.Empty);
        var prefs = new AppPreferences(backing);

        prefs.Language.Should().BeNull();
    }

    [TestMethod]
    public void Language_AssigningNullPersistsEmptyString()
    {
        var prefs = new AppPreferences(new InMemoryPreferences());

        prefs.Language = "cs";
        prefs.Language.Should().Be("cs");

        prefs.Language = null;
        prefs.Language.Should().BeNull();
    }

    [TestMethod]
    public void ThemePreference_RoundTrip()
    {
        var prefs = new AppPreferences(new InMemoryPreferences());

        prefs.ThemePreference = 2;

        prefs.ThemePreference.Should().Be(2);
    }

    [TestMethod]
    public void DisabledItemIds_RoundTrip()
    {
        var prefs = new AppPreferences(new InMemoryPreferences());

        prefs.DisabledItemIds = "beans,berries";

        prefs.DisabledItemIds.Should().Be("beans,berries");
    }
}
