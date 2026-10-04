using Microsoft.EntityFrameworkCore;
using SubTracker.Api.Data;
using SubTracker.Api.Dtos.Forecast;
using SubTracker.Api.Mappings;

namespace SubTracker.Api.Services;

public class ForecastService(AppDbContext db, TimeProvider timeProvider) : IForecastService
{
    //-------------
    //-----Forecast
    //-------------

    public async Task<ForecastResponse> GetAsync(string userId, int months)
    {
        // Användarens indata valideras redan av ForecastRequest (400). Här skyddas beräkningen bara mot felanrop från annan kod.
        ArgumentOutOfRangeException.ThrowIfLessThan(months, ForecastRequest.MinMonths);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(months, ForecastRequest.MaxMonths);

        // Perioden är hela kalendermånader från och med innevarande månad, men bara betalningar
        // från och med idag räknas med. Förfallna betalningar (före idag) ingår alltså inte.
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var firstMonth = FirstDayOfMonth(today);
        var lastDay = firstMonth.AddMonths(months).AddDays(-1);

        var subscriptions = await db.Subscriptions
            .Include(s => s.Category)
            .Where(s => s.UserId == userId && s.IsActive && s.NextPaymentDate <= lastDay)
            .ToListAsync();

        var paymentsByMonth = subscriptions
            .SelectMany(s => s.GetPaymentDates(today, lastDay).Select(date => s.ToForecastPayment(date)))
            .OrderBy(p => p.Date)
            .ThenBy(p => p.Name)
            .ToLookup(p => FirstDayOfMonth(p.Date));

        var forecastMonths = Enumerable.Range(0, months)
            .Select(offset => firstMonth.AddMonths(offset))
            .Select(month => CreateMonth(month, paymentsByMonth[month].ToList()))
            .ToList();

        return new ForecastResponse(forecastMonths.Sum(m => m.Total), forecastMonths);
    }


    //----------
    //-----Month
    //----------

    private static ForecastMonthResponse CreateMonth(DateOnly month, List<ForecastPaymentResponse> payments) => new(
        month.Year,
        month.Month,
        payments.Sum(p => p.Amount),
        GetCostByCategory(payments),
        payments);


    //---------------------
    //-----Cost by category
    //---------------------

    private static List<ForecastCategoryResponse> GetCostByCategory(List<ForecastPaymentResponse> payments) =>
        payments
            .GroupBy(p => p.Category.Id)
            .Select(group => new ForecastCategoryResponse(group.First().Category, group.Sum(p => p.Amount)))
            .OrderByDescending(c => c.Total)
            .ThenBy(c => c.Category.Name)
            .ToList();


    //------------
    //-----Helpers
    //------------

    private static DateOnly FirstDayOfMonth(DateOnly date) => new(date.Year, date.Month, 1);
}
