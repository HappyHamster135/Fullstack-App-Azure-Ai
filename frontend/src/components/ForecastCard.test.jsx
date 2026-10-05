import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { forecastApi } from "../api/ForecastApi.js";
import ForecastCard from "./ForecastCard.jsx";

vi.mock("../api/ForecastApi.js", () => ({
  forecastApi: { getForecast: vi.fn() },
}));

// Diagrammet testas i webbläsaren (jsdom saknar storlekar för Recharts). Här räcker det att veta vad det får.
vi.mock("./charts/ForecastChart.jsx", () => ({
  default: ({ data }) => <div data-testid="chart">{data.length} staplar</div>,
}));

const canceledError = () =>
  Object.assign(new Error("canceled"), { code: "ERR_CANCELED" });

function forecastFor(monthCount, total = 1234) {
  return {
    total,
    months: Array.from({ length: monthCount }, (_, index) => ({
      year: 2027,
      month: index + 1,
      total: 100,
      costByCategory: [],
      payments: null,
    })),
  };
}

// Ett anrop som följer axios beteende: det avvisas med ERR_CANCELED när det avbryts.
function pendingRequest(signal) {
  let resolve;
  const promise = new Promise((res, rej) => {
    resolve = res;
    signal?.addEventListener("abort", () => rej(canceledError()));
  });

  return { promise, resolve };
}

describe("ForecastCard", () => {
  beforeEach(() => {
    vi.mocked(forecastApi.getForecast).mockReset();
  });

  it("visar summan och periodens slut när prognosen har laddats", async () => {
    vi.mocked(forecastApi.getForecast).mockResolvedValue(forecastFor(6, 1234));

    render(<ForecastCard />);

    expect(await screen.findByText(/1.234.kr/)).toBeInTheDocument();
    expect(screen.getByText("till och med juni 2027")).toBeInTheDocument();
    expect(screen.getByTestId("chart")).toHaveTextContent("6 staplar");
    expect(forecastApi.getForecast).toHaveBeenCalledWith(6, expect.any(AbortSignal));
  });

  it("hämtar det antal månader som väljs", async () => {
    vi.mocked(forecastApi.getForecast).mockImplementation(async (months) => forecastFor(months));
    const user = userEvent.setup();
    render(<ForecastCard />);
    await screen.findByTestId("chart");

    await user.click(screen.getByText("12 mån"));

    await waitFor(() => expect(screen.getByTestId("chart")).toHaveTextContent("12 staplar"));
    expect(forecastApi.getForecast).toHaveBeenLastCalledWith(12, expect.any(AbortSignal));
  });

  it("visar ett fel med 'Försök igen' och hämtar samma period på nytt", async () => {
    vi.mocked(forecastApi.getForecast)
      .mockRejectedValueOnce(new Error("Network Error"))
      .mockResolvedValue(forecastFor(6, 4321));
    const user = userEvent.setup();
    render(<ForecastCard />);

    expect(await screen.findByText(/Kunde inte nå servern/)).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Försök igen" }));

    expect(await screen.findByText(/4.321.kr/)).toBeInTheDocument();
    expect(screen.queryByText(/Kunde inte nå servern/)).not.toBeInTheDocument();
    expect(forecastApi.getForecast).toHaveBeenCalledTimes(2);
    expect(vi.mocked(forecastApi.getForecast).mock.calls.map(([months]) => months)).toEqual([6, 6]);
  });

  it("avbryter det gamla anropet när perioden byts, och ett sent svar skriver inte över valet", async () => {
    const requests = [];
    vi.mocked(forecastApi.getForecast).mockImplementation((months, signal) => {
      const request = pendingRequest(signal);
      requests.push({ months, signal, ...request });
      return request.promise;
    });
    const user = userEvent.setup();
    render(<ForecastCard />);
    await waitFor(() => expect(requests).toHaveLength(1));

    await user.click(screen.getByText("3 mån"));
    await waitFor(() => expect(requests).toHaveLength(2));
    requests[1].resolve(forecastFor(3, 300));
    expect(await screen.findByTestId("chart")).toHaveTextContent("3 staplar");

    // Det första anropet (6 månader) är avbrutet. Även om svaret skulle komma sent visas det aldrig.
    expect(requests[0].signal.aborted).toBe(true);
    requests[0].resolve(forecastFor(6, 600));
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(screen.getByTestId("chart")).toHaveTextContent("3 staplar");
  });

  it("behandlar ett avbrutet anrop som ett avbrott och inte som ett fel", async () => {
    const requests = [];
    vi.mocked(forecastApi.getForecast).mockImplementation((months, signal) => {
      const request = pendingRequest(signal);
      requests.push({ months, signal, ...request });
      return request.promise;
    });
    const user = userEvent.setup();
    render(<ForecastCard />);
    await waitFor(() => expect(requests).toHaveLength(1));

    await user.click(screen.getByText("12 mån"));
    await waitFor(() => expect(requests).toHaveLength(2));
    await new Promise((resolve) => setTimeout(resolve, 20));

    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(screen.queryByText(/Något gick fel/)).not.toBeInTheDocument();
  });

  it("markerar innehållet som upptaget medan en ny period hämtas", async () => {
    const requests = [];
    vi.mocked(forecastApi.getForecast).mockImplementation((months, signal) => {
      if (months === 6) return Promise.resolve(forecastFor(6));
      const request = pendingRequest(signal);
      requests.push(request);
      return request.promise;
    });
    const user = userEvent.setup();
    render(<ForecastCard />);
    await screen.findByTestId("chart");
    expect(document.querySelector("[aria-busy='true']")).toBeNull();

    await user.click(screen.getByText("3 mån"));

    await waitFor(() => expect(document.querySelector("[aria-busy='true']")).not.toBeNull());
    requests[0].resolve(forecastFor(3));
    await waitFor(() => expect(document.querySelector("[aria-busy='true']")).toBeNull());
  });

  it("nämner att förfallna betalningar inte ingår", async () => {
    vi.mocked(forecastApi.getForecast).mockResolvedValue(forecastFor(6));

    render(<ForecastCard />);

    expect(await screen.findByText(/Förfallna betalningar ingår inte/)).toBeInTheDocument();
  });
});
