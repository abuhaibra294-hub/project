#!/usr/bin/env python3
"""Read-only public staging verification for the dedicated DareCare Admin aliases.

No credentials, no POST, no users, and no changes to Render's disposable SQLite.
"""
import json
import sys
import time
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

BASE = "https://project-kv53.onrender.com"


def get(path, expected=200):
    req = Request(BASE + path, method="GET", headers={
        "Accept": "*/*", "User-Agent": "DareCare-admin-readonly-qa/1.0"})
    try:
        with urlopen(req, timeout=40) as response:
            status, headers, body = response.status, response.headers, response.read(400000)
    except HTTPError as error:
        status, headers, body = error.code, error.headers, b""
    if status != expected:
        raise ValueError(f"GET {path}: expected {expected}, got HTTP {status}")
    return body.decode("utf-8"), headers


def check():
    health, _ = get("/health/ready")
    if json.loads(health).get("status") != "ready":
        raise ValueError("API or staging database is not ready")
    login, headers = get("/admin")
    alias, _ = get("/admin/login")
    for html in (login, alias):
        for marker in ('id="adminLoginForm"', 'id="adminWorkspace"', "darecare-admin.js"):
            if marker not in html:
                raise ValueError("Admin page/alias is missing " + marker)
    h = headers
    if "no-store" not in h.get("Cache-Control", "").lower():
        raise ValueError("Admin page caching is not disabled")
    if "connect-src 'self'" not in h.get("Content-Security-Policy", ""):
        raise ValueError("Admin CSP is missing same-origin protection")
    if "noindex" not in h.get("X-Robots-Tag", "").lower():
        raise ValueError("Admin login is not marked noindex")
    js, _ = get("/assets/darecare-admin.js")
    for marker in ("roles.includes('Admin')", "/api/v1/admin/overview", "localStorage"):
        if marker not in js:
            raise ValueError("Admin JS missing marker " + marker)
    # localStorage must only occur in an explicit comment asserting no persistence.
    if "localStorage.setItem" in js or "sessionStorage.setItem" in js:
        raise ValueError("Admin tokens must not be saved to browser storage")
    css, _ = get("/assets/darecare-admin.css")
    if ".workspace" not in css or ".login-layout" not in css:
        raise ValueError("Admin responsive styles are not live")
    get("/api/v1/admin/overview", expected=401)
    get("/auth.html", expected=404)
    print("PASS PUBLIC RENDER: /admin + /admin/login alias, Admin-only API 401, CSP, no-store, HTML/CSS/JS and readiness", flush=True)


def main():
    for attempt in range(1, 10):
        try:
            check()
            return 0
        except (URLError, OSError, ValueError, TimeoutError, AssertionError, json.JSONDecodeError) as exc:
            print(f"CHECK {attempt}/9: {exc}", file=sys.stderr, flush=True)
            if attempt < 9:
                time.sleep(25)
    return 1


if __name__ == "__main__":
    sys.exit(main())
