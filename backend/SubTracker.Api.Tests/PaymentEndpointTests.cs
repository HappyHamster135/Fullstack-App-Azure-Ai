using System.Net;
using System.Net.Http.Json;
using SubTracker.Api.Dtos.Payments;
using SubTracker.Api.Dtos.Subscriptions;
using SubTracker.Api.Entities;
using SubTracker.Api.Tests.Infrastructure;

namespace SubTracker.Api.Tests;

/// <summary>
/// AI-förslaget skrev om <c>BillingIntervalExtensions.NextDateAfter</c> (befintlig kod) så att den delegerar till den nya
/// <c>AddIntervals</c>. Testerna visar att "Markera betald" fortfarande flyttar fram datumet precis som förut.
/// </summary>
public class PaymentEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData(BillingInterval.Weekly, "2027-03-17")]
    [InlineData(BillingInterval.Monthly, "2027-04-10")]
    [InlineData(BillingInterval.Quarterly, "2027-06-10")]
    [InlineData(BillingInterval.Yearly, "2028-03-10")]
    public async Task RegisterPayment_AdvancesNextPaymentDateByOneInterval(BillingInterval interval, string expectedNext)
    {
        var user = await ApiTestUser.RegisterAsync(factory);
        var subscription = await user.AddSubscriptionAsync("Tjänst", 100m, interval, new DateOnly(2027, 3, 10));

        var response = await user.Client.PostAsJsonAsync($"/api/subscriptions/{subscription.Id}/payments", new PaymentRequest());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var updated = await user.Client.GetFromJsonAsync<SubscriptionResponse>($"/api/subscriptions/{subscription.Id}");
        Assert.Equal(DateOnly.Parse(expectedNext), updated!.NextPaymentDate);
    }
}
