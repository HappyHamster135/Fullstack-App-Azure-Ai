#!/usr/bin/env python3
"""Oberoende kontroll av prognosens datum och summor.

Beräknar de förväntade betalningarna i Python (egen kalenderaritmetik, ingen kod gemensam med C#) och jämför med det
riktiga API:t för slumpade prenumerationer: månadsskiften, 29 februari, förfallna datum, alla fyra intervall och
fem periodlängder. Kräver att API:t körs (se README i den här mappen).

    python3 oracle_check.py
"""
import calendar
import datetime
import random

from api import add_subscription, call, register, swedish_today

INTERVALS = ["Weekly", "Monthly", "Quarterly", "Yearly"]


def add_months(day, months):
    index = day.year * 12 + day.month - 1 + months
    year, month = divmod(index, 12)
    month += 1
    return datetime.date(year, month, min(day.day, calendar.monthrange(year, month)[1]))


def occurrence(anchor, interval, k):
    if interval == "Weekly":
        return anchor + datetime.timedelta(days=7 * k)
    return add_months(anchor, {"Monthly": 1, "Quarterly": 3, "Yearly": 12}[interval] * k)


def expected_payments(subscriptions, today, months):
    first = today.replace(day=1)
    last = add_months(first, months) - datetime.timedelta(days=1)
    result = []
    for sub in subscriptions:
        if not sub["active"]:
            continue
        k = 0
        while True:
            date = occurrence(sub["next"], sub["interval"], k)
            if date > last:
                break
            if date >= today:
                result.append((sub["id"], date.isoformat()))
            k += 1
    return sorted(result)


def main():
    random.seed(20261004)
    today = swedish_today()
    specials = [datetime.date(2026, 1, 31), datetime.date(2026, 3, 31), datetime.date(2028, 2, 29), datetime.date(2026, 5, 30),
                today, today - datetime.timedelta(days=1), today + datetime.timedelta(days=1),
                datetime.date(2026, 10, 31), datetime.date(2026, 10, 1), datetime.date(2026, 10, 30)]
    plan = [(base, interval) for base in specials for interval in INTERVALS]
    plan += [(today + datetime.timedelta(days=random.randint(-900, 900)), random.choice(INTERVALS)) for _ in range(220)]

    deviations = 0
    total_subscriptions = 0
    # Högst 200 prenumerationer per användare, så planen delas på flera användare.
    for batch_start in range(0, len(plan), 130):
        token, categories = register()
        created = []
        for i, (next_date, interval) in enumerate(plan[batch_start:batch_start + 130]):
            price = round(random.uniform(9, 399), 2)
            active = i % 11 != 0
            status, body = add_subscription(token, categories[i % len(categories)], f"S{i:03d}", price, interval, next_date.isoformat(), active=active)
            assert status == 201, (status, body)
            created.append({"id": body["id"], "next": next_date, "interval": interval, "active": active, "price": price, "cat": categories[i % len(categories)]["id"]})
        total_subscriptions += len(created)
        price_of = {s["id"]: s["price"] for s in created}

        for months in (1, 3, 6, 12, 24):
            status, body, _ = call("GET", f"/api/forecast?months={months}&includePayments=true", token=token)
            assert status == 200, (status, body)
            got = sorted((p["subscriptionId"], p["date"]) for m in body["months"] for p in m["payments"])
            expected = expected_payments(created, today, months)
            dates_ok = got == expected
            sums_ok = True
            first = today.replace(day=1)
            for offset, month in enumerate(body["months"]):
                day = add_months(first, offset)
                prefix = f"{day.year:04d}-{day.month:02d}"
                month_total = round(sum(price_of[sid] for sid, d in expected if d.startswith(prefix)), 2)
                category_total = round(sum(c["total"] for c in month["costByCategory"]), 2)
                sums_ok &= abs(month_total - month["total"]) < 0.001 and abs(category_total - month["total"]) < 0.001
            sums_ok &= abs(round(sum(m["total"] for m in body["months"]), 2) - body["total"]) < 0.001
            print(f"användare {batch_start // 130 + 1}, months={months:2}: {len(got):4} betalningar | datum {'OK' if dates_ok else 'FEL'} | summor {'OK' if sums_ok else 'FEL'}")
            if not (dates_ok and sums_ok):
                deviations += 1
                print("  saknas i API:", sorted(set(expected) - set(got))[:5], " extra i API:", sorted(set(got) - set(expected))[:5])

    print(f"\n{total_subscriptions} prenumerationer, dagens datum (svensk tid) {today}, avvikelser: {deviations}")
    raise SystemExit(1 if deviations else 0)


if __name__ == "__main__":
    main()
