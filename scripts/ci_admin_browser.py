#!/usr/bin/env python3
"""Playwright RTL admin portal QA with synthetic, role-aware API fixture.

No public Render writes; all login credentials and records below are test-only.
Actual ASP.NET Admin API auth/role middleware is covered by Docker HTTP smoke.
"""
import json
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit

from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[1] / "src" / "DareCare.Api" / "wwwroot"
ARTIFACTS = Path(__file__).resolve().parents[1] / "artifacts"
ARTIFACTS.mkdir(exist_ok=True)

TEST = {"nurseApproved": False, "pharmacyApproved": False,
        "patientActive": True, "calls": [], "unsafeAdminRequests": 0}
ADMIN = {"accessToken": "tok-admin", "refreshToken": "ref-admin",
         "roles": ["Admin"], "userId": "u-admin"}
PATIENT = {"accessToken": "tok-patient", "refreshToken": "ref-patient",
           "roles": ["Patient"], "userId": "u-patient"}


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *_):
        pass

    def output(self, status=200, payload=None, content_type="application/json"):
        self.send_response(status)
        if status == 204:
            self.end_headers()
            return
        raw = json.dumps(payload, ensure_ascii=False).encode()
        self.send_header("Content-Type", content_type + "; charset=utf-8")
        self.send_header("Content-Length", str(len(raw)))
        self.end_headers()
        self.wfile.write(raw)

    def token_role(self):
        value = self.headers.get("Authorization", "")
        return value.removeprefix("Bearer tok-") if value.startswith("Bearer tok-") else ""

    def do_GET(self):
        path = urlsplit(self.path).path
        static = {
            "/admin": ("darecare-admin.html", "text/html"),
            "/admin/login": ("darecare-admin.html", "text/html"),
            "/assets/darecare-admin.css": ("darecare-admin.css", "text/css"),
            "/assets/darecare-admin.js": ("darecare-admin.js", "text/javascript")
        }
        if path in static:
            filename, mime = static[path]
            raw = (ROOT / filename).read_bytes()
            self.send_response(200)
            self.send_header("Content-Type", mime+"; charset=utf-8")
            self.send_header("Content-Length", str(len(raw)))
            self.end_headers()
            self.wfile.write(raw)
            return
        role = self.token_role()
        if path == "/api/v1/auth/me":
            if role not in ("patient", "admin"):
                return self.output(401, {"message": "Unauthorized"})
            return self.output(200, {
                "id": "u-"+role, "fullName": "مدير النظام" if role == "admin" else "مريض",
                "email": "ci-"+role+"@example.test",
                "roles": ["Admin"] if role == "admin" else ["Patient"]
            })
        if path.startswith("/api/v1/admin/") and role != "admin":
            TEST["unsafeAdminRequests"] += 1
            return self.output(403 if role else 401, {"message": "Forbidden"})

        if path == "/api/v1/admin/overview":
            return self.output(200, {
                "activePatients": int(TEST["patientActive"]),
                "nursesPending": int(not TEST["nurseApproved"]),
                "nursesApproved": int(TEST["nurseApproved"]),
                "pharmaciesPending": int(not TEST["pharmacyApproved"]),
                "pharmaciesApproved": int(TEST["pharmacyApproved"]),
                "prescriptions": 0,
                "bookings": [{"status":"Pending", "count":2},
                             {"status":"Completed", "count":1}],
                "orders": [{"status":"Quoted", "count":1}]
            })
        if path == "/api/v1/admin/nurses/pending":
            return self.output(200, [] if TEST["nurseApproved"] else [{
                "id":"nurse1","fullName":"ممرض تجريبي","city":"Mansoura",
                "licenseNumber":"SYNTHETIC-001","email":"ci-nurse@example.test",
                "skills":["Elderly Care"],"yearsOfExperience":3
            }])
        if path == "/api/v1/admin/pharmacies/pending":
            return self.output(200, [] if TEST["pharmacyApproved"] else [{
                "id":"pharmacy1","businessName":"صيدلية تجريبية","city":"Mansoura",
                "licenseNumber":"SYNTHETIC-PHARMACY","email":"ci-pharmacy@example.test",
                "hasAcceptedInvitation":True
            }])
        if path == "/api/v1/admin/users":
            return self.output(200, [
                {"id":"patient1","fullName":"مريض تجريبي","email":"ci-patient@example.test",
                 "roles":["Patient"],"isActive":TEST["patientActive"],"isPendingInvitation":False},
                {"id":"admin1","fullName":"مدير النظام","email":"ci-admin@example.test",
                 "roles":["Admin"],"isActive":True,"isPendingInvitation":False}
            ])
        if path == "/api/v1/admin/audit":
            return self.output(200, [
                {"id":"audit1","action":"nurse.approved","entityType":"NurseProfile",
                 "entityId":"nurse1","occurredAtUtc":"2026-11-16T10:00:00Z"}
            ])
        return self.output(404, {"message":"Unmocked GET " + path})

    def do_POST(self):
        self.mutate("POST")

    def do_PATCH(self):
        self.mutate("PATCH")

    def mutate(self, method):
        path = urlsplit(self.path).path
        length = int(self.headers.get("Content-Length", "0"))
        body = self.rfile.read(length)
        payload = json.loads(body) if body else {}
        TEST["calls"].append((method, path))
        if path == "/api/v1/auth/login":
            if payload.get("email") == "ci-admin@example.test":
                return self.output(200, ADMIN)
            if payload.get("email") == "ci-patient@example.test":
                return self.output(200, PATIENT)
            return self.output(401, {"message": "Invalid credentials"})
        if path == "/api/v1/auth/logout":
            return self.output(204)
        if self.token_role() != "admin":
            TEST["unsafeAdminRequests"] += 1
            return self.output(403, {"message":"Forbidden"})
        if method == "PATCH" and path == "/api/v1/admin/nurses/nurse1/approval":
            TEST["nurseApproved"] = payload.get("approved") is True
            return self.output(200, {"id":"nurse1","isApproved":TEST["nurseApproved"]})
        if method == "PATCH" and path == "/api/v1/admin/pharmacies/pharmacy1/approval":
            TEST["pharmacyApproved"] = payload.get("approved") is True
            return self.output(200, {"id":"pharmacy1","isApproved":TEST["pharmacyApproved"]})
        if method == "PATCH" and path == "/api/v1/admin/users/patient1/status":
            TEST["patientActive"] = payload.get("isActive") is True
            return self.output(200, {"id":"patient1","isActive":TEST["patientActive"]})
        return self.output(404, {"message":"Unmocked operation " + path})


def main():
    server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    url = "http://127.0.0.1:" + str(server.server_port)
    try:
        with sync_playwright() as playwright:
            browser = playwright.chromium.launch(headless=True)
            page = browser.new_page(viewport={"width":1440,"height":900}, device_scale_factor=1)
            errors = []
            page.on("pageerror", lambda error: errors.append(str(error)))
            page.on("dialog", lambda dialog: dialog.accept())
            page.goto(url + "/admin/login")
            page.locator("#adminLoginForm").wait_for()
            assert page.locator("#adminWorkspace").is_hidden()
            page.screenshot(path=str(ARTIFACTS/"admin-login-desktop.png"),full_page=True)

            # A valid Patient can authenticate with the shared Auth API but must be rejected here.
            page.locator("#adminEmail").fill("ci-patient@example.test")
            page.locator("#adminPassword").fill("SyntheticStrong!Password2026")
            page.locator("#loginSubmit").click()
            page.get_by_text("هذا الحساب لا يملك صلاحية مدير النظام.").wait_for()
            assert page.locator("#adminWorkspace").is_hidden()
            assert TEST["unsafeAdminRequests"] == 0, "non-admin attempted to read protected Admin API"

            # Admin is accepted only after the backend role + /admin/overview authorization checks.
            page.locator("#adminEmail").fill("ci-admin@example.test")
            page.locator("#adminPassword").fill("SyntheticStrong!Password2026")
            page.locator("#loginSubmit").click()
            page.locator("#adminWorkspace").wait_for()
            page.locator(".metric").first.wait_for()
            assert page.locator("#adminPassword").input_value() == ""
            assert page.evaluate("localStorage.length === 0 && sessionStorage.length === 0")
            page.screenshot(path=str(ARTIFACTS/"admin-dashboard-desktop.png"),full_page=True)

            page.locator('[data-section="nurses"]').click()
            page.get_by_role("button", name="اعتماد الممرض").wait_for()
            assert "SYNTHETIC-001" in page.locator("#nurseRequests").inner_text()
            page.get_by_role("button", name="اعتماد الممرض").click()
            page.wait_for_function("() => document.querySelector('#nurseRequests').textContent.includes('لا توجد')")
            assert TEST["nurseApproved"]

            page.locator('[data-section="pharmacies"]').click()
            page.get_by_role("button", name="اعتماد الصيدلية").wait_for()
            page.get_by_role("button", name="اعتماد الصيدلية").click()
            page.wait_for_function("() => document.querySelector('#pharmacyRequests').textContent.includes('لا توجد')")
            assert TEST["pharmacyApproved"]

            page.locator('[data-section="users"]').click()
            page.get_by_role("button", name="إيقاف الحساب").wait_for()
            assert page.get_by_role("button", name="إيقاف الحساب").count() == 1
            page.get_by_role("button", name="إيقاف الحساب").click()
            page.get_by_role("button", name="تفعيل الحساب").wait_for()
            assert not TEST["patientActive"]

            page.locator('[data-section="audit"]').click()
            page.get_by_text("nurse.approved").wait_for()

            page.set_viewport_size({"width":390,"height":844})
            page.locator('[data-section="overview"]').click()
            page.locator(".metric").first.wait_for()
            page.screenshot(path=str(ARTIFACTS/"admin-dashboard-mobile.png"),full_page=True)
            overflow=page.evaluate("document.documentElement.scrollWidth-window.innerWidth")
            assert overflow <= 2, f"horizontal overflow by {overflow}px at 390px"

            page.locator("#adminLogout").click()
            page.locator("#adminLoginForm").wait_for()
            assert page.locator("#adminWorkspace").is_hidden()
            assert page.locator("#adminPassword").input_value() == ""
            page.screenshot(path=str(ARTIFACTS/"admin-login-mobile.png"),full_page=True)
            assert not errors, "Browser JavaScript errors: " + repr(errors)
            browser.close()
        print("PASS REAL CHROMIUM: admin alias, Patient role rejection, live overview, nurse/pharmacy approval, user status, audit, logout, 390px RTL")
    finally:
        server.shutdown()
        server.server_close()


if __name__ == "__main__":
    main()
