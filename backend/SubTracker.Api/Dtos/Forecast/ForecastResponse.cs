namespace SubTracker.Api.Dtos.Forecast;

public record ForecastResponse(decimal Total, List<ForecastMonthResponse> Months);
