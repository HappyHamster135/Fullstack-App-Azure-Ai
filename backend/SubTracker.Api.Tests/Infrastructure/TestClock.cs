namespace SubTracker.Api.Tests.Infrastructure;

/// <summary>
/// Klocka som testet styr själv. Servicer som tar <see cref="TimeProvider"/> får då ett fast "idag",
/// så att datumregler (månadsskiften, skottår, förfallna betalningar) kan testas deterministiskt.
/// Lokal tid är svensk tid, som i produktion, så att testerna också kan pröva tiden runt midnatt.
/// </summary>
public sealed class TestClock : TimeProvider
{
    private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

    public override TimeZoneInfo LocalTimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");

    public override DateTimeOffset GetUtcNow() => _utcNow;

    /// <summary>Sätter "idag" till klockan 12:00 UTC det angivna datumet (samma datum i Sverige).</summary>
    public void SetToday(int year, int month, int day) =>
        SetUtc(new DateTimeOffset(year, month, day, 12, 0, 0, TimeSpan.Zero));

    /// <summary>Sätter ett exakt klockslag i UTC, t.ex. strax före svensk midnatt.</summary>
    public void SetUtc(DateTimeOffset utcNow) => _utcNow = utcNow;
}
