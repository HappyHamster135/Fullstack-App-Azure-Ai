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
    // Varje datum räknas från nästa betalning och inte från datumet före. Annars skulle en betalning
    // den 31:a hamna på den 28:e efter februari och aldrig komma tillbaka till den 31:a.
    public IEnumerable<DateOnly> GetPaymentDates(DateOnly from, DateOnly to)
    {
        var date = NextPaymentDate;

        for (var count = 1; date <= to; count++)
        {
            if (date >= from)
            {
                yield return date;
            }

            date = BillingInterval.AddIntervals(NextPaymentDate, count);
        }
    }
}
