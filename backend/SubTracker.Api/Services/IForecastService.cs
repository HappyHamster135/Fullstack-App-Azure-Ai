using SubTracker.Api.Common;
using SubTracker.Api.Dtos.Forecast;

namespace SubTracker.Api.Services;

public interface IForecastService
{
    Task<ServiceResult<ForecastResponse>> GetAsync(string userId, int months);
}
