#!/usr/bin/env python3
"""Read-only proof of which DareCare commit is ACTUALLY running on Render.

No accounts, documents, tokens or other service state are modified.
Use HTTPS *.onrender.com or an HTTP loopback address for the Docker CI test.
"""

import argparse
import json
import re
import sys
import time
from urllib.error import HTTPError, URLError
from urllib.parse import urlsplit
from urllib.request import Request, urlopen


def read_json(base_url, path):
    req = Request(base_url + path, headers={
        "User-Agent": "DareCare-release-verifier/1.0",
        "Accept": "application/json"
    }, method="GET")
    with urlopen(req, timeout=20) as response:
        if response.status != 200:
            raise ValueError(f"{path} returned HTTP {response.status}")
        return json.loads(response.read(64 * 1024))


def verify(base_url, expected_commit, retries, delay):
    parsed = urlsplit(base_url)
    host = (parsed.hostname or "").lower()
    if (parsed.scheme != "https" or not host.endswith(".onrender.com")) and not (
        parsed.scheme == "http" and host in ("127.0.0.1", "localhost", "::1")
    ):
        raise ValueError("Only HTTPS *.onrender.com and HTTP loopback URLs are supported.")
    if parsed.username or parsed.password or parsed.path not in ("", "/") or (
        parsed.query or parsed.fragment
    ):
        raise ValueError("Provide an origin URL only, with no path, query or credentials.")
    if not re.fullmatch(r"[0-9a-fA-F]{40}", expected_commit):
        raise ValueError("Expected GitHub commit must be a 40-character hex SHA.")

    url = base_url.rstrip("/")
    expected = expected_commit.lower()
    last_error = None
    for attempt in range(retries):
        try:
            health = read_json(url, "/health")
            ready = read_json(url, "/health/ready")
            if health.get("service") != "DareCare.Api" or ready.get("status") != "ready":
                raise ValueError("Unexpected service identity or database not ready.")
            live = health.get("deployedCommit")
            if not isinstance(live, str) or not re.fullmatch(r"[0-9a-fA-F]{40}", live):
                raise ValueError("Live API has no deployedCommit; likely an older deployment.")
            if live.lower() == expected:
                print("PASS Render is ready and running expected commit " + expected)
                return
            raise ValueError(
                "STALE deployment: live commit " + live.lower() +
                " differs from expected GitHub commit " + expected
            )
        except (URLError, HTTPError, TimeoutError, ValueError, json.JSONDecodeError) as exc:
            last_error = str(exc)
            if attempt + 1 < retries:
                time.sleep(delay)
    raise ValueError(last_error or "Render deployment verification did not complete.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-url", required=True)
    parser.add_argument("--expected-commit", required=True)
    parser.add_argument("--retries", type=int, default=3)
    parser.add_argument("--delay", type=int, default=5)
    args = parser.parse_args()
    if args.retries not in range(1, 11) or args.delay not in range(0, 61):
        parser.error("--retries must be 1-10 and --delay must be 0-60.")
    try:
        verify(args.base_url, args.expected_commit, args.retries, args.delay)
    except (ValueError, URLError, HTTPError) as exc:
        print("FAIL DareCare release verification: " + str(exc), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
