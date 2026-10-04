import api from "./client.js";

class ForecastApi {
  constructor(http) {
    this.http = http;
  }

  async getForecast(months) {
    const { data } = await this.http.get("/api/forecast", {
      params: { months },
    });
    return data;
  }
}

export const forecastApi = new ForecastApi(api);
