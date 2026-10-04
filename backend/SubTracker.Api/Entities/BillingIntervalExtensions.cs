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

    // Hur många hela intervall som kan hoppas över från start utan att passera en betalning på eller efter date.
    // Betalningarna före det returnerade antalet ligger alltid före date, så ingen betalning missas.
    public static int IntervalsBefore(this BillingInterval interval, DateOnly start, DateOnly date)
    {
        if (date <= start)
        {
            return 0;
        }

        var months = (date.Year - start.Year) * 12 + date.Month - start.Month;

        return interval switch
        {
            BillingInterval.Weekly => (date.DayNumber - start.DayNumber) / 7,
            BillingInterval.Monthly => months,
            BillingInterval.Quarterly => months / 3,
            BillingInterval.Yearly => months / 12,
            _ => throw new ArgumentOutOfRangeException(nameof(interval)),
        };
    }
}
