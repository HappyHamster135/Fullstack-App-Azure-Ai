import { describe, expect, it } from "vitest";
import { MAX_DATE, MIN_DATE, validateSubscription } from "./validation.js";

const validValues = {
  name: "Netflix",
  categoryId: "1",
  price: "149",
  billingInterval: "Monthly",
  startDate: "2026-01-01",
  nextPaymentDate: "2026-11-01",
  isActive: true,
  notes: "",
};

describe("validateSubscription – datumgränser (samma som API:t)", () => {
  it("godtar giltiga värden", () => {
    expect(validateSubscription(validValues)).toEqual({});
  });

  it("godtar datum på gränserna", () => {
    expect(validateSubscription({ ...validValues, startDate: MIN_DATE, nextPaymentDate: MIN_DATE })).toEqual({});
    expect(validateSubscription({ ...validValues, startDate: MAX_DATE, nextPaymentDate: MAX_DATE })).toEqual({});
  });

  it.each(["1999-12-31", "0001-01-01", "2101-01-01", "9999-12-31"])("nekar startdatum %s", (startDate) => {
    const errors = validateSubscription({ ...validValues, startDate, nextPaymentDate: "2100-12-31" });

    expect(errors.startDate).toBe("Startdatumet måste vara mellan 2000-01-01 och 2100-12-31.");
  });

  it.each(["1999-12-31", "2101-01-01", "9999-12-31"])("nekar nästa betalning %s", (nextPaymentDate) => {
    const errors = validateSubscription({ ...validValues, startDate: "2000-01-01", nextPaymentDate });

    expect(errors.nextPaymentDate).toBe("Nästa betalning måste vara mellan 2000-01-01 och 2100-12-31.");
  });

  it("nekar nästa betalning före startdatum", () => {
    const errors = validateSubscription({ ...validValues, startDate: "2026-05-01", nextPaymentDate: "2026-04-30" });

    expect(errors.nextPaymentDate).toBe("Nästa betalning kan inte vara före startdatumet.");
  });

  it("kräver båda datumen", () => {
    const errors = validateSubscription({ ...validValues, startDate: "", nextPaymentDate: "" });

    expect(errors.startDate).toBe("Ange ett startdatum.");
    expect(errors.nextPaymentDate).toBe("Ange nästa betalningsdatum.");
  });
});
