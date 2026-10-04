using SubTracker.Api.Dtos.Categories;

namespace SubTracker.Api.Dtos.Forecast;

public record ForecastPaymentResponse(
    int SubscriptionId,
    string Name,
    decimal Amount,
    DateOnly Date,
    CategoryResponse Category);
