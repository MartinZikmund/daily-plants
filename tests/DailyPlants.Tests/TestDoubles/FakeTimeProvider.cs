namespace DailyPlants.Tests.TestDoubles;

/// <summary>
/// Controllable clock for tests that need to cross a date boundary.
/// Local time is UTC so "midnight" is unambiguous.
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public void Advance(TimeSpan by) => _utcNow = _utcNow.Add(by);
}
