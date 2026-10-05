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
    [HttpGet]
    [ProducesResponseType<ForecastResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ForecastResponse>> Get([FromQuery] ForecastRequest request) =>
        Ok(await forecastService.GetAsync(User.GetUserId(), request.Months, request.IncludePayments));
}
