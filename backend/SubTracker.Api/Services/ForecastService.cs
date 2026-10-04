using Microsoft.EntityFrameworkCore;
using SubTracker.Api.Common;
using SubTracker.Api.Data;
using SubTracker.Api.Dtos.Forecast;
using SubTracker.Api.Mappings;

namespace SubTracker.Api.Services;

public class ForecastService(AppDbContext db) : IForecastService
{
    private const int MinMonths = 1;
    private const int MaxMonths = 24;

    private static readonly ServiceError InvalidMonths = ServiceError.Validation(
        new Dictionary<string, string[]>
        {
            ["months"] = [$"Antal månader måste vara mellan {MinMonths} och {MaxMonths}."],
        });


    //-------------
    //-----Forecast
    //-------------

    public async Task<ServiceResult<ForecastResponse>> GetAsync(string userId, int months)
    {
        if (months is < MinMonths or > MaxMonths)
        {
            return ServiceResult<ForecastResponse>.Failure(InvalidMonths);
        }

        // Perioden är hela kalendermånader från och med innevarande månad, men bara betalningar
        // från och med idag räknas med. Förfallna betalningar (före idag) ingår alltså inte.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
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

        return ServiceResult<ForecastResponse>.Success(
            new ForecastResponse(forecastMonths.Sum(m => m.Total), forecastMonths));
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
