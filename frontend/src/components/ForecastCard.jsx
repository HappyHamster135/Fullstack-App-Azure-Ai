import { useEffect, useState } from "react";
import {
  Alert,
  Button,
  Card,
  ToggleButton,
  ToggleButtonGroup,
} from "react-bootstrap";
import { getErrorMessage, isCanceled } from "../api/errors.js";
import { forecastApi } from "../api/ForecastApi.js";
import { formatCurrency, formatMonthYear } from "../utils/format.js";
import ForecastChart from "./charts/ForecastChart.jsx";
import LoadingSpinner from "./LoadingSpinner.jsx";

const MONTH_OPTIONS = [3, 6, 12];
const DEFAULT_MONTHS = 6;

function ForecastCard() {
  const [months, setMonths] = useState(DEFAULT_MONTHS);
  const [forecast, setForecast] = useState(null);
  const [error, setError] = useState("");
  const [isLoading, setIsLoading] = useState(true);
  const [reloadKey, setReloadKey] = useState(0);

  //------------
  //-----Loading
  //------------

  useEffect(() => {
    // Byter man period (eller lämnar sidan) avbryts det pågående anropet. Då kan ett sent svar för ett
    // tidigare val inte skriva över det nya, och onödig data laddas inte ner färdigt.
    const controller = new AbortController();

    async function load() {
      setIsLoading(true);
      setError("");

      try {
        const result = await forecastApi.getForecast(months, controller.signal);

        if (!controller.signal.aborted) {
          setForecast(result);
        }
      } catch (loadError) {
        if (!isCanceled(loadError)) {
          setError(getErrorMessage(loadError));
        }
      } finally {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      }
    }

    load();

    return () => controller.abort();
  }, [months, reloadKey]);

  //-----------
  //-----Render
  //-----------

  let content;

  if (error) {
    content = (
      <Alert variant="danger" className="mb-0">
        <div className="d-flex flex-wrap justify-content-between align-items-center gap-2">
          <span>{error}</span>
          <Button
            size="sm"
            variant="outline-danger"
            onClick={() => setReloadKey((key) => key + 1)}
          >
            Försök igen
          </Button>
        </div>
      </Alert>
    );
  } else if (!forecast) {
    content = <LoadingSpinner />;
  } else {
    // Perioden är hela kalendermånader och den första räknas bara från idag, så slutet anges i stället för "N månader".
    const lastMonth = forecast.months.at(-1);

    content = (
      <div
        className={isLoading ? "opacity-50" : undefined}
        aria-busy={isLoading}
      >
        <p className="mb-3">
          <span className="fs-2 fw-semibold">
            {formatCurrency(forecast.total)}
          </span>{" "}
          <span className="text-body-secondary">
            till och med {formatMonthYear(lastMonth.year, lastMonth.month)}
          </span>
        </p>

        <ForecastChart data={forecast.months} />

        <p className="small text-body-secondary mt-2 mb-0">
          Bygger på nästa betalningsdatum och intervall för aktiva
          prenumerationer. Innevarande månad räknas från och med idag.
          Förfallna betalningar ingår inte.
        </p>
      </div>
    );
  }

  return (
    <Card className="shadow-sm">
      <Card.Body>
        <div className="d-flex flex-wrap justify-content-between align-items-start gap-2 mb-3">
          <div>
            <h2 className="h5 mb-1">Prognos</h2>
            <p className="small text-body-secondary mb-0">
              Förväntade betalningar per månad
            </p>
          </div>

          <ToggleButtonGroup
            type="radio"
            name="forecast-months"
            size="sm"
            value={months}
            onChange={setMonths}
            aria-label="Antal månader"
          >
            {MONTH_OPTIONS.map((option) => (
              <ToggleButton
                key={option}
                id={`forecast-months-${option}`}
                value={option}
                variant="outline-primary"
              >
                {option} mån
              </ToggleButton>
            ))}
          </ToggleButtonGroup>
        </div>

        {content}
      </Card.Body>
    </Card>
  );
}

export default ForecastCard;
