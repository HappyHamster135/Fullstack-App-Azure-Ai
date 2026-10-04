using System.Net;
using System.Net.Http.Json;
using SubTracker.Api.Dtos.Dashboard;
using SubTracker.Api.Entities;
using SubTracker.Api.Tests.Infrastructure;

namespace SubTracker.Api.Tests;

/// <summary>
/// Baslinje för den befintliga applikationen: visar att testmiljön (JWT + SQLite) fungerar
/// innan någon ny funktion byggs ovanpå den.
/// </summary>
public class DashboardEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Dashboard_RequiresAuthentication()
    {
        var response = await factory.CreateClient().GetAsync("/api/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Registration_CreatesDefaultCategories()
    {
        var user = await ApiTestUser.RegisterAsync(factory);

        Assert.Equal(6, user.Categories.Count);
    }

    [Fact]
    public async Task Dashboard_SumsMonthlyCostOfActiveSubscriptionsOnly()
    {
        var user = await ApiTestUser.RegisterAsync(factory);
        await user.AddSubscriptionAsync("Netflix", 120m, BillingInterval.Monthly, Today.AddDays(5));
        await user.AddSubscriptionAsync("Antivirus", 600m, BillingInterval.Yearly, Today.AddDays(40));
        await user.AddSubscriptionAsync("Pausad", 999m, BillingInterval.Monthly, Today.AddDays(5), isActive: false);

        var dashboard = await user.Client.GetFromJsonAsync<DashboardResponse>("/api/dashboard");

        Assert.NotNull(dashboard);
        Assert.Equal(2, dashboard.ActiveSubscriptions);
        Assert.Equal(170m, dashboard.TotalMonthlyCost);
        Assert.Equal(2040m, dashboard.TotalYearlyCost);
    }

    [Fact]
    public async Task Subscription_OfAnotherUser_IsNotFound()
    {
        var owner = await ApiTestUser.RegisterAsync(factory);
        var intruder = await ApiTestUser.RegisterAsync(factory);
        var subscription = await owner.AddSubscriptionAsync("Hemlig", 99m, BillingInterval.Monthly, Today.AddDays(3));

        var response = await intruder.Client.GetAsync($"/api/subscriptions/{subscription.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
