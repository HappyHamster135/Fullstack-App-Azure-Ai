using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubTracker.Api.Auth;
using SubTracker.Api.Dtos.Forecast;
using SubTracker.Api.Services;

namespace SubTracker.Api.Controllers;

[Authorize]
[Route("api/forecast")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class ForecastController(IForecastService forecastService) : ApiControllerBase
{
    private const int DefaultMonths = 6;

    [HttpGet]
    [ProducesResponseType<ForecastResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ForecastResponse>> Get(int months = DefaultMonths)
    {
        var result = await forecastService.GetAsync(User.GetUserId(), months);
        return ToActionResult(result);
    }
}
