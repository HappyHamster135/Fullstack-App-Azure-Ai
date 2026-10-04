using SubTracker.Api.Dtos.Forecast;
using SubTracker.Api.Entities;

namespace SubTracker.Api.Mappings;

public static class ForecastMappings
{
    public static ForecastPaymentResponse ToForecastPayment(this Subscription subscription, DateOnly date) => new(
        subscription.Id,
        subscription.Name,
        subscription.Price,
        date,
        subscription.Category!.ToResponse());
}
