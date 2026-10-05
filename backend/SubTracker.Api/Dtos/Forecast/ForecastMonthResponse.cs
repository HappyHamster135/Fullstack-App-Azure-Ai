namespace SubTracker.Api.Dtos.Forecast;

public record ForecastMonthResponse(
    int Year,
    int Month,
    decimal Total,
    List<ForecastCategoryResponse> CostByCategory,
    List<ForecastPaymentResponse>? Payments);
