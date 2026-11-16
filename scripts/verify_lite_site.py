#!/usr/bin/env python3
"""Read-only live gate for a deployed DareCare Lite on its existing Render domain."""
import argparse
import json
import sys
import time
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

BASE = "https://project-kv53.onrender.com"

def get(path, limit=100000):
    req = Request(BASE + path, headers={"User-Agent": "DareCare-Lite-live-verifier/1.0"}, method="GET")
    with urlopen(req, timeout=35) as resp:
        content = resp.read(limit)
        return resp.status, resp.headers, content

def check():
    status, _, ready = get("/health/ready")
    if status != 200 or json.loads(ready).get("status") != "ready":
        raise ValueError("Backend readiness is not 200/ready")
    status, hdr, html = get("/")
    if status != 200 or b"darecare-lite.js" not in html or b"DARECARE" not in html:
        raise ValueError("DareCare Lite HTML is not served at root")
    if "connect-src 'self'" not in hdr.get("Content-Security-Policy", ""):
        raise ValueError("Expected same-origin CSP is missing")
    for path, signature in (
        ("/assets/darecare-lite.css", b"root"),
        ("/assets/darecare-lite.js", b"DareCare Lite"),
    ):
        code, _, body = get(path)
        if code != 200 or signature not in body:
            raise ValueError(f"Frontend asset unavailable: {path}")
    status, _, services = get("/api/v1/services")
    if status != 200 or not isinstance(json.loads(services), list):
        raise ValueError("Existing backend /api/v1/services failed")
    try:
        status, _, _ = get("/auth.html")
    except HTTPError as exc:
        status = exc.code
    if status != 404:
        raise ValueError("Development-only auth.html must remain private")
    print("PASS: Live Render same-domain DareCare Lite HTML + CSS + JS + CSP, API ready, private auth lab hidden")

def main():
    p = argparse.ArgumentParser()
    p.add_argument("--attempts", type=int, default=15)
    p.add_argument("--interval", type=int, default=20)
    args = p.parse_args()
    if not 1 <= args.attempts <= 20 or not 0 <= args.interval <= 60:
        p.error("Invalid attempts/interval")
    for n in range(1, args.attempts + 1):
        try:
            check()
            return 0
        except (URLError, HTTPError, OSError, TimeoutError, ValueError, json.JSONDecodeError) as exc:
            print(f"WAIT {n}/{args.attempts}: {exc}", file=sys.stderr, flush=True)
            if n < args.attempts:
                time.sleep(args.interval)
    return 1

if __name__ == "__main__":
    raise SystemExit(main())
