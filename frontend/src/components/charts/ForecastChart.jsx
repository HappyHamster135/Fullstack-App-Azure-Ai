import {
  Bar,
  BarChart,
  LabelList,
  ResponsiveContainer,
  Tooltip,
  XAxis,
} from "recharts";
import {
  formatCompactCurrency,
  formatCurrency,
  formatMonth,
  formatMonthYear,
} from "../../utils/format.js";
import CategoryLabel from "../CategoryLabel.jsx";
import { CHART_THEME } from "./chartTheme.js";

// Värdeetiketterna ovanför staplarna får bara plats när staplarna är tillräckligt breda. Uppmätt i webbläsare
// krockade sex fulla etiketter ("3 432 kr") på en telefon på 360 px, så där visas kortare belopp ("3,4 tkr").
// Tolv staplar är för smala för det: de får etiketter först från lg. Beloppen finns alltid kvar i tooltipen.
const fullLabel = (value) => (value > 0 ? formatCurrency(value) : "");
const compactLabel = (value) => (value > 0 ? formatCompactCurrency(value) : "");

function fullLabelClassName(monthCount) {
  if (monthCount <= 3) return undefined;

  return monthCount <= 6 ? "d-none d-sm-block" : "d-none d-lg-block";
}

//------------
//-----Tooltip
//------------

function ForecastTooltip({ active, payload }) {
  if (!active || !payload?.length) {
    return null;
  }

  const row = payload[0].payload;

  return (
    <div className="bg-white border rounded shadow-sm px-2 py-1 small">
      <div className="fw-semibold">{formatCurrency(row.total)}</div>
      <div className="text-body-secondary">{row.title}</div>

      {row.categories.length === 0 ? (
        <div className="text-body-secondary">Inga betalningar</div>
      ) : (
        <div className="border-top mt-1 pt-1">
          {row.categories.map(({ category, total }) => (
            <div
              key={category.id}
              className="d-flex justify-content-between gap-3"
            >
              <CategoryLabel category={category} />
              <span>{formatCurrency(total)}</span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

//----------
//-----Chart
//----------

function ForecastChart({ data }) {
  const rows = data.map((item) => ({
    label: formatMonth(item.year, item.month),
    title: formatMonthYear(item.year, item.month),
    total: item.total,
    categories: item.costByCategory ?? [],
  }));

  if (rows.every((row) => row.total === 0)) {
    return (
      <p className="text-body-secondary mb-0">
        Inga betalningar väntas under perioden.
      </p>
    );
  }

  const showCompactLabels = rows.length > 3 && rows.length <= 6;

  return (
    <ResponsiveContainer width="100%" height={240}>
      <BarChart
        data={rows}
        title="Förväntade betalningar per månad"
        margin={{ top: 28, right: 8, bottom: 0, left: 8 }}
      >
        <XAxis
          dataKey="label"
          interval="preserveStartEnd"
          axisLine={{ stroke: CHART_THEME.axis }}
          tickLine={false}
          tick={{ fill: CHART_THEME.muted, fontSize: CHART_THEME.fontSize }}
        />
        <Tooltip
          cursor={{ fill: CHART_THEME.hover }}
          content={<ForecastTooltip />}
        />
        <Bar
          dataKey="total"
          fill={CHART_THEME.bar}
          maxBarSize={24}
          radius={[4, 4, 0, 0]}
          isAnimationActive={false}
        >
          <LabelList
            dataKey="total"
            position="top"
            className={fullLabelClassName(rows.length)}
            formatter={fullLabel}
            fill={CHART_THEME.text}
            fontSize={12}
          />
          {showCompactLabels && (
            <LabelList
              dataKey="total"
              position="top"
              className="d-sm-none"
              formatter={compactLabel}
              fill={CHART_THEME.text}
              fontSize={11}
            />
          )}
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  );
}

export default ForecastChart;
