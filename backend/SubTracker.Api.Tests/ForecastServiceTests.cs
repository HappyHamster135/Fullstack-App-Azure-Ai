using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SubTracker.Api.Data;
using SubTracker.Api.Entities;
using SubTracker.Api.Services;
using SubTracker.Api.Tests.Infrastructure;

namespace SubTracker.Api.Tests;

/// <summary>
/// Tester som går direkt mot servicen. De skyddar sådant som HTTP-lagret aldrig kommer åt:
/// vakterna mot felanrop från annan kod och att en ren läsfråga inte spårar entiteter.
/// </summary>
public class ForecastServiceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(25)]
    public async Task GetAsync_RejectsMonthsOutsideTheSupportedRange(int months)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IForecastService>();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetAsync("vilken-användare-som-helst", months));
    }

    [Fact]
    public async Task GetAsync_DoesNotTrackTheEntitiesItReads()
    {
        factory.Clock.SetToday(2027, 1, 15);
        var user = await ApiTestUser.RegisterAsync(factory);
        await user.AddSubscriptionAsync("Netflix", 100m, BillingInterval.Monthly, new DateOnly(2027, 1, 20));

        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IForecastService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var forecast = await service.GetAsync(user.User.Id, 6);

        Assert.True(forecast.Total > 0);
        Assert.Empty(db.ChangeTracker.Entries());
    }
}
