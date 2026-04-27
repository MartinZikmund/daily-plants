namespace DailyPlants.Tests.Models;

[TestClass]
public class WeightEntryTests
{
    [TestMethod]
    public void Properties_RoundTrip()
    {
        var entry = new WeightEntry
        {
            Date = new DateOnly(2026, 4, 27),
            Weight = 72.5,
            Notes = "after run"
        };

        entry.Date.Should().Be(new DateOnly(2026, 4, 27));
        entry.Weight.Should().Be(72.5);
        entry.Notes.Should().Be("after run");
    }

    [TestMethod]
    public void Notes_AreOptional()
    {
        var entry = new WeightEntry { Date = DateOnly.MinValue, Weight = 70 };

        entry.Notes.Should().BeNull();
    }

    [TestMethod]
    public void Weight_AcceptsZero()
    {
        var entry = new WeightEntry { Date = DateOnly.MinValue, Weight = 0 };

        entry.Weight.Should().Be(0);
    }
}
