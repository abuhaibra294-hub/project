#!/usr/bin/env python3
"""Read-only external DareCare staging smoke/contract check; no test writes or credentials.

Run only against an explicitly selected public Render HTTPS URL.
Never treats GitHub CI success as proof of a completed user workflow.
"""
import argparse
import json
import re
import sys
import time
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.parse import urlsplit
from urllib.request import Request, urlopen

METHODS = {"get", "post", "put", "patch", "delete"}


def fetch(base, path, method="GET", headers=None, timeout=30):
    request = Request(
        base + path,
        headers={"Accept": "application/json", "User-Agent": "DareCare-staging-readonly/1.0", **(headers or {})},
        method=method,
    )
    try:
        with urlopen(request, timeout=timeout) as response:
            return response.status, response.read(4 * 1024 * 1024)
    except HTTPError as response:
        return response.code, response.read(16 * 1024)


def read_json(base, path, expected, timeout=30):
    status, body = fetch(base, path, timeout=timeout)
    if status != expected:
        raise ValueError(f"GET {path}: expected HTTP {expected}, received {status}")
    try:
        return json.loads(body)
    except (ValueError, UnicodeDecodeError) as exc:
        raise ValueError(f"GET {path}: invalid JSON") from exc


def check(base, manifest_path, attempts, interval, timeout, origin=None):
    url = urlsplit(base)
    if (url.scheme != "https" or not (url.hostname or "").endswith(".onrender.com")
            or url.username or url.password or url.path not in ("", "/")
            or url.query or url.fragment):
        raise ValueError("Only an HTTPS *.onrender.com origin without path/credentials is allowed.")
    base = base.rstrip("/")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    expected = {(ep["path"], ep["method"].lower()) for ep in manifest["endpoints"]}
    if not expected or len(expected) != manifest["count"]:
        raise ValueError("Local endpoint manifest is inconsistent.")

    last_error = None
    for n in range(1, attempts + 1):
        try:
            health = read_json(base, "/health", 200, timeout)
            ready = read_json(base, "/health/ready", 200, timeout)
            if health.get("service") != "DareCare.Api" or ready.get("status") != "ready":
                raise ValueError("Health JSON does not identify ready DareCare.Api.")
            print(f"[PASS] Healthy DareCare API; databaseProvider={health.get('databaseProvider', 'unknown')}")
            live_sha = health.get("deployedCommit")
            if isinstance(live_sha, str) and re.fullmatch(r"[a-fA-F0-9]{40}", live_sha):
                print(f"[INFO] Deployed Git commit: {live_sha}")
            else:
                print("[NOT VERIFIED] Deployed Git commit not exposed by /health.")

            spec = read_json(base, "/openapi/v1.json", 200, timeout)
            paths = spec.get("paths")
            if not isinstance(paths, dict):
                raise ValueError("Live OpenAPI has no valid paths mapping.")
            actual = {
                (re.sub(r":guid(?=})", "", path), method.lower())
                for path, item in paths.items()
                if isinstance(item, dict)
                for method in item.keys()
                if method.lower() in METHODS
            }
            missing, extra = expected - actual, actual - expected
            if missing:
                print(f"[FAIL] Missing {len(missing)} source-defined API operations:")
                print("  Missing (first 20): " + repr(sorted(missing)[:20]))
                raise ValueError("Live OpenAPI is missing expected source-derived API operations.")
            print(f"[PASS] Live OpenAPI contains all {len(expected)} expected HTTP operations.")
            if extra:
                print(f"[INFO] Live OpenAPI exposes {len(extra)} additional operations; review separately.")

            public_tests = (
                ("/api/v1/skills", 200),
                ("/api/v1/nurses/search?page=1&pageSize=5", 200),
                ("/api/v1/auth/me", 401),
                ("/api/v1/admin/overview", 401),
            )
            for path, expected_status in public_tests:
                status, _ = fetch(base, path, timeout=timeout)
                if status != expected_status:
                    raise ValueError(f"GET {path}: expected {expected_status}, got {status}")
                print(f"[PASS] GET {path}: {status}")

            if origin:
                o = urlsplit(origin)
                if o.scheme != "https" or not o.netloc or o.path not in ("", "/") or o.query or o.fragment:
                    raise ValueError("Frontend origin must be an HTTPS origin.")
                headers = {"Origin": origin.rstrip("/"), "Access-Control-Request-Method": "GET"}
                req = Request(base + "/api/v1/nurses/search", headers=headers, method="OPTIONS")
                try:
                    with urlopen(req, timeout=timeout) as response:
                        status = response.status
                        allowed = response.headers.get("Access-Control-Allow-Origin")
                except HTTPError as response:
                    status = response.code
                    allowed = response.headers.get("Access-Control-Allow-Origin")
                if status not in (200, 204) or allowed != origin.rstrip("/"):
                    raise ValueError(f"CORS preflight failed: status={status}, allow-origin={allowed!r}")
                print("[PASS] Browser preflight matches exact HTTPS frontend origin.")
            print("[PASS] READ-ONLY staging probe. Authenticated E2E and browser CORS still require separate tests.")
            return 0
        except (URLError, OSError, ValueError, TimeoutError) as exc:
            last_error = exc
            print(f"[RETRY {n}/{attempts}] {exc}", file=sys.stderr)
            if n < attempts:
                time.sleep(interval)
    raise ValueError(f"Staging probe never passed: {last_error}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-url", required=True)
    parser.add_argument("--manifest", type=Path, default=Path("integration/DareCare_API_Endpoints.json"))
    parser.add_argument("--attempts", type=int, default=4)
    parser.add_argument("--interval", type=int, default=20)
    parser.add_argument("--timeout", type=int, default=30)
    parser.add_argument("--origin", default=None)
    args = parser.parse_args()
    if not 1 <= args.attempts <= 10 or not 0 <= args.interval <= 60 or not 5 <= args.timeout <= 120:
        parser.error("Invalid attempts, interval, or timeout.")
    try:
        return check(args.base_url, args.manifest, args.attempts, args.interval, args.timeout, args.origin)
    except (ValueError, OSError) as exc:
        print(f"[FAIL] {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
