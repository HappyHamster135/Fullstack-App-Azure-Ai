using SubTracker.Api.Entities;

namespace SubTracker.Api.Tests;

/// <summary>
/// Enhetstester av <see cref="Subscription.GetPaymentDates"/> utan databas och klocka.
/// </summary>
public class SubscriptionPaymentDatesTests
{
    private static readonly BillingInterval[] Intervals = Enum.GetValues<BillingInterval>();

    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    private static Subscription SubscriptionWith(BillingInterval interval, DateOnly next) =>
        new() { BillingInterval = interval, NextPaymentDate = next };

    /// <summary>
    /// Referensimplementation med egen kalenderaritmetik. Den anropar varken <c>AddIntervals</c> eller
    /// <c>DateOnly.AddMonths</c>, så ett fel i någon av dem kan inte gömma sig i både koden och referensen
    /// (AI-granskaren visade att en tidigare referens återanvände <c>AddIntervals</c>).
    /// Den stegar från nästa betalningsdatum, som AI-förslagets ursprungliga loop. Samma beräkning är också
    /// kontrollerad mot ett oberoende Python-skript: se docs/ai/verifiering/.
    /// </summary>
    private static List<DateOnly> Reference(Subscription subscription, DateOnly from, DateOnly to)
    {
        var dates = new List<DateOnly>();

        for (var count = 0; ; count++)
        {
            var date = Occurrence(subscription.NextPaymentDate, subscription.BillingInterval, count);

            if (date > to)
            {
                return dates;
            }

            if (date >= from)
            {
                dates.Add(date);
            }
        }
    }

    private static DateOnly Occurrence(DateOnly anchor, BillingInterval interval, int count) => interval switch
    {
        BillingInterval.Weekly => DateOnly.FromDayNumber(anchor.DayNumber + 7 * count),
        BillingInterval.Monthly => AddMonthsKeepingTheDay(anchor, count),
        BillingInterval.Quarterly => AddMonthsKeepingTheDay(anchor, 3 * count),
        _ => AddMonthsKeepingTheDay(anchor, 12 * count),
    };

    // Samma dag i månaden, men aldrig längre än månadens sista dag (31 jan + 1 månad = 28 eller 29 feb).
    private static DateOnly AddMonthsKeepingTheDay(DateOnly anchor, int months)
    {
        var monthIndex = anchor.Year * 12 + (anchor.Month - 1) + months;
        var year = monthIndex / 12;
        var month = monthIndex % 12 + 1;

        return new DateOnly(year, month, Math.Min(anchor.Day, DateTime.DaysInMonth(year, month)));
    }

    //-----------------
    //-----Kända fall
    //-----------------

    [Fact]
    public void Monthly_KeepsTheAnchorDayAfterShortMonths()
    {
        var subscription = SubscriptionWith(BillingInterval.Monthly, D(2027, 1, 31));

        var dates = subscription.GetPaymentDates(D(2027, 1, 1), D(2027, 6, 30)).ToList();

        Assert.Equal(
            [D(2027, 1, 31), D(2027, 2, 28), D(2027, 3, 31), D(2027, 4, 30), D(2027, 5, 31), D(2027, 6, 30)],
            dates);
    }

    [Fact]
    public void FromAndTo_AreInclusive()
    {
        var subscription = SubscriptionWith(BillingInterval.Weekly, D(2027, 1, 4));

        var dates = subscription.GetPaymentDates(D(2027, 1, 11), D(2027, 1, 25)).ToList();

        Assert.Equal([D(2027, 1, 11), D(2027, 1, 18), D(2027, 1, 25)], dates);
    }

    [Fact]
    public void NextPaymentAfterTheWindow_GivesNoDates()
    {
        var subscription = SubscriptionWith(BillingInterval.Monthly, D(2027, 8, 1));

        Assert.Empty(subscription.GetPaymentDates(D(2027, 1, 1), D(2027, 6, 30)));
    }

    [Fact]
    public void OverdueSubscription_ContinuesItsCycleFromTheStartOfTheWindow()
    {
        var subscription = SubscriptionWith(BillingInterval.Quarterly, D(2026, 5, 15));

        var dates = subscription.GetPaymentDates(D(2027, 1, 1), D(2027, 12, 31)).ToList();

        // 15 maj 2026 + 3 månader i taget: aug, nov, feb, maj, aug, nov.
        Assert.Equal([D(2027, 2, 15), D(2027, 5, 15), D(2027, 8, 15), D(2027, 11, 15)], dates);
    }

    [Fact]
    public void Yearly_LeapDayFallsBackToFebruary28AndReturnsOnTheNextLeapYear()
    {
        var subscription = SubscriptionWith(BillingInterval.Yearly, D(2028, 2, 29));

        var dates = subscription.GetPaymentDates(D(2028, 1, 1), D(2032, 12, 31)).ToList();

        Assert.Equal([D(2028, 2, 29), D(2029, 2, 28), D(2030, 2, 28), D(2031, 2, 28), D(2032, 2, 29)], dates);
    }

    //-------------------------------------------
    //-----Lika med referensen för många indata
    //-------------------------------------------

    [Fact]
    public void MatchesTheReference_ForManyRandomSubscriptionsAndWindows()
    {
        var random = new Random(20261004);
        var anchors = new[]
        {
            D(2024, 2, 29), D(2026, 1, 31), D(2026, 3, 31), D(2026, 5, 31), D(2026, 12, 31),
            D(2027, 1, 1), D(2027, 2, 28), D(2028, 2, 29), D(1999, 12, 31), D(2001, 1, 1),
        };

        for (var i = 0; i < 4000; i++)
        {
            var interval = Intervals[random.Next(Intervals.Length)];
            var next = i % 3 == 0
                ? anchors[random.Next(anchors.Length)]
                : D(2020, 1, 1).AddDays(random.Next(0, 3000));
            var from = D(2026, 1, 1).AddDays(random.Next(-400, 1500));
            var to = from.AddDays(random.Next(0, 800));
            var subscription = SubscriptionWith(interval, next);

            var actual = subscription.GetPaymentDates(from, to).ToList();

            Assert.True(
                Reference(subscription, from, to).SequenceEqual(actual),
                $"{interval}, nästa {next:yyyy-MM-dd}, fönster {from:yyyy-MM-dd}..{to:yyyy-MM-dd}");
        }
    }

    [Theory]
    [InlineData(BillingInterval.Weekly)]
    [InlineData(BillingInterval.Monthly)]
    [InlineData(BillingInterval.Quarterly)]
    [InlineData(BillingInterval.Yearly)]
    public void VeryOldNextPaymentDate_GivesTheSameDatesAsSteppingFromIt(BillingInterval interval)
    {
        // Äldsta möjliga datum: DTO:n sätter ingen nedre gräns för NextPaymentDate.
        var subscription = SubscriptionWith(interval, DateOnly.MinValue);
        var from = D(2027, 1, 1);
        var to = D(2028, 12, 31);

        Assert.Equal(Reference(subscription, from, to), subscription.GetPaymentDates(from, to).ToList());
    }
}
