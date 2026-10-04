using System.ComponentModel.DataAnnotations;

namespace SubTracker.Api.Dtos.Forecast;

public record ForecastRequest
{
    public const int DefaultMonths = 6;
    public const int MinMonths = 1;
    public const int MaxMonths = 24;

    [Range(MinMonths, MaxMonths, ErrorMessage = "Antal månader måste vara mellan 1 och 24.")]
    public int Months { get; init; } = DefaultMonths;
}
