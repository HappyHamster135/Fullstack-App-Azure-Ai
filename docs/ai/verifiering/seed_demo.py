"""Skapar demokontot demo@example.com (lösenord Lösenord123) med 14 prenumerationer för skärmdumpar och webbläsartester."""
import datetime

from api import add_subscription, call

email, password = "demo@example.com", "Lösenord123"
status, body, _ = call("POST", "/api/auth/register", {"email": email, "password": password})
if status == 409:
    status, body, _ = call("POST", "/api/auth/login", {"email": email, "password": password})
token = body["token"]
_, categories, _ = call("GET", "/api/categories", token=token)
category = {c["name"]: c for c in categories}

if not call("GET", "/api/subscriptions", token=token)[1]:
    today = datetime.date.today()
    plus = lambda days: (today + datetime.timedelta(days=days)).isoformat()
    demo = [
        ("Netflix", 149, "Monthly", plus(8), "Streaming", True),
        ("Spotify", 119, "Monthly", plus(3), "Musik", True),
        ("Disney+", 109, "Monthly", plus(16), "Streaming", True),
        ("HBO Max", 99, "Monthly", plus(26), "Streaming", True),
        ("Microsoft 365", 995, "Yearly", plus(42), "Mjukvara", True),
        ("Adobe Creative Cloud", 249, "Monthly", plus(24), "Mjukvara", True),
        ("iCloud 200 GB", 39, "Monthly", plus(14), "Mjukvara", True),
        ("Dropbox", 1099, "Yearly", plus(120), "Mjukvara", True),
        ("Xbox Game Pass", 129, "Monthly", plus(5), "Spel", True),
        ("Dagens Nyheter", 149, "Monthly", plus(21), "Nyheter & media", True),
        ("Tidskriften Ny Teknik", 449, "Quarterly", plus(58), "Nyheter & media", True),
        ("Gymkort", 399, "Monthly", plus(-3), "Övrigt", True),
        ("Matkasse", 249, "Weekly", plus(2), "Övrigt", True),
        ("Gammal tidning (pausad)", 89, "Monthly", plus(10), "Nyheter & media", False),
    ]
    for name, price, interval, next_date, cat, active in demo:
        status, response = add_subscription(token, category[cat], name, price, interval, next_date, start=plus(-300), active=active)
        assert status == 201, (status, response)

print("demo klar:", len(call("GET", "/api/subscriptions", token=token)[1]), "prenumerationer")
