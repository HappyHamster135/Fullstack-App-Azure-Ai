using System.ComponentModel.DataAnnotations;
using SubTracker.Api.Entities;

namespace SubTracker.Api.Dtos.Subscriptions;

public record SubscriptionRequest : IValidatableObject
{
    // Datumen är användarens indata och styr datumaritmetik och beräkningar. Utan gränser gav t.ex. 9999-12-31 ett
    // 500-fel vid "Markera betald". Samma gränser finns i frontend (utils/validation.js).
    public static readonly DateOnly MinDate = new(2000, 1, 1);
    public static readonly DateOnly MaxDate = new(2100, 12, 31);

    [Required(ErrorMessage = "Ange ett namn.")]
    [StringLength(100, ErrorMessage = "Namnet får vara högst 100 tecken.")]
    public string Name { get; init; } = string.Empty;

    [Required(ErrorMessage = "Ange ett pris.")]
    [Range(typeof(decimal), "0", "100000", ErrorMessage = "Priset måste vara mellan 0 och 100 000 kr.")]
    public decimal? Price { get; init; }

    [Required(ErrorMessage = "Välj ett betalningsintervall.")]
    [EnumDataType(typeof(BillingInterval), ErrorMessage = "Ogiltigt betalningsintervall.")]
    public BillingInterval? BillingInterval { get; init; }

    [Required(ErrorMessage = "Ange ett startdatum.")]
    public DateOnly? StartDate { get; init; }

    [Required(ErrorMessage = "Ange nästa betalningsdatum.")]
    public DateOnly? NextPaymentDate { get; init; }

    public bool IsActive { get; init; } = true;

    [StringLength(500, ErrorMessage = "Anteckningen får vara högst 500 tecken.")]
    public string? Notes { get; init; }

    [Required(ErrorMessage = "Välj en kategori.")]
    public int? CategoryId { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate is { } startDate && (startDate < MinDate || startDate > MaxDate))
        {
            yield return new ValidationResult(
                $"Startdatumet måste vara mellan {MinDate:yyyy-MM-dd} och {MaxDate:yyyy-MM-dd}.",
                [nameof(StartDate)]);
        }

        if (NextPaymentDate is { } nextPaymentDate && (nextPaymentDate < MinDate || nextPaymentDate > MaxDate))
        {
            yield return new ValidationResult(
                $"Nästa betalning måste vara mellan {MinDate:yyyy-MM-dd} och {MaxDate:yyyy-MM-dd}.",
                [nameof(NextPaymentDate)]);
        }

        if (StartDate is not null && NextPaymentDate < StartDate)
        {
            yield return new ValidationResult(
                "Nästa betalning kan inte vara före startdatumet.",
                [nameof(NextPaymentDate)]);
        }
    }
}
