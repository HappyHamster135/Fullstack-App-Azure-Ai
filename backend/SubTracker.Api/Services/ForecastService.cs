using Microsoft.EntityFrameworkCore;
using SubTracker.Api.Common;
using SubTracker.Api.Data;
using SubTracker.Api.Dtos.Forecast;
using SubTracker.Api.Entities;
using SubTracker.Api.Mappings;

namespace SubTracker.Api.Services;

public class ForecastService(AppDbContext db, TimeProvider timeProvider) : IForecastService
{
    // En dragning av en prenumeration ett visst datum. Svarsobjekten för enskilda betalningar skapas
    // bara när de efterfrågas, så beräkningen arbetar med den här lilla typen.
    private readonly record struct Charge(Subscription Subscription, DateOnly Date);


    //-------------
    //-----Forecast
    //-------------

    public async Task<ForecastResponse> GetAsync(string userId, int months, bool includePayments = false)
    {
        // Användarens indata valideras redan av ForecastRequest (400). Här skyddas beräkningen bara mot felanrop från annan kod.
        ArgumentOutOfRangeException.ThrowIfLessThan(months, ForecastRequest.MinMonths);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(months, ForecastRequest.MaxMonths);

        // Perioden är hela kalendermånader från och med innevarande månad, men bara betalningar
        // från och med idag räknas med. Förfallna betalningar (före idag) ingår alltså inte.
        var today = timeProvider.GetToday();
        var firstMonth = FirstDayOfMonth(today);
        var lastDay = firstMonth.AddMonths(months).AddDays(-1);

        var subscriptions = await db.Subscriptions
            .AsNoTracking()
            .Include(s => s.Category)
            .Where(s => s.UserId == userId && s.IsActive && s.NextPaymentDate <= lastDay)
            .ToListAsync();

        // Namnen jämförs utan hänsyn till versaler och utan serverns kultur (ICU och invariant läge ger annars olika
        // ordning), och id avgör när allt annat är lika. Då blir ordningen densamma på varje server.
        var chargesByMonth = subscriptions
            .SelectMany(s => s.GetPaymentDates(today, lastDay).Select(date => new Charge(s, date)))
            .OrderBy(c => c.Date)
            .ThenBy(c => c.Subscription.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Subscription.Id)
            .ToLookup(c => FirstDayOfMonth(c.Date));

        var forecastMonths = Enumerable.Range(0, months)
            .Select(offset => firstMonth.AddMonths(offset))
            .Select(month => CreateMonth(month, chargesByMonth[month].ToList(), includePayments))
            .ToList();

        return new ForecastResponse(forecastMonths.Sum(m => m.Total), forecastMonths);
    }


    //----------
    //-----Month
    //----------

    private static ForecastMonthResponse CreateMonth(DateOnly month, List<Charge> charges, bool includePayments) => new(
        month.Year,
        month.Month,
        charges.Sum(c => c.Subscription.Price),
        GetCostByCategory(charges),
        includePayments ? charges.Select(c => c.Subscription.ToForecastPayment(c.Date)).ToList() : null);


    //---------------------
    //-----Cost by category
    //---------------------

    private static List<ForecastCategoryResponse> GetCostByCategory(List<Charge> charges) =>
        charges
            .GroupBy(c => c.Subscription.CategoryId)
            .Select(group => new ForecastCategoryResponse(
                group.First().Subscription.Category!.ToResponse(),
                group.Sum(c => c.Subscription.Price)))
            .OrderByDescending(c => c.Total)
            .ThenBy(c => c.Category.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Category.Id)
            .ToList();


    //------------
    //-----Helpers
    //------------

    private static DateOnly FirstDayOfMonth(DateOnly date) => new(date.Year, date.Month, 1);
}
