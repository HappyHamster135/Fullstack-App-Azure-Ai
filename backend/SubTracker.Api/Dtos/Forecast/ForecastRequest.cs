using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SubTracker.Api.Dtos.Forecast;

public record ForecastRequest
{
    public const int DefaultMonths = 6;
    public const int MinMonths = 1;
    public const int MaxMonths = 24;

    [DefaultValue(DefaultMonths)]
    [Range(MinMonths, MaxMonths, ErrorMessage = "Antal månader måste vara mellan 1 och 24.")]
    public int Months { get; init; } = DefaultMonths;

    // Listan över enskilda betalningar var 97–100 % av svaret och används inte av webbappen,
    // så den skickas bara när den efterfrågas.
    public bool IncludePayments { get; init; }
}
