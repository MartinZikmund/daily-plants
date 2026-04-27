namespace DailyPlants.Tests.Models;

[TestClass]
public class DailyEntryTests
{
    [TestMethod]
    public void Properties_AreMutable()
    {
        var entry = new DailyEntry
        {
            Date = new DateOnly(2026, 4, 27),
            ItemId = "beans",
            ServingsCompleted = 1
        };

        entry.ServingsCompleted = 3;
        entry.ItemId = "berries";

        entry.ServingsCompleted.Should().Be(3);
        entry.ItemId.Should().Be("berries");
    }

    [TestMethod]
    public void Date_IsDateOnly()
    {
        var date = new DateOnly(2026, 1, 15);
        var entry = new DailyEntry { Date = date, ItemId = "x", ServingsCompleted = 0 };

        entry.Date.Should().Be(date);
        entry.Date.ToString("yyyy-MM-dd").Should().Be("2026-01-15");
    }

    [TestMethod]
    public void Id_DefaultsToZero()
    {
        var entry = new DailyEntry { Date = DateOnly.MinValue, ItemId = "x" };

        entry.Id.Should().Be(0);
    }

    [TestMethod]
    public void ServingsCompleted_DefaultsToZero()
    {
        var entry = new DailyEntry { Date = DateOnly.MinValue, ItemId = "x" };

        entry.ServingsCompleted.Should().Be(0);
    }
}
