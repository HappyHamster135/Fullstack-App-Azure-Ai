import api from "./client.js";

class ForecastApi {
  constructor(http) {
    this.http = http;
  }

  async getForecast(months, signal) {
    const { data } = await this.http.get("/api/forecast", {
      params: { months },
      signal,
    });
    return data;
  }
}

export const forecastApi = new ForecastApi(api);
