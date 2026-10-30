#!/usr/bin/env python3
"""Local-only, synthetic-data SQL Server persistence and concurrency integration test.

Only invoked by the GitHub Actions ephemeral SQL Server job. Writes no personal
information to a remote API; deliberately refuses non-loopback hosts.
"""

import json
import os
import secrets
import sys
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta, timezone
from pathlib import Path

from ci_http_smoke import expect, register_patient, request

STATE = os.environ.get("DARECARE_SQLSERVER_STATE_PATH")
if not STATE or not Path(STATE).is_absolute():
    raise SystemExit("DARECARE_SQLSERVER_STATE_PATH must be an absolute CI temp path")


def assert_sql_server():
    health = expect("GET", "/health", 200)
    if health.get("databaseProvider") != "SqlServer":
        raise AssertionError("Refusing to run persistence test against non-SQL Server")
    expect("GET", "/health/ready", 200)


def seed():
    assert_sql_server()
    email, password, patient = register_patient("sql-persistence")
    address = expect("POST", "/api/v1/patients/me/addresses", 201, {
        "label": "CI home", "city": "Mansoura",
        "fullAddress": "Synthetic CI address, building 12",
        "isDefault": True,
    }, bearer=patient["accessToken"])
    expect("GET", "/api/v1/patients/me/addresses/" + address["id"],
           200, bearer=patient["accessToken"])

    # The two simultaneous POST requests must not create overlapping slots
    # for the same nurse. This exercises SQL Server serializable isolation.
    nurse_email = f"ci-nurse-{secrets.token_hex(8)}@example.test"
    nurse_password = "OnlySyntheticTest!2026NursePwd"
    nurse = expect("POST", "/api/v1/auth/register/nurse", 200, {
        "fullName": "Synthetic CI Nurse", "email": nurse_email,
        "password": nurse_password, "phoneNumber": "+201099991112",
        "licenseNumber": "CI-" + secrets.token_hex(10),
        "yearsOfExperience": 4,
    })
    nurse_token = nurse["accessToken"]
    nurse_profile = expect("GET", "/api/v1/nurses/me", 200, bearer=nurse_token)
    start = (datetime.now(timezone.utc) + timedelta(hours=4)).replace(microsecond=0)
    slots = [
        {"startsAt": start.isoformat(),
         "endsAt": (start + timedelta(hours=1)).isoformat()},
        {"startsAt": (start + timedelta(minutes=30)).isoformat(),
         "endsAt": (start + timedelta(hours=1, minutes=30)).isoformat()},
    ]

    def create_slot(body):
        return request("POST", "/api/v1/nurses/me/slots", body, bearer=nurse_token)

    with ThreadPoolExecutor(max_workers=2) as pool:
        responses = list(pool.map(create_slot, slots))
    statuses = sorted(item[0] for item in responses)
    if statuses != [201, 409]:
        raise AssertionError(f"Concurrent overlapping nurse slots: HTTP results {statuses}")
    created = next(value for status, value in responses if status == 201)
    all_slots = expect("GET", "/api/v1/nurses/me/slots", 200, bearer=nurse_token)
    matching_slots = [slot for slot in all_slots if slot["id"] == created["id"]]
    if len(matching_slots) != 1:
        raise AssertionError("Created nurse availability slot was not persisted")

    state = {
        "email": email, "password": password, "patientId": patient["userId"],
        "addressId": address["id"], "nurseEmail": nurse_email,
        "nursePassword": nurse_password, "nurseId": nurse_profile["id"],
        "slotId": created["id"],
    }
    path = Path(STATE)
    path.write_text(json.dumps(state), encoding="utf-8")
    path.chmod(0o600)
    print("PASS SQL Server initial patient address and serializable nurse slot race")


def verify():
    assert_sql_server()
    state = json.loads(Path(STATE).read_text(encoding="utf-8"))
    logged_in = expect("POST", "/api/v1/auth/login", 200, {
        "email": state["email"], "password": state["password"],
    })
    if logged_in["userId"].lower() != state["patientId"].lower():
        raise AssertionError("Persisted patient identity has changed")
    address = expect("GET",
                     "/api/v1/patients/me/addresses/" + state["addressId"],
                     200, bearer=logged_in["accessToken"])
    if address["city"] != "Mansoura":
        raise AssertionError("Persisted patient address changed after restart")

    nurse = expect("POST", "/api/v1/auth/login", 200, {
        "email": state["nurseEmail"], "password": state["nursePassword"],
    })
    nurse_profile = expect("GET", "/api/v1/nurses/me", 200,
                           bearer=nurse["accessToken"])
    if nurse_profile["id"].lower() != state["nurseId"].lower():
        raise AssertionError("Persisted nurse identity has changed")
    slots = expect("GET", "/api/v1/nurses/me/slots", 200,
                   bearer=nurse["accessToken"])
    if sum(s["id"] == state["slotId"] for s in slots) != 1:
        raise AssertionError("Nurse slot was lost on API restart")
    print("PASS SQL Server durable patient, nurse and availability after app restart")


if __name__ == "__main__":
    if len(sys.argv) != 2 or sys.argv[1] not in ("seed", "verify"):
        raise SystemExit("Usage: ci_sqlserver_persistence.py seed|verify")
    try:
        {"seed": seed, "verify": verify}[sys.argv[1]]()
    except (AssertionError, KeyError, ValueError, TypeError) as exc:
        print(f"FAIL SQL Server CI integration: {exc}", file=sys.stderr)
        sys.exit(1)
