using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SubTracker.Api.Dtos.Payments;
using SubTracker.Api.Dtos.Subscriptions;
using SubTracker.Api.Entities;
using SubTracker.Api.Tests.Infrastructure;

namespace SubTracker.Api.Tests;

/// <summary>
/// Datumfälten är användarens indata och styr beräkningar och datumaritmetik. Utan gränser gav ett datum
/// långt fram (9999-12-31) ett 500-fel vid "Markera betald", och ett mycket gammalt datum gjorde beräkningar onödigt dyra.
/// </summary>
public class SubscriptionDateValidationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    private static SubscriptionRequest RequestWith(ApiTestUser user, DateOnly startDate, DateOnly nextPaymentDate) => new()
    {
        Name = "Tjänst",
        Price = 10m,
        BillingInterval = BillingInterval.Monthly,
        StartDate = startDate,
        NextPaymentDate = nextPaymentDate,
        CategoryId = user.Categories[0].Id,
    };

    private static async Task<ValidationProblemDetails> ReadProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!;
    }

    [Theory]
    [InlineData("1999-12-31")]
    [InlineData("0001-01-01")]
    [InlineData("2101-01-01")]
    [InlineData("9999-12-31")]
    public async Task StartDate_OutsideTheSupportedRange_IsRejected(string startDate)
    {
        var user = await ApiTestUser.RegisterAsync(factory);
        var date = DateOnly.Parse(startDate);

        var response = await user.Client.PostAsJsonAsync("/api/subscriptions", RequestWith(user, date, date));

        var problem = await ReadProblemAsync(response);
        Assert.Equal(["Startdatumet måste vara mellan 2000-01-01 och 2100-12-31."], problem.Errors["StartDate"]);
    }

    [Theory]
    [InlineData("1999-12-31")]
    [InlineData("2101-01-01")]
    [InlineData("9999-12-31")]
    public async Task NextPaymentDate_OutsideTheSupportedRange_IsRejected(string nextPaymentDate)
    {
        var user = await ApiTestUser.RegisterAsync(factory);
        var next = DateOnly.Parse(nextPaymentDate);
        var start = next < D(2000, 1, 1) ? next : D(2020, 1, 1);

        var response = await user.Client.PostAsJsonAsync("/api/subscriptions", RequestWith(user, start, next));

        var problem = await ReadProblemAsync(response);
        Assert.Contains("Nästa betalning måste vara mellan 2000-01-01 och 2100-12-31.", problem.Errors["NextPaymentDate"]);
    }

    [Fact]
    public async Task DatesOnTheBoundaries_AreAccepted_AndMarkingPaidAtTheUpperBoundDoesNotCrash()
    {
        var user = await ApiTestUser.RegisterAsync(factory);

        var oldest = await user.Client.PostAsJsonAsync("/api/subscriptions", RequestWith(user, D(2000, 1, 1), D(2000, 1, 1)));
        Assert.Equal(HttpStatusCode.Created, oldest.StatusCode);

        var latest = await user.Client.PostAsJsonAsync("/api/subscriptions", RequestWith(user, D(2100, 12, 31), D(2100, 12, 31)));
        Assert.Equal(HttpStatusCode.Created, latest.StatusCode);
        var subscription = await latest.Content.ReadFromJsonAsync<SubscriptionResponse>();

        // Före rättningen gav ett datum nära årtal 9999 ett 500-fel här. Vid övre gränsen går det att flytta fram.
        var payment = await user.Client.PostAsJsonAsync($"/api/subscriptions/{subscription!.Id}/payments", new PaymentRequest());
        Assert.Equal(HttpStatusCode.Created, payment.StatusCode);
    }

    [Fact]
    public async Task NextPaymentBeforeStart_IsStillRejected()
    {
        var user = await ApiTestUser.RegisterAsync(factory);

        var response = await user.Client.PostAsJsonAsync("/api/subscriptions", RequestWith(user, D(2027, 5, 1), D(2027, 4, 30)));

        var problem = await ReadProblemAsync(response);
        Assert.Equal(["Nästa betalning kan inte vara före startdatumet."], problem.Errors["NextPaymentDate"]);
    }
}
