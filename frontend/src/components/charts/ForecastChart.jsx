import {
  Bar,
  BarChart,
  LabelList,
  ResponsiveContainer,
  Tooltip,
  XAxis,
} from "recharts";
import {
  formatCurrency,
  formatMonth,
  formatMonthYear,
} from "../../utils/format.js";
import CategoryLabel from "../CategoryLabel.jsx";
import { CHART_THEME } from "./chartTheme.js";

const MAX_MONTHS_WITH_LABELS_ON_SMALL_SCREENS = 6;

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
    categories: item.costByCategory,
  }));

  if (rows.every((row) => row.total === 0)) {
    return (
      <p className="text-body-secondary mb-0">
        Inga betalningar väntas under perioden.
      </p>
    );
  }

  // Med många månader får beloppen inte plats på små skärmar. Då visas de bara i tooltipen.
  const labelClassName =
    rows.length > MAX_MONTHS_WITH_LABELS_ON_SMALL_SCREENS
      ? "d-none d-lg-block"
      : undefined;

  return (
    <ResponsiveContainer width="100%" height={240}>
      <BarChart data={rows} margin={{ top: 28, right: 8, bottom: 0, left: 8 }}>
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
            className={labelClassName}
            formatter={(value) => (value > 0 ? formatCurrency(value) : "")}
            fill={CHART_THEME.text}
            fontSize={12}
          />
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  );
}

export default ForecastChart;
