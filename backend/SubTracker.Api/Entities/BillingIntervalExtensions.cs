namespace SubTracker.Api.Entities;

public static class BillingIntervalExtensions
{
    public static decimal ToMonthlyCost(this BillingInterval interval, decimal price) => interval switch
    {
        BillingInterval.Weekly => price * 52 / 12,
        BillingInterval.Monthly => price,
        BillingInterval.Quarterly => price / 3,
        BillingInterval.Yearly => price / 12,
        _ => throw new ArgumentOutOfRangeException(nameof(interval)),
    };

    public static DateOnly NextDateAfter(this BillingInterval interval, DateOnly date) =>
        interval.AddIntervals(date, 1);

    public static DateOnly AddIntervals(this BillingInterval interval, DateOnly date, int count) => interval switch
    {
        BillingInterval.Weekly => date.AddDays(7 * count),
        BillingInterval.Monthly => date.AddMonths(count),
        BillingInterval.Quarterly => date.AddMonths(3 * count),
        BillingInterval.Yearly => date.AddYears(count),
        _ => throw new ArgumentOutOfRangeException(nameof(interval)),
    };
}
