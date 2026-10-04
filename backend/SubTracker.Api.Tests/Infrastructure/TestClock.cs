namespace SubTracker.Api.Tests.Infrastructure;

/// <summary>
/// Klocka som testet styr själv. Servicer som tar <see cref="TimeProvider"/> får då ett fast "idag",
/// så att datumregler (månadsskiften, skottår, förfallna betalningar) kan testas deterministiskt.
/// </summary>
public sealed class TestClock : TimeProvider
{
    private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    /// <summary>Sätter "idag" till klockan 12:00 UTC det angivna datumet.</summary>
    public void SetToday(int year, int month, int day) =>
        _utcNow = new DateTimeOffset(year, month, day, 12, 0, 0, TimeSpan.Zero);
}
