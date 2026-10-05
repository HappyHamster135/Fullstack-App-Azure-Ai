#!/usr/bin/env python3
"""Mäter prognosens svarsstorlek och att taket på prenumerationer håller.

    python3 size_check.py [antal försök att skapa prenumerationer, standard 1000]
"""
import datetime
import statistics
import sys
import time
import urllib.request

from api import BASE, add_subscription, register

import json


def fetch(token, path):
    request = urllib.request.Request(BASE + path, headers={"Authorization": "Bearer " + token})
    started = time.perf_counter()
    raw = urllib.request.urlopen(request, timeout=300).read()
    return raw, (time.perf_counter() - started) * 1000


def seed(token, categories, count, intervals, name_length=20):
    today = datetime.date.today()
    statuses = []
    for i in range(count):
        status, _ = add_subscription(
            token, categories[i % len(categories)], f"Tjänst {i} ".ljust(name_length, "x"), 49 + i % 7,
            intervals[i % len(intervals)], (today + datetime.timedelta(days=i % 25)).isoformat(),
            start=(today - datetime.timedelta(days=200)).isoformat())
        statuses.append(status)
    return statuses


print("== Realistisk användare: 24 prenumerationer med blandade intervall")
token, categories = register()
seed(token, categories, 24, ["Monthly", "Monthly", "Yearly", "Weekly", "Quarterly", "Monthly"])
for months in (6, 12, 24):
    for suffix, label in (("", "standard"), ("&includePayments=true", "med betalningslista")):
        raw, ms = fetch(token, f"/api/forecast?months={months}{suffix}")
        payments = sum(len(m["payments"] or []) for m in json.loads(raw)["months"])
        print(f"  months={months:2} {label:20} {len(raw) / 1024:8.1f} KiB  {payments:5} betalningar  {ms:6.1f} ms")

attempts = int(sys.argv[1]) if len(sys.argv) > 1 else 1000
print(f"\n== Missbruk: försöker skapa {attempts} veckoprenumerationer på ett konto")
token, categories = register()
started = time.perf_counter()
statuses = seed(token, categories, attempts, ["Weekly"], name_length=100)
print(f"  {attempts} anrop på {time.perf_counter() - started:.1f} s: 201 x{statuses.count(201)}, 409 x{statuses.count(409)}, övrigt {len(statuses) - statuses.count(201) - statuses.count(409)}")
for suffix, label in (("", "standard"), ("&includePayments=true", "med betalningslista")):
    times = []
    for _ in range(3):
        raw, ms = fetch(token, f"/api/forecast?months=24{suffix}")
        times.append(ms)
    print(f"  months=24 {label:20} {statuses.count(201)} prenumerationer: {len(raw) / 1024 / 1024:7.2f} MiB, median {statistics.median(times):6.0f} ms")
