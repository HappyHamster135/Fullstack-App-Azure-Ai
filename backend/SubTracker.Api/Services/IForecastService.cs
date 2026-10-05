using SubTracker.Api.Dtos.Forecast;

namespace SubTracker.Api.Services;

public interface IForecastService
{
    Task<ForecastResponse> GetAsync(string userId, int months, bool includePayments = false);
}
