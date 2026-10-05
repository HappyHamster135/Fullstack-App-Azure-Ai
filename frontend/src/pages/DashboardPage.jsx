import { useEffect, useState } from "react";
import { Alert, Button, Card, Col, Row } from "react-bootstrap";
import { Link } from "react-router";
import { dashboardApi } from "../api/DashboardApi.js";
import { getErrorMessage } from "../api/errors.js";
import CategoryCostChart from "../components/charts/CategoryCostChart.jsx";
import PaymentsChart from "../components/charts/PaymentsChart.jsx";
import EmptyState from "../components/EmptyState.jsx";
import ErrorBoundary from "../components/ErrorBoundary.jsx";
import ForecastCard from "../components/ForecastCard.jsx";
import LoadingSpinner from "../components/LoadingSpinner.jsx";
import StatCard from "../components/StatCard.jsx";
import UpcomingPayments from "../components/UpcomingPayments.jsx";
import { formatCurrency } from "../utils/format.js";

function DashboardPage() {
  const [summary, setSummary] = useState(null);
  const [error, setError] = useState("");

  useEffect(() => {
    dashboardApi
      .getSummary()
      .then(setSummary)
      .catch((loadError) => setError(getErrorMessage(loadError)));
  }, []);

  if (error) {
    return <Alert variant="danger">{error}</Alert>;
  }

  if (!summary) {
    return <LoadingSpinner />;
  }

  return (
    <>
      <h1>Dashboard</h1>
      <p className="lead">Översikt över dina aktiva prenumerationer.</p>

      {summary.activeSubscriptions === 0 ? (
        <EmptyState
          title="Inga aktiva prenumerationer"
          text="När du har lagt till prenumerationer visas kostnader och kommande betalningar här."
          action={
            <Button as={Link} to="/subscriptions">
              Gå till prenumerationer
            </Button>
          }
        />
      ) : (
        <>
          <Row className="g-3 mb-3">
            <Col lg={6}>
              <StatCard
                label="Kostnad per månad"
                value={formatCurrency(summary.totalMonthlyCost)}
                isHero
              />
            </Col>
            <Col sm={6} lg={3}>
              <StatCard
                label="Kostnad per år"
                value={formatCurrency(summary.totalYearlyCost)}
              />
            </Col>
            <Col sm={6} lg={3}>
              <StatCard
                label="Aktiva prenumerationer"
                value={summary.activeSubscriptions}
              />
            </Col>
          </Row>

          <Row className="g-3 mb-3">
            <Col lg={7}>
              <Card className="h-100 shadow-sm">
                <Card.Body>
                  <h2 className="h5 mb-1">Kostnad per kategori</h2>
                  <p className="small text-body-secondary">Per månad</p>
                  <CategoryCostChart data={summary.costByCategory} />
                </Card.Body>
              </Card>
            </Col>
            <Col lg={5}>
              <UpcomingPayments payments={summary.upcomingPayments} />
            </Col>
          </Row>

          <div className="mb-3">
            <ErrorBoundary message="Prognosen kunde inte visas. Resten av dashboarden fungerar som vanligt.">
              <ForecastCard />
            </ErrorBoundary>
          </div>

          <Card className="shadow-sm">
            <Card.Body>
              <h2 className="h5 mb-1">Betalt per månad</h2>
              <p className="small text-body-secondary">
                Registrerade betalningar de senaste sex månaderna
              </p>
              <PaymentsChart data={summary.paymentsByMonth} />
            </Card.Body>
          </Card>
        </>
      )}
    </>
  );
}

export default DashboardPage;
