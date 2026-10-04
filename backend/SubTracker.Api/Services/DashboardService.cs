using Microsoft.EntityFrameworkCore;
using SubTracker.Api.Common;
using SubTracker.Api.Data;
using SubTracker.Api.Dtos.Dashboard;
using SubTracker.Api.Entities;
using SubTracker.Api.Mappings;

namespace SubTracker.Api.Services;

public class DashboardService(AppDbContext db, TimeProvider timeProvider) : IDashboardService
{
    private const int UpcomingDays = 30;
    private const int HistoryMonths = 6;


    //------------
    //-----Summary
    //------------

    public async Task<DashboardResponse> GetAsync(string userId)
    {
        var today = timeProvider.GetToday();

        var activeSubscriptions = await db.Subscriptions
            .Include(s => s.Category)
            .Where(s => s.UserId == userId && s.IsActive)
            .ToListAsync();

        var totalMonthlyCost = activeSubscriptions.Sum(s => s.MonthlyCost);

        return new DashboardResponse(
            activeSubscriptions.Count,
            Money.Round(totalMonthlyCost),
            Money.Round(totalMonthlyCost * 12),
            GetCostByCategory(activeSubscriptions),
            GetUpcomingPayments(activeSubscriptions, today),
            await GetPaymentsByMonthAsync(userId, today));
    }


    //---------------------
    //-----Cost by category
    //---------------------

    private static List<CategoryCostResponse> GetCostByCategory(List<Subscription> subscriptions) =>
        subscriptions
            .GroupBy(s => s.CategoryId)
            .Select(group => new CategoryCostResponse(
                group.First().Category!.ToResponse(),
                Money.Round(group.Sum(s => s.MonthlyCost)),
                group.Count()))
            .OrderByDescending(c => c.MonthlyCost)
            .ToList();


    //----------------------
    //-----Upcoming payments
    //----------------------

    private static List<UpcomingPaymentResponse> GetUpcomingPayments(List<Subscription> subscriptions, DateOnly today) =>
        subscriptions
            .Where(s => s.NextPaymentDate <= today.AddDays(UpcomingDays))
            .OrderBy(s => s.NextPaymentDate)
            .Select(s => new UpcomingPaymentResponse(s.Id, s.Name, s.Price, s.NextPaymentDate, s.Category!.ToResponse()))
            .ToList();


    //----------------------
    //-----Payments by month
    //----------------------

    private async Task<List<MonthlyPaymentResponse>> GetPaymentsByMonthAsync(string userId, DateOnly today)
    {
        var firstMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-(HistoryMonths - 1));

        var payments = await db.Payments
            .Where(p => p.Subscription!.UserId == userId && p.PaidOn >= firstMonth)
            .Select(p => new { p.PaidOn, p.Amount })
            .ToListAsync();

        return Enumerable.Range(0, HistoryMonths)
            .Select(offset => firstMonth.AddMonths(offset))
            .Select(month => new MonthlyPaymentResponse(
                month.Year,
                month.Month,
                payments
                    .Where(p => p.PaidOn.Year == month.Year && p.PaidOn.Month == month.Month)
                    .Sum(p => p.Amount)))
            .ToList();
    }
}
