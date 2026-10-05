"""Små hjälpfunktioner för verifieringsskripten: anrop mot det riktiga API:t, registrering och skapande av prenumerationer."""
import datetime
import json
import os
import urllib.error
import urllib.request
import uuid

BASE = os.environ.get("SUBTRACKER_API", "http://localhost:5077")


def call(method, path, body=None, token=None):
    """Returnerar (statuskod, json eller text, sekunder)."""
    import time

    data = json.dumps(body).encode() if body is not None else None
    request = urllib.request.Request(BASE + path, data=data, method=method)
    request.add_header("Content-Type", "application/json")
    if token:
        request.add_header("Authorization", "Bearer " + token)
    started = time.perf_counter()
    try:
        with urllib.request.urlopen(request, timeout=300) as response:
            raw = response.read().decode()
            return response.status, (json.loads(raw) if raw else None), time.perf_counter() - started
    except urllib.error.HTTPError as error:
        raw = error.read().decode()
        try:
            parsed = json.loads(raw) if raw else None
        except ValueError:
            parsed = raw
        return error.code, parsed, time.perf_counter() - started


def register(email=None, password="Lösenord123"):
    """Registrerar en ny användare och returnerar (token, kategorier)."""
    email = email or f"{uuid.uuid4().hex}@example.com"
    status, body, _ = call("POST", "/api/auth/register", {"email": email, "password": password})
    assert status == 201, (status, body)
    token = body["token"]
    _, categories, _ = call("GET", "/api/categories", token=token)
    return token, categories


def add_subscription(token, category, name, price, interval, next_date, start=None, active=True):
    """Skapar en prenumeration. Datumen anges som ISO-strängar och måste ligga inom 2000-01-01..2100-12-31."""
    status, body, _ = call("POST", "/api/subscriptions", {
        "name": name, "price": price, "billingInterval": interval,
        "startDate": start or next_date, "nextPaymentDate": next_date,
        "isActive": active, "categoryId": category["id"]}, token)
    return status, body


def swedish_today():
    from zoneinfo import ZoneInfo

    return datetime.datetime.now(ZoneInfo("Europe/Stockholm")).date()
