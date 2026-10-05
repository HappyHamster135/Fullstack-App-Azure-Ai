#!/usr/bin/env python3
"""Mutationsprov av prognosfunktionen och rättningarna.

Kopierar backend/ till en tillfällig mapp (aldrig repot), för in ett medvetet fel i taget och kör hela testsviten.
KILLED = minst ett test föll (bra). SURVIVED = inget test föll: antingen ett hål i testerna eller en ekvivalent mutant
(felet ändrar inget beteende som går att observera). NOT-APPLIED = koden har ändrats så att mönstret inte hittas.

Användning:  python3 mutation_check.py [--source <repo>/backend] [--work <tom mapp>] [ID ...]
"""
import argparse, json, os, re, shutil, subprocess, sys, tarfile, tempfile

# id, fil, original, ersättning, beskrivning, förväntad ekvivalent (True om mutanten inte kan ändra beteende)
M = [
    # --- AI-granskarens ursprungliga mutanter (anpassade till rättad kod) ---
    ("M01", "Services/ForecastService.cs", "s.NextPaymentDate <= lastDay", "s.NextPaymentDate < lastDay", "Prenumeration som förfaller exakt sista dagen i perioden tappas", False),
    ("M02", "Services/ForecastService.cs", "firstMonth.AddMonths(months).AddDays(-1)", "firstMonth.AddMonths(months).AddDays(-2)", "Periodens sista dag en dag för tidig", False),
    ("M03", "Common/TimeProviderExtensions.cs", "timeProvider.GetLocalNow().DateTime", "timeProvider.GetUtcNow().DateTime", "'Idag' i UTC i stället för svensk tid (den ursprungliga buggen)", False),
    ("M04", "Services/ForecastService.cs", "            .ThenBy(c => c.Category.Name, StringComparer.OrdinalIgnoreCase)\n", "", "Kategorier med lika summa sorteras inte på namn", False),
    ("M05", "Services/ForecastService.cs", "            .ThenBy(c => c.Subscription.Name, StringComparer.OrdinalIgnoreCase)\n", "", "Betalningar med samma datum sorteras inte på namn", False),
    ("M05b", "Services/ForecastService.cs", "c.Subscription.Name, StringComparer.OrdinalIgnoreCase", "c.Subscription.Name", "Namn jämförs med serverns kultur (ICU) i stället för ordinalt", False),
    ("M05c", "Services/ForecastService.cs", "            .ThenBy(c => c.Subscription.Id)\n", "", "Ingen id-tie-breaker för betalningar (stabil sortering + id-ordning från databasen gör den ekvivalent)", True),
    ("M05d", "Services/ForecastService.cs", "            .ThenBy(c => c.Category.Id)\n", "", "Ingen id-tie-breaker för kategorier (namnet är unikt per användare, så ekvivalent)", True),
    ("M06", "Services/ForecastService.cs", "s.UserId == userId && s.IsActive && ", "s.UserId == userId && ", "Inaktiva prenumerationer ingår", False),
    ("M07", "Services/ForecastService.cs", "s.GetPaymentDates(today, lastDay)", "s.GetPaymentDates(firstMonth, lastDay)", "Förfallna betalningar i innevarande månad ingår", False),
    ("M08", "Services/ForecastService.cs", "        ArgumentOutOfRangeException.ThrowIfLessThan(months, ForecastRequest.MinMonths);\n", "", "Servicens vakt mot months < 1 borttagen", False),
    ("M09", "Services/ForecastService.cs", "        ArgumentOutOfRangeException.ThrowIfGreaterThan(months, ForecastRequest.MaxMonths);\n", "", "Servicens vakt mot months > 24 borttagen", False),
    ("M10", "Services/ForecastService.cs", "            .AsNoTracking()\n", "", "AsNoTracking borttaget", False),
    ("M11", "Mappings/ForecastMappings.cs", "        subscription.Id,\n", "        0,\n", "SubscriptionId alltid 0", False),
    ("M12", "Mappings/CategoryMappings.cs", "new(category.Id, category.Name, category.Color)", "new(category.Id, category.Color, category.Name)", "Kategorins namn och färg ombytta", False),
    ("M13", "Entities/BillingIntervalExtensions.cs", "        if (date <= start)\n        {\n            return 0;\n        }\n", "        return 0;\n", "IntervalsBefore hoppar aldrig över något", False),
    ("M14", "Entities/BillingIntervalExtensions.cs", "(date.DayNumber - start.DayNumber) / 7,", "(date.DayNumber - start.DayNumber) / 7 + 1,", "Hoppar en vecka för mycket", False),
    ("M15", "Entities/BillingIntervalExtensions.cs", "BillingInterval.Monthly => months,", "BillingInterval.Monthly => months + 1,", "Hoppar en månad för mycket", False),
    ("M16", "Entities/BillingIntervalExtensions.cs", "BillingInterval.Quarterly => months / 3,", "BillingInterval.Quarterly => months / 3 + 1,", "Hoppar ett kvartal för mycket", False),
    ("M17", "Entities/BillingIntervalExtensions.cs", "BillingInterval.Yearly => months / 12,", "BillingInterval.Yearly => months / 12 + 1,", "Hoppar ett år för mycket", False),
    ("M17b", "Entities/BillingIntervalExtensions.cs", "BillingInterval.Quarterly => months / 3,", "BillingInterval.Quarterly => (months + 2) / 3,", "Kvartal avrundat uppåt (hoppar bara över månader före perioden)", True),
    ("M18", "Entities/Subscription.cs", "if (date >= from)", "if (date > from)", "Betalning exakt idag exkluderas", False),
    ("M19", "Entities/Subscription.cs", "if (date > to)", "if (date >= to)", "Betalning exakt sista dagen exkluderas", False),
    ("M20", "Dtos/Forecast/ForecastRequest.cs", "MaxMonths = 24", "MaxMonths = 25", "Maxgräns 25 i stället för 24", False),
    ("M21", "Dtos/Forecast/ForecastRequest.cs", "MinMonths = 1", "MinMonths = 0", "Mingräns 0 i stället för 1", False),
    ("M22", "Dtos/Forecast/ForecastRequest.cs", "DefaultMonths = 6", "DefaultMonths = 12", "Standard 12 månader", False),
    ("M23", "Controllers/ForecastController.cs", "[Authorize]\n", "", "Ingen autentisering på endpointen", False),
    ("M24", "Services/ForecastService.cs", "        GetCostByCategory(charges),", "        GetCostByCategory(charges).Take(1).ToList(),", "Bara största kategorin redovisas", False),
    ("M25", "Services/ForecastService.cs", "group.Sum(c => c.Subscription.Price)", "group.Max(c => c.Subscription.Price)", "Kategorisumma = största betalningen", False),
    ("M26", "Entities/BillingIntervalExtensions.cs", "BillingInterval.Weekly => date.AddDays(7 * count),", "BillingInterval.Weekly => date.AddDays(7 * count + (count > 40 ? 1 : 0)),", "Veckodatum glider en dag efter 40 veckor", False),
    ("M27", "Entities/BillingIntervalExtensions.cs", "BillingInterval.Monthly => date.AddMonths(count),", "BillingInterval.Monthly => date.AddMonths(count + (count > 30 ? 1 : 0)),", "Månadsdatum hoppar en månad efter 30 månader", False),
    ("M28", "Entities/BillingIntervalExtensions.cs", "BillingInterval.Quarterly => date.AddMonths(3 * count),", "BillingInterval.Quarterly => date.AddMonths(2 * count),", "Kvartal räknas som 2 månader", False),
    ("M29", "Services/ForecastService.cs", "s.UserId == userId && s.IsActive", "s.IsActive", "Ägarfiltret borttaget: alla användares prenumerationer ingår", False),
    ("M30", "Services/ForecastService.cs", "charges.Sum(c => c.Subscription.Price),", "charges.Max(c => c.Subscription.Price),", "Månadens summa = största betalningen", False),
    ("M31", "Services/ForecastService.cs", "forecastMonths.Sum(m => m.Total)", "forecastMonths.Max(m => m.Total)", "Periodens total = största månaden", False),
    # --- mutanter för rättningarna ---
    ("N01", "Services/ForecastService.cs", "includePayments ?", "true ?", "Betalningslistan skickas alltid", False),
    ("N02", "Services/ForecastService.cs", "includePayments ?", "false ?", "Betalningslistan skickas aldrig", False),
    ("N03", "Dtos/Forecast/ForecastRequest.cs", "public bool IncludePayments { get; init; }", "public bool IncludePayments { get; init; } = true;", "Betalningslistan på som standard", False),
    ("N04", "Controllers/ForecastController.cs", "request.Months, request.IncludePayments", "request.Months, false", "Controllern ignorerar includePayments", False),
    ("N05", "Controllers/ForecastController.cs", "GetAsync(User.GetUserId(), request.Months,", "GetAsync(User.GetUserId(), 6,", "Controllern ignorerar months", False),
    ("N06", "Services/SubscriptionService.cs", "MaxPerUser = 200", "MaxPerUser = 201", "Taket är 201", False),
    ("N07", "Services/SubscriptionService.cs", ">= MaxPerUser", "> MaxPerUser", "Taket off-by-one", False),
    ("N08", "Services/SubscriptionService.cs", "if (await db.Subscriptions.CountAsync(s => s.UserId == userId) >= MaxPerUser)", "if (false)", "Taket borttaget", False),
    ("N09", "Services/SubscriptionService.cs", "CountAsync(s => s.UserId == userId) >= MaxPerUser", "CountAsync() >= MaxPerUser", "Taket räknar alla användares prenumerationer", False),
    ("N10", "Dtos/Subscriptions/SubscriptionRequest.cs", "(startDate < MinDate || startDate > MaxDate)", "(startDate < MinDate)", "Startdatum saknar övre gräns", False),
    ("N11", "Dtos/Subscriptions/SubscriptionRequest.cs", "(startDate < MinDate || startDate > MaxDate)", "(startDate > MaxDate)", "Startdatum saknar nedre gräns", False),
    ("N12", "Dtos/Subscriptions/SubscriptionRequest.cs", "(nextPaymentDate < MinDate || nextPaymentDate > MaxDate)", "(nextPaymentDate < MinDate)", "Nästa betalning saknar övre gräns", False),
    ("N13", "Dtos/Subscriptions/SubscriptionRequest.cs", "MinDate = new(2000, 1, 1)", "MinDate = new(1999, 1, 1)", "Nedre gräns 1999", False),
    ("N14", "Dtos/Subscriptions/SubscriptionRequest.cs", "MaxDate = new(2100, 12, 31)", "MaxDate = new(2101, 12, 31)", "Övre gräns 2101", False),
    ("N15", "Common/SwedishTimeProvider.cs", 'ZoneId = "Europe/Stockholm"', 'ZoneId = "UTC"', "Fel tidszon i den riktiga klockan", False),
    ("N16", "Program.cs", "AddSingleton<TimeProvider>(swedishTime)", "AddSingleton<TimeProvider>(TimeProvider.System)", "Program.cs registrerar systemklockan", False),
    ("N17", "Services/PaymentService.cs", "request.PaidOn ?? timeProvider.GetToday()", "request.PaidOn ?? DateOnly.FromDateTime(DateTime.UtcNow)", "Betalningens standarddatum i UTC", False),
    ("N18", "Services/DashboardService.cs", "var today = timeProvider.GetToday();", "var today = DateOnly.FromDateTime(DateTime.UtcNow);", "Dashboardens 'idag' i UTC", False),
    ("N19", "Services/ForecastService.cs", "var today = timeProvider.GetToday();", "var today = DateOnly.FromDateTime(DateTime.UtcNow);", "Prognosens 'idag' i UTC", False),
    ("N20", "Services/DashboardService.cs", "UpcomingDays = 30", "UpcomingDays = 31", "Dashboardens fönster är 31 dagar", False),
]

def run_tests(tests_dir):
    p = subprocess.run(["dotnet", "test", "--no-restore", "--nologo"], cwd=tests_dir, capture_output=True, text=True, timeout=900)
    out = p.stdout + p.stderr
    if "error CS" in out or "Build FAILED" in out:
        return "BUILD-ERROR", out[-400:]
    m = re.search(r"(Passed|Failed)!\s+-\s+Failed:\s+(\d+), Passed:\s+(\d+), Skipped:\s+(\d+), Total:\s+(\d+)", out)
    if not m:
        return "UNKNOWN", out[-400:]
    failed, passed = int(m.group(2)), int(m.group(3))
    names = sorted(set(re.findall(r"Failed (SubTracker\.Api\.Tests\.[\w\.]+)", out)))
    return ("KILLED" if failed else "SURVIVED"), f"failed={failed} passed={passed}" + (": " + "; ".join(names)[:200] if names else "")

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--source", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "backend"))
    ap.add_argument("--work", default=None)
    ap.add_argument("--out", default=None)
    ap.add_argument("ids", nargs="*")
    a = ap.parse_args()

    work = a.work or tempfile.mkdtemp(prefix="mutation-")
    backend = os.path.join(work, "backend")
    if os.path.exists(backend):
        shutil.rmtree(backend)
    shutil.copytree(a.source, backend, ignore=shutil.ignore_patterns("bin", "obj"))
    api, tests = os.path.join(backend, "SubTracker.Api"), os.path.join(backend, "SubTracker.Api.Tests")
    subprocess.run(["dotnet", "build", "--nologo", "-v", "q"], cwd=tests, check=True, capture_output=True)

    base_status, base_info = run_tests(tests)
    print(f"BASELINE {base_status} {base_info}", flush=True)
    if base_status != "SURVIVED":
        print("Baslinjen måste vara grön innan mutanter körs."); sys.exit(1)

    results = []
    for mid, rel, old, new, desc, equivalent in M:
        if a.ids and mid not in a.ids:
            continue
        path = os.path.normpath(os.path.join(api, rel))
        src = open(path, encoding="utf-8").read()
        if old not in src:
            results.append({"id": mid, "status": "NOT-APPLIED", "desc": desc, "info": "mönstret hittades inte"})
            print(f"{mid:5} NOT-APPLIED  {desc}", flush=True)
            continue
        try:
            open(path, "w", encoding="utf-8").write(src.replace(old, new, 1))
            status, info = run_tests(tests)
        finally:
            open(path, "w", encoding="utf-8").write(src)
        results.append({"id": mid, "status": status, "desc": desc, "info": info, "equivalent": equivalent})
        print(f"{mid:5} {status:9} {desc} :: {info[:110]}", flush=True)

    killed = sum(r["status"] == "KILLED" for r in results)
    survived = [r for r in results if r["status"] == "SURVIVED"]
    unexpected = [r for r in survived if not r.get("equivalent")]
    print(f"\nKILLED {killed}/{len(results)}; överlevde {len(survived)} varav förväntat ekvivalenta {len(survived) - len(unexpected)}; oväntade överlevare: {[r['id'] for r in unexpected]}")
    if a.out:
        json.dump(results, open(a.out, "w", encoding="utf-8"), ensure_ascii=False, indent=1)

if __name__ == "__main__":
    main()
