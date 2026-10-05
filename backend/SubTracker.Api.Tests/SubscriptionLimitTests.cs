using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SubTracker.Api.Dtos.Subscriptions;
using SubTracker.Api.Entities;
using SubTracker.Api.Tests.Infrastructure;

namespace SubTracker.Api.Tests;

/// <summary>
/// Ett konto får inte skapa obegränsat många prenumerationer. Registreringen är öppen och prognosens arbete
/// växer med antalet prenumerationer, så taket är det som begränsar värsta fallet.
/// </summary>
public class SubscriptionLimitTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const int Limit = 200;

    private static readonly DateOnly NextPayment = new(2027, 3, 15);

    private static SubscriptionRequest RequestFor(ApiTestUser user, string name) => new()
    {
        Name = name,
        Price = 10m,
        BillingInterval = BillingInterval.Monthly,
        StartDate = NextPayment.AddYears(-1),
        NextPaymentDate = NextPayment,
        CategoryId = user.Categories[0].Id,
    };

    [Fact]
    public async Task CreatingMoreThanTheLimit_IsRejectedWithAClearMessage_PerUser_AndDeletingFreesASlot()
    {
        var user = await ApiTestUser.RegisterAsync(factory);
        var first = await user.AddSubscriptionAsync("Tjänst 0", 10m, BillingInterval.Monthly, NextPayment);
        for (var i = 1; i < Limit; i++)
        {
            await user.AddSubscriptionAsync($"Tjänst {i}", 10m, BillingInterval.Monthly, NextPayment);
        }

        var rejected = await user.Client.PostAsJsonAsync("/api/subscriptions", RequestFor(user, "En för mycket"));

        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        var problem = await rejected.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Du kan ha högst 200 prenumerationer.", problem!.Detail);

        // Taket gäller per användare.
        var other = await ApiTestUser.RegisterAsync(factory);
        await other.AddSubscriptionAsync("Någon annans", 10m, BillingInterval.Monthly, NextPayment);

        // Tar man bort en prenumeration går det att skapa en ny.
        var deleted = await user.Client.DeleteAsync($"/api/subscriptions/{first.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var accepted = await user.Client.PostAsJsonAsync("/api/subscriptions", RequestFor(user, "Ersättare"));
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }
}
