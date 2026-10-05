import { Component } from "react";
import { Alert, Button } from "react-bootstrap";

// Fångar renderingsfel i en del av sidan, så att resten av appen fortsätter fungera i stället för att bli tom.
// React har ingen hook för detta, så det måste vara en klasskomponent.
class ErrorBoundary extends Component {
  state = { hasError: false };

  static getDerivedStateFromError() {
    return { hasError: true };
  }

  componentDidCatch(error) {
    console.error(error);
  }

  render() {
    if (!this.state.hasError) {
      return this.props.children;
    }

    return (
      <Alert variant="warning" className="mb-0">
        <div className="d-flex flex-wrap justify-content-between align-items-center gap-2">
          <span>
            {this.props.message ?? "Den här delen av sidan kunde inte visas."}
          </span>
          <Button
            size="sm"
            variant="outline-secondary"
            onClick={() => this.setState({ hasError: false })}
          >
            Försök igen
          </Button>
        </div>
      </Alert>
    );
  }
}

export default ErrorBoundary;
