import { describe, expect, it } from "vitest";
import { formatCompactCurrency, formatMonth, formatMonthYear, toIsoDate } from "./format.js";

describe("format", () => {
  it("formatMonthYear skriver månad och år på svenska", () => {
    expect(formatMonthYear(2027, 3)).toBe("mars 2027");
    expect(formatMonthYear(2026, 12)).toBe("december 2026");
  });

  it("formatMonth skriver kort månad", () => {
    expect(formatMonth(2027, 1)).toBe("jan.");
  });

  it.each([
    [249, "249\u00a0kr"],
    [999.4, "999\u00a0kr"],
    [1000, "1,0\u00a0tkr"],
    [2038, "2,0\u00a0tkr"],
    [3432, "3,4\u00a0tkr"],
    [9999, "10,0\u00a0tkr"],
    [10000, "10\u00a0tkr"],
    [17713, "18\u00a0tkr"],
    [100000, "100\u00a0tkr"],
  ])("formatCompactCurrency(%s) blir %j (med hårt mellanrum)", (amount, expected) => {
    expect(formatCompactCurrency(amount)).toBe(expected);
  });

  it("toIsoDate använder lokalt datum med nollor", () => {
    expect(toIsoDate(new Date(2027, 0, 5))).toBe("2027-01-05");
    expect(toIsoDate(new Date(2027, 11, 31))).toBe("2027-12-31");
  });
});
