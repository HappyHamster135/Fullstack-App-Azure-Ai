import { useEffect, useState } from "react";
import { Alert, Card, ToggleButton, ToggleButtonGroup } from "react-bootstrap";
import { getErrorMessage } from "../api/errors.js";
import { forecastApi } from "../api/ForecastApi.js";
import { formatCurrency } from "../utils/format.js";
import ForecastChart from "./charts/ForecastChart.jsx";
import LoadingSpinner from "./LoadingSpinner.jsx";

const MONTH_OPTIONS = [3, 6, 12];
const DEFAULT_MONTHS = 6;

function ForecastCard() {
  const [months, setMonths] = useState(DEFAULT_MONTHS);
  const [forecast, setForecast] = useState(null);
  const [error, setError] = useState("");
  const [isLoading, setIsLoading] = useState(true);

  //------------
  //-----Loading
  //------------

  useEffect(() => {
    // Ett sent svar för ett tidigare val av antal månader ska inte skriva över det nya valet.
    let isCurrent = true;

    async function load() {
      setIsLoading(true);
      setError("");

      try {
        const result = await forecastApi.getForecast(months);

        if (isCurrent) {
          setForecast(result);
        }
      } catch (loadError) {
        if (isCurrent) {
          setError(getErrorMessage(loadError));
        }
      } finally {
        if (isCurrent) {
          setIsLoading(false);
        }
      }
    }

    load();

    return () => {
      isCurrent = false;
    };
  }, [months]);

  //-----------
  //-----Render
  //-----------

  let content;

  if (error) {
    content = (
      <Alert variant="danger" className="mb-0">
        {error}
      </Alert>
    );
  } else if (!forecast) {
    content = <LoadingSpinner />;
  } else {
    content = (
      <div className={isLoading ? "opacity-50" : undefined}>
        <p className="mb-3">
          <span className="fs-2 fw-semibold">
            {formatCurrency(forecast.total)}
          </span>{" "}
          <span className="text-body-secondary">
            de kommande {forecast.months.length} månaderna
          </span>
        </p>

        <ForecastChart data={forecast.months} />

        <p className="small text-body-secondary mt-2 mb-0">
          Bygger på nästa betalningsdatum och intervall för aktiva
          prenumerationer. Innevarande månad räknas från och med idag.
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
