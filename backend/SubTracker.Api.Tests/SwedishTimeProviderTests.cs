using Microsoft.Extensions.DependencyInjection;
using SubTracker.Api.Common;
using SubTracker.Api.Tests.Infrastructure;

namespace SubTracker.Api.Tests;

/// <summary>
/// De övriga testerna byter klockan mot <see cref="TestClock"/>. Här testas den riktiga klockan:
/// att den räknar svensk tid och att Program.cs registrerar den.
/// </summary>
public class SwedishTimeProviderTests
{
    private sealed class ProductionClockFactory : ApiFactory
    {
        protected override bool ReplaceClock => false;
    }

    [Theory]
    [InlineData("2027-01-31T22:30:00Z", "2027-01-31")] // 23:30 vintertid
    [InlineData("2027-01-31T23:30:00Z", "2027-02-01")] // 00:30 vintertid
    [InlineData("2027-06-30T21:30:00Z", "2027-06-30")] // 23:30 sommartid
    [InlineData("2027-06-30T22:30:00Z", "2027-07-01")] // 00:30 sommartid
    public void GetToday_IsTheSwedishDate(string utc, string expectedDate)
    {
        var provider = new FixedUtcSwedishTimeProvider(DateTimeOffset.Parse(utc));

        Assert.Equal(DateOnly.Parse(expectedDate), provider.GetToday());
    }

    [Fact]
    public void LocalTimeZone_IsStockholm_WhenTheTimeZoneDatabaseExists()
    {
        var provider = new SwedishTimeProvider();

        Assert.Equal("Europe/Stockholm", provider.LocalTimeZone.Id);
        Assert.False(provider.UsesFallbackZone);
    }

    [Fact]
    public void ProgramRegistersTheSwedishTimeProvider()
    {
        using var factory = new ProductionClockFactory();

        var timeProvider = factory.Services.GetRequiredService<TimeProvider>();

        Assert.IsType<SwedishTimeProvider>(timeProvider);
    }

    // Den riktiga SwedishTimeProvider med fast UTC-tid, så att tidszonslogiken testas utan systemklockan.
    private sealed class FixedUtcSwedishTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private readonly SwedishTimeProvider _swedish = new();

        public override TimeZoneInfo LocalTimeZone => _swedish.LocalTimeZone;

        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
