using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SubTracker.Api.Dtos.Forecast;
using SubTracker.Api.Entities;
using SubTracker.Api.Tests.Infrastructure;

namespace SubTracker.Api.Tests;

/// <summary>
/// Tester av GET /api/forecast genom hela kedjan (JWT → controller → service → SQLite).
/// Varje test sätter "idag" själv med <see cref="TestClock"/>, så att månadsskiften och skottår går att testa.
/// </summary>
public class ForecastEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    private async Task<ApiTestUser> RegisterOnAsync(int year, int month, int day)
    {
        factory.Clock.SetToday(year, month, day);
        return await ApiTestUser.RegisterAsync(factory);
    }

    private static async Task<ForecastResponse> GetForecastAsync(ApiTestUser user, int? months = null)
    {
        var url = months is null ? "/api/forecast" : $"/api/forecast?months={months}";

        return await user.Client.GetFromJsonAsync<ForecastResponse>(url)
            ?? throw new InvalidOperationException("Prognosen returnerade inget svar.");
    }

    private static List<DateOnly> PaymentDates(ForecastResponse forecast, string name) =>
        forecast.Months.SelectMany(m => m.Payments).Where(p => p.Name == name).Select(p => p.Date).ToList();


    //-----------------
    //-----Åtkomst
    //-----------------

    [Fact]
    public async Task Forecast_RequiresAuthentication()
    {
        var response = await factory.CreateClient().GetAsync("/api/forecast");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Forecast_ContainsOnlyTheCallersOwnSubscriptions()
    {
        var owner = await RegisterOnAsync(2027, 1, 15);
        var other = await ApiTestUser.RegisterAsync(factory);
        await owner.AddSubscriptionAsync("Ägarens", 100m, BillingInterval.Monthly, D(2027, 1, 20));
        await other.AddSubscriptionAsync("Andras", 55m, BillingInterval.Monthly, D(2027, 1, 21));

        var ownerForecast = await GetForecastAsync(owner);
        var otherForecast = await GetForecastAsync(other);

        Assert.All(ownerForecast.Months.SelectMany(m => m.Payments), p => Assert.Equal("Ägarens", p.Name));
        Assert.All(otherForecast.Months.SelectMany(m => m.Payments), p => Assert.Equal("Andras", p.Name));
        Assert.Equal(600m, ownerForecast.Total);
        Assert.Equal(330m, otherForecast.Total);
    }


    //-------------
    //-----Perioden
    //-------------

    [Fact]
    public async Task Forecast_DefaultsToSixMonthsStartingWithTheCurrentMonth_AcrossNewYear()
    {
        var user = await RegisterOnAsync(2026, 11, 20);

        var forecast = await GetForecastAsync(user);

        var expected = new[] { (2026, 11), (2026, 12), (2027, 1), (2027, 2), (2027, 3), (2027, 4) };
        Assert.Equal(expected, forecast.Months.Select(m => (m.Year, m.Month)));
        Assert.Equal(0m, forecast.Total);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(24)]
    public async Task Forecast_ReturnsTheRequestedNumberOfMonths(int months)
    {
        var user = await RegisterOnAsync(2027, 1, 15);

        var forecast = await GetForecastAsync(user, months);

        Assert.Equal(months, forecast.Months.Count);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("25")]
    [InlineData("-1")]
    public async Task Forecast_RejectsMonthsOutsideOneToTwentyFour_WithTheProjectsErrorFormat(string months)
    {
        var user = await RegisterOnAsync(2027, 1, 15);

        var response = await user.Client.GetAsync($"/api/forecast?months={months}");

        // Samma format som övriga DTO-valideringar: nyckeln är egenskapens namn (PascalCase) och texten är på svenska.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["Antal månader måste vara mellan 1 och 24."], problem!.Errors["Months"]);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("7.5")]
    [InlineData("")]
    public async Task Forecast_RejectsNonNumericMonths(string months)
    {
        var user = await RegisterOnAsync(2027, 1, 15);

        var response = await user.Client.GetAsync($"/api/forecast?months={months}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("Months", problem!.Errors.Keys);
    }

    [Fact]
    public async Task Forecast_UsesSwedishTimeForToday_JustAfterMidnightInWinter()
    {
        // 23:30 UTC den 31 januari är 00:30 den 1 februari i Stockholm. En svensk användare ser redan februari.
        factory.Clock.SetUtc(new DateTimeOffset(2027, 1, 31, 23, 30, 0, TimeSpan.Zero));
        var user = await ApiTestUser.RegisterAsync(factory);
        await user.AddSubscriptionAsync("Igår", 100m, BillingInterval.Monthly, D(2027, 1, 31));
        await user.AddSubscriptionAsync("Idag", 100m, BillingInterval.Monthly, D(2027, 2, 1));

        var forecast = await GetForecastAsync(user);

        Assert.Equal((2027, 2), (forecast.Months[0].Year, forecast.Months[0].Month));
        Assert.Equal((2027, 7), (forecast.Months[5].Year, forecast.Months[5].Month));
        Assert.Equal(
            [D(2027, 2, 1), D(2027, 3, 1), D(2027, 4, 1), D(2027, 5, 1), D(2027, 6, 1), D(2027, 7, 1)],
            PaymentDates(forecast, "Idag"));
        Assert.Equal(
            [D(2027, 2, 28), D(2027, 3, 31), D(2027, 4, 30), D(2027, 5, 31), D(2027, 6, 30), D(2027, 7, 31)],
            PaymentDates(forecast, "Igår"));
    }

    [Fact]
    public async Task Forecast_UsesSwedishSummerTime()
    {
        // Sommartid är UTC+2: 22:30 UTC den 30 juni är 00:30 den 1 juli i Stockholm.
        factory.Clock.SetUtc(new DateTimeOffset(2027, 6, 30, 22, 30, 0, TimeSpan.Zero));
        var user = await ApiTestUser.RegisterAsync(factory);

        var forecast = await GetForecastAsync(user);

        Assert.Equal((2027, 7), (forecast.Months[0].Year, forecast.Months[0].Month));
    }

    [Fact]
    public async Task Forecast_StillShowsTheOldMonth_BeforeSwedishMidnight()
    {
        // 22:30 UTC den 31 januari är 23:30 i Stockholm, alltså fortfarande januari.
        factory.Clock.SetUtc(new DateTimeOffset(2027, 1, 31, 22, 30, 0, TimeSpan.Zero));
        var user = await ApiTestUser.RegisterAsync(factory);
        await user.AddSubscriptionAsync("Idag", 100m, BillingInterval.Monthly, D(2027, 1, 31));

        var forecast = await GetForecastAsync(user);

        Assert.Equal((2027, 1), (forecast.Months[0].Year, forecast.Months[0].Month));
        Assert.Equal(D(2027, 1, 31), PaymentDates(forecast, "Idag")[0]);
    }

    [Fact]
    public async Task Forecast_CountsPaymentsFromTodayOnly_ButKeepsTheBillingCycle()
    {
        var user = await RegisterOnAsync(2027, 1, 15);
        await user.AddSubscriptionAsync("Förfallen", 100m, BillingInterval.Monthly, D(2027, 1, 10));
        await user.AddSubscriptionAsync("Igår", 100m, BillingInterval.Monthly, D(2027, 1, 14));
        await user.AddSubscriptionAsync("Idag", 100m, BillingInterval.Monthly, D(2027, 1, 15));

        var forecast = await GetForecastAsync(user);

        // Januari: bara det som dras idag eller senare.
        Assert.Equal(["Idag"], forecast.Months[0].Payments.Select(p => p.Name));
        Assert.Equal(100m, forecast.Months[0].Total);

        // En förfallen prenumeration fortsätter i sin takt från nästa månad.
        Assert.Equal(
            [D(2027, 2, 10), D(2027, 3, 10), D(2027, 4, 10), D(2027, 5, 10), D(2027, 6, 10)],
            PaymentDates(forecast, "Förfallen"));
        Assert.Equal(6, PaymentDates(forecast, "Idag").Count);
    }


    //-------------------
    //-----Betalningsdatum
    //-------------------

    [Fact]
    public async Task Forecast_MonthlySubscription_IsChargedOnItsDateEveryMonth()
    {
        var user = await RegisterOnAsync(2027, 1, 15);
        await user.AddSubscriptionAsync("Netflix", 100m, BillingInterval.Monthly, D(2027, 1, 20));

        var forecast = await GetForecastAsync(user);

        Assert.Equal(
            [D(2027, 1, 20), D(2027, 2, 20), D(2027, 3, 20), D(2027, 4, 20), D(2027, 5, 20), D(2027, 6, 20)],
            PaymentDates(forecast, "Netflix"));
        Assert.All(forecast.Months, m => Assert.Equal(100m, m.Total));
        Assert.Equal(600m, forecast.Total);
    }

    [Fact]
    public async Task Forecast_MonthEnd_DoesNotDriftAfterShortMonths()
    {
        var user = await RegisterOnAsync(2027, 1, 15);
        await user.AddSubscriptionAsync("Månadsskifte", 100m, BillingInterval.Monthly, D(2027, 1, 31));

        var forecast = await GetForecastAsync(user);

        Assert.Equal(
            [D(2027, 1, 31), D(2027, 2, 28), D(2027, 3, 31), D(2027, 4, 30), D(2027, 5, 31), D(2027, 6, 30)],
            PaymentDates(forecast, "Månadsskifte"));
    }

    [Fact]
    public async Task Forecast_YearlyLeapDay_FallsBackToFebruary28InNonLeapYears()
    {
        var user = await RegisterOnAsync(2028, 1, 10);
        await user.AddSubscriptionAsync("Skottdag", 1200m, BillingInterval.Yearly, D(2028, 2, 29));

        var forecast = await GetForecastAsync(user, 24);

        Assert.Equal([D(2028, 2, 29), D(2029, 2, 28)], PaymentDates(forecast, "Skottdag"));
        Assert.Equal(2400m, forecast.Total);
    }

    [Fact]
    public async Task Forecast_WeeklySubscription_IsChargedEveryWeek()
    {
        var user = await RegisterOnAsync(2027, 1, 15);
        await user.AddSubscriptionAsync("Matkasse", 10m, BillingInterval.Weekly, D(2027, 1, 18));

        var forecast = await GetForecastAsync(user, 2);

        Assert.Equal(
            [D(2027, 1, 18), D(2027, 1, 25), D(2027, 2, 1), D(2027, 2, 8), D(2027, 2, 15), D(2027, 2, 22)],
            PaymentDates(forecast, "Matkasse"));
        Assert.Equal(20m, forecast.Months[0].Total);
        Assert.Equal(40m, forecast.Months[1].Total);
        Assert.Equal(60m, forecast.Total);
    }

    [Fact]
    public async Task Forecast_QuarterlySubscription_IsChargedEveryThirdMonth()
    {
        var user = await RegisterOnAsync(2027, 1, 15);
        await user.AddSubscriptionAsync("Tidskrift", 449m, BillingInterval.Quarterly, D(2027, 2, 1));

        var forecast = await GetForecastAsync(user);

        Assert.Equal([D(2027, 2, 1), D(2027, 5, 1)], PaymentDates(forecast, "Tidskrift"));
    }

    [Fact]
    public async Task Forecast_IncludesAPaymentOnTheLastDayOfThePeriod()
    {
        // Gränsfall som AI-granskaren hittade: databasfiltret ska behålla en prenumeration vars nästa betalning
        // är exakt periodens sista dag.
        var user = await RegisterOnAsync(2027, 1, 15);
        await user.AddSubscriptionAsync("Sista dagen", 100m, BillingInterval.Monthly, D(2027, 6, 30));

        var forecast = await GetForecastAsync(user, 6);

        Assert.Equal([D(2027, 6, 30)], PaymentDates(forecast, "Sista dagen"));
    }

    [Fact]
    public async Task Forecast_IgnoresInactiveSubscriptions()
    {
        var user = await RegisterOnAsync(2027, 1, 15);
        await user.AddSubscriptionAsync("Pausad", 999m, BillingInterval.Monthly, D(2027, 1, 20), isActive: false);

        var forecast = await GetForecastAsync(user);

        Assert.Equal(0m, forecast.Total);
        Assert.Empty(forecast.Months.SelectMany(m => m.Payments));
    }


    //----------------------
    //-----Summor och ordning
    //----------------------

    [Fact]
    public async Task Forecast_SplitsMonthCostByCategory_LargestFirst()
    {
        var user = await RegisterOnAsync(2027, 1, 15);
        var streaming = user.Categories[0];
        var music = user.Categories[1];
        await user.AddSubscriptionAsync("A", 100m, BillingInterval.Monthly, D(2027, 2, 5), categoryId: music.Id);
        await user.AddSubscriptionAsync("B", 60m, BillingInterval.Monthly, D(2027, 2, 6), categoryId: streaming.Id);
        await user.AddSubscriptionAsync("C", 70m, BillingInterval.Monthly, D(2027, 2, 7), categoryId: streaming.Id);

        var february = (await GetForecastAsync(user, 2)).Months[1];

        Assert.Equal(230m, february.Total);
        Assert.Collection(
            february.CostByCategory,
            first =>
            {
                Assert.Equal(streaming.Id, first.Category.Id);
                Assert.Equal(streaming.Name, first.Category.Name);
                Assert.Equal(streaming.Color, first.Category.Color);
                Assert.Equal(130m, first.Total);
            },
            second =>
            {
                Assert.Equal(music.Id, second.Category.Id);
                Assert.Equal(music.Name, second.Category.Name);
                Assert.Equal(music.Color, second.Category.Color);
                Assert.Equal(100m, second.Total);
            });
    }

    [Fact]
    public async Task Forecast_OrdersCategoriesWithEqualTotalsByName()
    {
        var user = await RegisterOnAsync(2027, 1, 15);
        var streaming = user.Categories.Single(c => c.Name == "Streaming");
        var music = user.Categories.Single(c => c.Name == "Musik");
        await user.AddSubscriptionAsync("A", 50m, BillingInterval.Monthly, D(2027, 2, 5), categoryId: streaming.Id);
        await user.AddSubscriptionAsync("B", 50m, BillingInterval.Monthly, D(2027, 2, 6), categoryId: music.Id);

        var february = (await GetForecastAsync(user, 2)).Months[1];

        // Streaming skapas före Musik och har därför lägre id, så namnet (inte id) måste avgöra ordningen.
        Assert.Equal(["Musik", "Streaming"], february.CostByCategory.Select(c => c.Category.Name));
    }

    [Fact]
    public async Task Forecast_SortsPaymentsByDateThenName()
    {
        var user = await RegisterOnAsync(2027, 1, 15);
        await user.AddSubscriptionAsync("B", 10m, BillingInterval.Monthly, D(2027, 2, 3));
        await user.AddSubscriptionAsync("A", 10m, BillingInterval.Monthly, D(2027, 2, 3));
        await user.AddSubscriptionAsync("C", 10m, BillingInterval.Monthly, D(2027, 2, 1));

        var february = (await GetForecastAsync(user, 2)).Months[1];

        Assert.Equal(["C", "A", "B"], february.Payments.Select(p => p.Name));
    }

    [Fact]
    public async Task Forecast_JsonShape_MatchesWhatTheFrontendReads()
    {
        var user = await RegisterOnAsync(2027, 1, 15);
        var category = user.Categories[0];
        var subscription = await user.AddSubscriptionAsync(
            "Netflix", 99.5m, BillingInterval.Monthly, D(2027, 1, 20), categoryId: category.Id);

        var json = await user.Client.GetStringAsync("/api/forecast?months=1");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(99.5m, root.GetProperty("total").GetDecimal());

        var month = root.GetProperty("months")[0];
        Assert.Equal(2027, month.GetProperty("year").GetInt32());
        Assert.Equal(1, month.GetProperty("month").GetInt32());
        Assert.Equal(99.5m, month.GetProperty("total").GetDecimal());

        var byCategory = month.GetProperty("costByCategory")[0];
        Assert.Equal(99.5m, byCategory.GetProperty("total").GetDecimal());
        Assert.Equal(category.Id, byCategory.GetProperty("category").GetProperty("id").GetInt32());
        Assert.Equal(category.Name, byCategory.GetProperty("category").GetProperty("name").GetString());
        Assert.Equal(category.Color, byCategory.GetProperty("category").GetProperty("color").GetString());

        var payment = month.GetProperty("payments")[0];
        Assert.Equal(subscription.Id, payment.GetProperty("subscriptionId").GetInt32());
        Assert.Equal("Netflix", payment.GetProperty("name").GetString());
        Assert.Equal(99.5m, payment.GetProperty("amount").GetDecimal());
        Assert.Equal("2027-01-20", payment.GetProperty("date").GetString());
        Assert.Equal(category.Name, payment.GetProperty("category").GetProperty("name").GetString());
    }
}
