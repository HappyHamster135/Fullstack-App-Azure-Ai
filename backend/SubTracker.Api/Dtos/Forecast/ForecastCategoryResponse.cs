using SubTracker.Api.Dtos.Categories;

namespace SubTracker.Api.Dtos.Forecast;

public record ForecastCategoryResponse(CategoryResponse Category, decimal Total);
