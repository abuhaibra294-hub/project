#!/usr/bin/env python3
"""End-to-end DareCare demo onboarding and booking through REAL HTTP.

Safety: refuses any host other than loopback HTTP. Uses a disposable Docker
database with synthetic nurse, patient, and CI-only admin accounts.
"""
import json
import os
import secrets
import sys
from datetime import datetime, timedelta, timezone
from urllib.error import HTTPError
from urllib.parse import urlencode, urlsplit
from urllib.request import Request, urlopen

BASE = os.environ.get("DARECARE_API_BASE_URL", "http://127.0.0.1:18080").rstrip("/")
parts = urlsplit(BASE)
if (parts.scheme != "http" or parts.hostname not in ("localhost", "127.0.0.1", "::1")
        or parts.username or parts.password or parts.path not in ("", "/")):
    raise SystemExit("Safety guard: this write test may run ONLY on loopback HTTP.")

def call(method, path, payload=None, token=None, expected=200):
    headers = {"Accept": "application/json"}
    if token:
        headers["Authorization"] = "Bearer " + token
    body = None
    if payload is not None:
        body = json.dumps(payload).encode()
        headers["Content-Type"] = "application/json"
    req = Request(BASE + path, method=method, data=body, headers=headers)
    try:
        with urlopen(req, timeout=25) as response:
            status, raw = response.status, response.read()
    except HTTPError as exc:
        status, raw = exc.code, exc.read()
    if status != expected:
        raise AssertionError(f"{method} {path}: expected {expected}, received {status}")
    return json.loads(raw) if raw else None

def register(role):
    suffix = secrets.token_hex(7)
    payload = {
        "fullName": f"Synthetic {role} {suffix}",
        "email": f"ci-{role.lower()}-{suffix}@example.test",
        "password": "OnlySynthetic!2026Pwd",
        "phoneNumber": "+201099991111",
    }
    if role == "Nurse":
        payload.update(licenseNumber=f"CI-NURSE-{suffix}", yearsOfExperience=5)
    response = call("POST", "/api/v1/auth/register/" + role.lower(), payload)
    assert role in response["roles"] and response["accessToken"], "wrong registration role"
    return response["accessToken"]

def test():
    admin_email = os.environ["DARECARE_CI_ADMIN_EMAIL"]
    admin_pwd = os.environ["DARECARE_CI_ADMIN_PASSWORD"]
    skills = call("GET", "/api/v1/skills")
    services = call("GET", "/api/v1/services")
    assert skills and services, "seed skills/services missing"
    service = services[0]
    skill_id = service["requiredSkillId"]

    nurse_token = register("Nurse")
    profile = call("GET", "/api/v1/nurses/me", token=nurse_token)
    nurse_id = profile["id"]
    assert not profile["isApproved"], "new nurse should await human admin approval"
    call("PUT", "/api/v1/nurses/me", {
        "bio": "Synthetic nurse — automated integration test only",
        "city": "Mansoura", "serviceArea": "Mansoura",
        "yearsOfExperience": 5, "hourlyRate": 350
    }, token=nurse_token)
    call("PUT", "/api/v1/nurses/me/skills", {
        "skillIds": [skill_id]
    }, token=nurse_token)

    search = "/api/v1/nurses/search?" + urlencode({"skillId": skill_id})
    before = call("GET", search)
    assert all(x["id"] != nurse_id for x in before["items"]), "unapproved nurse leaked into public search"

    admin = call("POST", "/api/v1/auth/login", {
        "email": admin_email, "password": admin_pwd
    })
    assert "Admin" in admin["roles"], "CI bootstrap admin absent"
    admin_token = admin["accessToken"]
    pending = call("GET", "/api/v1/admin/nurses/pending", token=admin_token)
    assert any(x["id"] == nurse_id for x in pending), "nurse missing from pending approval list"
    call("PATCH", f"/api/v1/admin/nurses/{nurse_id}/approval",
         {"approved": True}, admin_token)

    approved = call("GET", search)
    assert any(x["id"] == nurse_id for x in approved["items"]), "approved nurse missing from search"
    now = datetime.now(timezone.utc)
    start = (now + timedelta(hours=3)).isoformat().replace("+00:00", "Z")
    end = (now + timedelta(hours=5)).isoformat().replace("+00:00", "Z")
    slot = call("POST", "/api/v1/nurses/me/slots", {
        "startsAt": start, "endsAt": end
    }, token=nurse_token, expected=201)
    visible_slots = call("GET", f"/api/v1/nurses/{nurse_id}/slots")
    assert any(x["id"] == slot["id"] for x in visible_slots), "free slot missing"

    patient_token = register("Patient")
    call("POST", f"/api/v1/bookings/{secrets.token_hex(16)}/accept",
         token=patient_token, expected=404)  # no privilege escalation or data leak
    booked = call("POST", "/api/v1/bookings", {
        "nurseProfileId": nurse_id, "availabilitySlotId": slot["id"],
        "careServiceId": service["id"], "serviceAddress": "Mansoura Synthetic Street 123"
    }, token=patient_token, expected=201)
    booking_id = booked["id"]
    assert booked["status"] == "Pending", "new booking should be pending"
    patient_bookings = call("GET", "/api/v1/bookings/mine", token=patient_token)
    assert any(x["id"] == booking_id for x in patient_bookings), "patient dashboard lost booking"
    nurse_bookings = call("GET", "/api/v1/bookings/mine", token=nurse_token)
    assert any(x["id"] == booking_id for x in nurse_bookings), "nurse inbox lost booking"

    call("POST", f"/api/v1/bookings/{booking_id}/accept", token=patient_token, expected=403)
    call("POST", f"/api/v1/bookings/{booking_id}/accept", token=nurse_token)
    call("POST", f"/api/v1/bookings/{booking_id}/start", token=nurse_token)
    call("POST", f"/api/v1/bookings/{booking_id}/complete", token=nurse_token)
    completed = call("GET", f"/api/v1/bookings/{booking_id}", token=patient_token)
    assert completed["status"] == "Completed", "booking did not complete"
    dashboard = call("GET", "/api/v1/dashboard/me", token=patient_token)
    assert dashboard["completedVisits"] >= 1, "dashboard did not count completed booking"
    print("PASS synthetic REAL HTTP: nurse registration -> profile -> skills -> admin approval "
          "-> public discovery -> future slot -> patient booking -> accept/start/complete -> dashboard")

if __name__ == "__main__":
    try:
        test()
    except (AssertionError, KeyError, TypeError, ValueError, OSError) as exc:
        # Never log passwords, tokens or payloads from a failing request.
        print(f"FAIL isolated DareCare onboarding/booking E2E: {exc}", file=sys.stderr)
        raise SystemExit(1)
