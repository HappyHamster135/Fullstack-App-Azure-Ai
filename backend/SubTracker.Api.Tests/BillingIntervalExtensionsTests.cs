using SubTracker.Api.Entities;

namespace SubTracker.Api.Tests;

/// <summary>
/// Tester av <see cref="BillingIntervalExtensions"/>. <c>IntervalsBefore</c> är det som gör beräkningen
/// oberoende av hur gammalt ett betalningsdatum är. Två egenskaper skyddar den:
/// den får aldrig hoppa över en betalning som ligger på eller efter datumet (säkerhet),
/// och den får inte lämna mer än ett steg kvar (annars blir arbetet beroende av datumets ålder igen).
/// </summary>
public class BillingIntervalExtensionsTests
{
    private static readonly BillingInterval[] Intervals = Enum.GetValues<BillingInterval>();

    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    [Theory]
    [InlineData(BillingInterval.Weekly, "2027-03-17")]
    [InlineData(BillingInterval.Monthly, "2027-04-10")]
    [InlineData(BillingInterval.Quarterly, "2027-06-10")]
    [InlineData(BillingInterval.Yearly, "2028-03-10")]
    public void NextDateAfter_AddsExactlyOneInterval(BillingInterval interval, string expected)
    {
        Assert.Equal(DateOnly.Parse(expected), interval.NextDateAfter(D(2027, 3, 10)));
    }

    [Theory]
    [InlineData(BillingInterval.Weekly)]
    [InlineData(BillingInterval.Monthly)]
    [InlineData(BillingInterval.Quarterly)]
    [InlineData(BillingInterval.Yearly)]
    public void IntervalsBefore_IsZero_WhenTheDateIsNotAfterTheStart(BillingInterval interval)
    {
        Assert.Equal(0, interval.IntervalsBefore(D(2027, 3, 10), D(2027, 3, 10)));
        Assert.Equal(0, interval.IntervalsBefore(D(2027, 3, 10), D(2020, 1, 1)));
    }

    [Fact]
    public void IntervalsBefore_SkipsStraightToTheWindow_ForTheOldestPossibleStart()
    {
        // 0001-01-01 till 2027-01-01 är drygt 105 000 veckor. Utan hoppet stegar beräkningen igenom alla.
        var start = DateOnly.MinValue;
        var date = D(2027, 1, 1);

        var skipped = BillingInterval.Weekly.IntervalsBefore(start, date);

        Assert.Equal((date.DayNumber - start.DayNumber) / 7, skipped);
        Assert.True(skipped > 100_000);
        Assert.True(BillingInterval.Monthly.IntervalsBefore(start, date) > 24_000);
    }

    [Fact]
    public void IntervalsBefore_NeverSkipsAPaymentOnOrAfterTheDate_AndLeavesAtMostOneStep()
    {
        var random = new Random(20261004);

        for (var i = 0; i < 20_000; i++)
        {
            var interval = Intervals[random.Next(Intervals.Length)];
            var start = D(2000, 1, 1).AddDays(random.Next(0, 11_000));
            var date = start.AddDays(random.Next(1, 4_000));

            var skipped = interval.IntervalsBefore(start, date);

            var context = $"{interval}, start {start:yyyy-MM-dd}, datum {date:yyyy-MM-dd}, hoppar {skipped}";
            Assert.True(skipped >= 0, context);

            // Säkerhet: betalningen före den första som beräkningen tittar på ligger före datumet.
            if (skipped > 0)
            {
                Assert.True(interval.AddIntervals(start, skipped - 1) < date, context);
            }

            // Effektivitet: redan ett steg längre fram är betalningen efter datumet, så mer än ett steg blir aldrig kvar.
            Assert.True(interval.AddIntervals(start, skipped + 1) > date, context);
        }
    }
}
