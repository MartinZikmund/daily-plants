using DailyPlants.Services.Tips;
using DailyPlants.Tests.TestDoubles;

namespace DailyPlants.Tests.Services;

/// <summary>
/// Tips are remembered in one comma-separated preference rather than a flag per tip, so the
/// parsing has to survive whatever is already in that value - including ids written by a
/// build this one has never heard of.
/// </summary>
[TestClass]
public class TipServiceTests
{
    private FakeAppPreferences _prefs = null!;
    private TipService _tips = null!;

    [TestInitialize]
    public void Initialize()
    {
        _prefs = new FakeAppPreferences();
        _tips = new TipService(_prefs);
    }

    [TestMethod]
    public void ShouldShow_TipNeverSeen_ReturnsTrue()
    {
        _tips.ShouldShow(TipId.DiaryLogServing).Should().BeTrue();
    }

    [TestMethod]
    public void ShouldShow_TipAlreadySeen_ReturnsFalse()
    {
        _tips.MarkSeen(TipId.DiaryLogServing);

        _tips.ShouldShow(TipId.DiaryLogServing).Should().BeFalse();
    }

    [TestMethod]
    public void MarkSeen_DoesNotAffectTheOtherTips()
    {
        _tips.MarkSeen(TipId.DiaryLogServing);

        _tips.ShouldShow(TipId.DiaryDayProgress).Should().BeTrue();
        _tips.ShouldShow(TipId.DiaryPastDays).Should().BeTrue();
    }

    [TestMethod]
    public void MarkSeen_WritesTheStableStringId()
    {
        _tips.MarkSeen(TipId.DiaryLogServing);

        _prefs.SeenTips.Should().Contain("diary-log-serving",
            "the enum's ordinal must never reach storage - inserting a member would then "
            + "silently re-point every id a user has already seen");
    }

    [TestMethod]
    public void MarkSeen_SeveralTipsAtOnce_RecordsAllOfThem()
    {
        _tips.MarkSeen(TipId.DiaryLogServing, TipId.DiaryDayProgress);

        _tips.ShouldShow(TipId.DiaryLogServing).Should().BeFalse();
        _tips.ShouldShow(TipId.DiaryDayProgress).Should().BeFalse();
    }

    [TestMethod]
    public void MarkSeen_CalledTwice_DoesNotRepeatTheId()
    {
        _tips.MarkSeen(TipId.DiaryLogServing);
        _tips.MarkSeen(TipId.DiaryLogServing);

        _prefs.SeenTips.Split(',').Should().ContainSingle();
    }

    [TestMethod]
    public void MarkSeen_KeepsIdsThisBuildDoesNotKnow()
    {
        _prefs.SeenTips = "diary-something-newer";

        _tips.MarkSeen(TipId.DiaryLogServing);

        _prefs.SeenTips.Should().Contain("diary-something-newer",
            "dropping an unknown id would replay, on a downgrade, a tip the newer build "
            + "had already shown");
    }

    [TestMethod]
    public void ShouldShow_BlankSegments_AreIgnored()
    {
        _prefs.SeenTips = ",, ,diary-log-serving, ,";

        _tips.ShouldShow(TipId.DiaryLogServing).Should().BeFalse();
        _tips.ShouldShow(TipId.DiaryDayProgress).Should().BeTrue();
    }

    [TestMethod]
    public void Reset_MakesEveryTipEligibleAgain()
    {
        _tips.MarkSeen(TipId.DiaryLogServing, TipId.DiaryDayProgress, TipId.DiaryPastDays);

        _tips.Reset();

        _tips.ShouldShow(TipId.DiaryLogServing).Should().BeTrue();
        _tips.ShouldShow(TipId.DiaryDayProgress).Should().BeTrue();
        _tips.ShouldShow(TipId.DiaryPastDays).Should().BeTrue();
    }

    [TestMethod]
    public void ToStorageId_IsStableForEveryTip()
    {
        TipId.DiaryLogServing.ToStorageId().Should().Be("diary-log-serving");
        TipId.DiaryDayProgress.ToStorageId().Should().Be("diary-day-progress");
        TipId.DiaryPastDays.ToStorageId().Should().Be("diary-past-days");
    }
}
