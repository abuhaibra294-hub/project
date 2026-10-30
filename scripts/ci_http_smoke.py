#!/usr/bin/env python3
"""Synthetic, loopback-only API smoke test for the real Render Docker image.

Uses the public HTTP middleware and a temporary SQLite database. Never run this
against a live service or with real patient credentials.
"""

import json
import os
import secrets
import sys
from urllib.error import HTTPError, URLError
from urllib.parse import urlsplit
from urllib.request import Request, urlopen

BASE = os.environ.get("DARECARE_API_BASE_URL", "http://127.0.0.1:18080").rstrip("/")
address = urlsplit(BASE)
if address.scheme != "http" or address.hostname not in ("localhost", "127.0.0.1", "::1"):
    raise SystemExit("Safety guard: this synthetic write test is loopback HTTP only.")


def request(method, path, payload=None, bearer=None):
    headers = {"Accept": "application/json"}
    body = None
    if payload is not None:
        body = json.dumps(payload).encode("utf-8")
        headers["Content-Type"] = "application/json"
    if bearer:
        headers["Authorization"] = "Bearer " + bearer
    req = Request(BASE + path, data=body, headers=headers, method=method)
    try:
        with urlopen(req, timeout=15) as response:
            status = response.status
            data = response.read()
    except HTTPError as exc:
        status = exc.code
        data = exc.read()
    except URLError as exc:
        raise AssertionError(f"{method} {path}: API unavailable: {exc.reason}") from exc
    try:
        decoded = json.loads(data) if data else None
    except (json.JSONDecodeError, UnicodeDecodeError):
        decoded = None
    return status, decoded


def expect(method, path, expected, payload=None, bearer=None):
    status, result = request(method, path, payload=payload, bearer=bearer)
    if status != expected:
        # Avoid printing private or bearer-token values in CI logs.
        raise AssertionError(f"{method} {path}: expected HTTP {expected}, received {status}")
    return result


def register_patient(name):
    email = f"ci-{name}-{secrets.token_hex(8)}@example.test"
    password = "OnlySyntheticTest!2026Pwd"
    result = expect("POST", "/api/v1/auth/register/patient", 200, {
        "fullName": "Synthetic " + name,
        "email": email,
        "password": password,
        "phoneNumber": "+201099991111"
    })
    assert result and result.get("accessToken") and result.get("refreshToken"), "missing login tokens"
    assert "Patient" in result.get("roles", []), "unexpected registration role"
    return email, password, result


def main():
    ready = expect("GET", "/health/ready", 200)
    assert ready["status"] == "ready"
    openapi = expect("GET", "/openapi/v1.json", 200)
    assert len(openapi.get("paths", {})) >= 20, "runtime OpenAPI missing routes"
    expect("GET", "/api/v1/auth/me", 401)
    expect("GET", "/api/v1/admin/overview", 401)

    email_a, password_a, a = register_patient("owner")
    _, _, b = register_patient("other")
    token_a, token_b = a["accessToken"], b["accessToken"]
    me = expect("GET", "/api/v1/auth/me", 200, bearer=token_a)
    assert me["id"].lower() == a["userId"].lower(), "identity mismatch"
    expect("GET", "/api/v1/admin/overview", 403, bearer=token_a)

    address = expect("POST", "/api/v1/patients/me/addresses", 201, {
        "label": "Home",
        "city": "Mansoura",
        "fullAddress": "Synthetic Street 123, Building 4",
        "isDefault": True
    }, bearer=token_a)
    address_path = "/api/v1/patients/me/addresses/" + address["id"]
    expect("GET", address_path, 200, bearer=token_a)
    expect("GET", address_path, 404, bearer=token_b)
    expect("DELETE", address_path, 404, bearer=token_b)

    login = expect("POST", "/api/v1/auth/login", 200,
                   {"email": email_a, "password": password_a})
    rotated = expect("POST", "/api/v1/auth/refresh", 200,
                     {"refreshToken": login["refreshToken"]})
    assert rotated["refreshToken"] != login["refreshToken"], "refresh token not rotated"
    expect("POST", "/api/v1/auth/refresh", 401,
           {"refreshToken": login["refreshToken"]})
    expect("GET", "/api/v1/auth/me", 200, bearer=rotated["accessToken"])
    expect("POST", "/api/v1/auth/logout-all", 204, bearer=rotated["accessToken"])
    expect("GET", "/api/v1/auth/me", 401, bearer=rotated["accessToken"])
    print("PASS real HTTP: readiness, OpenAPI, 401/403, patient signup/login,")
    print("     address ownership, refresh rotation and logout-all invalidation")


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, KeyError, TypeError) as exc:
        print(f"FAIL real HTTP smoke: {exc}", file=sys.stderr)
        sys.exit(1)
