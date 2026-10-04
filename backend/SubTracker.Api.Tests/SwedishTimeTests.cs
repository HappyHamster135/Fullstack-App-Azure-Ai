using System.Net.Http.Json;
using SubTracker.Api.Dtos.Dashboard;
using SubTracker.Api.Dtos.Payments;
using SubTracker.Api.Entities;
using SubTracker.Api.Tests.Infrastructure;

namespace SubTracker.Api.Tests;

/// <summary>
/// Appen är svensk, så "idag" ska vara svensk tid även när servern går i UTC. Alla tester här sätter klockan
/// till 23:30 UTC den 31 januari 2027. Då är det redan 00:30 den 1 februari i Stockholm.
/// Klassen har en egen fabrik och klocka så att övriga tester inte påverkas.
/// </summary>
public class SwedishTimeTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly DateTimeOffset JustAfterSwedishMidnight = new(2027, 1, 31, 23, 30, 0, TimeSpan.Zero);

    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    [Fact]
    public async Task Dashboard_UpcomingWindow_StartsFromTheSwedishDate()
    {
        factory.Clock.SetUtc(JustAfterSwedishMidnight);
        var user = await ApiTestUser.RegisterAsync(factory);

        // Svensk "idag" är 1 februari, så 30-dagarsfönstret slutar den 3 mars. Med UTC hade det slutat den 2 mars.
        await user.AddSubscriptionAsync("Sista dagen i fönstret", 99m, BillingInterval.Monthly, D(2027, 3, 3));
        await user.AddSubscriptionAsync("Dagen efter fönstret", 99m, BillingInterval.Monthly, D(2027, 3, 4));

        var dashboard = await user.Client.GetFromJsonAsync<DashboardResponse>("/api/dashboard");

        Assert.Equal(["Sista dagen i fönstret"], dashboard!.UpcomingPayments.Select(p => p.Name));
    }

    [Fact]
    public async Task Dashboard_PaymentHistory_EndsInTheSwedishCurrentMonth()
    {
        factory.Clock.SetUtc(JustAfterSwedishMidnight);
        var user = await ApiTestUser.RegisterAsync(factory);

        var dashboard = await user.Client.GetFromJsonAsync<DashboardResponse>("/api/dashboard");

        var last = dashboard!.PaymentsByMonth.Last();
        Assert.Equal((2027, 2), (last.Year, last.Month));
    }

    [Fact]
    public async Task RegisterPayment_WithoutDate_UsesTheSwedishDate()
    {
        factory.Clock.SetUtc(JustAfterSwedishMidnight);
        var user = await ApiTestUser.RegisterAsync(factory);
        var subscription = await user.AddSubscriptionAsync("Netflix", 100m, BillingInterval.Monthly, D(2027, 1, 20));

        var response = await user.Client.PostAsJsonAsync($"/api/subscriptions/{subscription.Id}/payments", new PaymentRequest());
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>();

        Assert.Equal(D(2027, 2, 1), payment!.PaidOn);
    }
}
