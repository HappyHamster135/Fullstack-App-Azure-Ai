const currencyFormatter = new Intl.NumberFormat("sv-SE", {
  style: "currency",
  currency: "SEK",
  minimumFractionDigits: 0,
  maximumFractionDigits: 2,
});

const dateFormatter = new Intl.DateTimeFormat("sv-SE", {
  day: "numeric",
  month: "short",
  year: "numeric",
});

const monthFormatter = new Intl.DateTimeFormat("sv-SE", { month: "short" });

const monthYearFormatter = new Intl.DateTimeFormat("sv-SE", {
  month: "long",
  year: "numeric",
});

const DAY_IN_MS = 24 * 60 * 60 * 1000;

//-----------
//-----Format
//-----------

export function formatCurrency(amount) {
  return currencyFormatter.format(amount);
}

export function formatDate(isoDate) {
  return dateFormatter.format(parseIsoDate(isoDate));
}

export function formatMonth(year, month) {
  return monthFormatter.format(new Date(year, month - 1, 1));
}

export function formatMonthYear(year, month) {
  return monthYearFormatter.format(new Date(year, month - 1, 1));
}

//----------
//-----Dates
//----------

export function toIsoDate(date) {
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${date.getFullYear()}-${month}-${day}`;
}

export function daysUntil(isoDate) {
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  return Math.round((parseIsoDate(isoDate) - today) / DAY_IN_MS);
}

export function describeDue(isoDate) {
  const days = daysUntil(isoDate);

  if (days < 0) return "förfallen";
  if (days === 0) return "idag";
  if (days === 1) return "imorgon";
  return `om ${days} dagar`;
}

function parseIsoDate(isoDate) {
  const [year, month, day] = isoDate.split("-").map(Number);
  return new Date(year, month - 1, day);
}
