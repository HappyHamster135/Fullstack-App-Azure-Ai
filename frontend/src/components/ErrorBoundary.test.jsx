import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import ErrorBoundary from "./ErrorBoundary.jsx";

let shouldThrow = false;

function Fragile() {
  if (shouldThrow) {
    throw new TypeError("Cannot read properties of undefined (reading 'length')");
  }

  return <p>Allt fungerar</p>;
}

describe("ErrorBoundary", () => {
  beforeEach(() => {
    shouldThrow = false;
    // React loggar fel som fångas av en gräns. Det är väntat i de här testerna.
    vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("visar innehållet när inget går fel", () => {
    render(
      <ErrorBoundary>
        <Fragile />
      </ErrorBoundary>,
    );

    expect(screen.getByText("Allt fungerar")).toBeInTheDocument();
  });

  it("visar ett meddelande i stället för att tömma sidan när ett barn kastar, och resten av sidan finns kvar", () => {
    shouldThrow = true;

    render(
      <>
        <h1>Dashboard</h1>
        <ErrorBoundary message="Prognosen kunde inte visas.">
          <Fragile />
        </ErrorBoundary>
      </>,
    );

    expect(screen.getByText("Prognosen kunde inte visas.")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Dashboard" })).toBeInTheDocument();
    expect(screen.queryByText("Allt fungerar")).not.toBeInTheDocument();
  });

  it("visar innehållet igen när man trycker på 'Försök igen' och felet är borta", async () => {
    shouldThrow = true;
    const user = userEvent.setup();
    render(
      <ErrorBoundary>
        <Fragile />
      </ErrorBoundary>,
    );

    shouldThrow = false;
    await user.click(screen.getByRole("button", { name: "Försök igen" }));

    expect(screen.getByText("Allt fungerar")).toBeInTheDocument();
  });
});
