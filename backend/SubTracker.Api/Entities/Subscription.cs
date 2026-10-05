namespace SubTracker.Api.Entities;

public class Subscription
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public BillingInterval BillingInterval { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly NextPaymentDate { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CategoryId { get; set; }

    public Category? Category { get; set; }

    public string UserId { get; set; } = string.Empty;

    public AppUser? User { get; set; }

    public List<Payment> Payments { get; set; } = [];

    public decimal MonthlyCost => BillingInterval.ToMonthlyCost(Price);

    public Payment RegisterPayment(decimal amount, DateOnly paidOn)
    {
        var payment = new Payment { Amount = amount, PaidOn = paidOn };

        Payments.Add(payment);
        NextPaymentDate = BillingInterval.NextDateAfter(NextPaymentDate);

        return payment;
    }

    // Betalningsdatumen mellan from och to (inklusive), utifrån nästa betalning och intervallet.
    // Inom en prognos räknas varje datum från nästa betalning och inte från datumet före, så en betalning
    // den 31:a blir 28 feb, 31 mars, 30 april och inte 28:e resten av året.
    // Observera: RegisterPayment stegar fortfarande från föregående datum, så efter "Markera betald" över februari
    // har det lagrade datumet redan glidit till den 28:e. Det kräver att betalningsdagen lagras (se README, Kända begränsningar).
    // Loopen börjar vid första betalningen som kan ligga på eller efter from i stället för att stega från
    // NextPaymentDate. Datumet kommer från användaren, så arbetet får inte växa med dess ålder.
    public IEnumerable<DateOnly> GetPaymentDates(DateOnly from, DateOnly to)
    {
        for (var count = BillingInterval.IntervalsBefore(NextPaymentDate, from); ; count++)
        {
            var date = BillingInterval.AddIntervals(NextPaymentDate, count);

            if (date > to)
            {
                yield break;
            }

            if (date >= from)
            {
                yield return date;
            }
        }
    }
}
