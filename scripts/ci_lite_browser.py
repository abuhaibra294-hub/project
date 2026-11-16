#!/usr/bin/env python3
"""Real Chromium UI smoke with deterministic, in-memory fake API responses.

The real API/database flow is verified separately via ci_onboarding_booking.py.
This test checks browser DOM/interactions and NEVER touches a public website.
"""
import json
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit

from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[1] / "src" / "DareCare.Api" / "wwwroot"
state = {
    "nurse": {"id": "n1", "bio": "", "city": "", "serviceArea": "",
              "yearsOfExperience": 5, "hourlyRate": 0, "isApproved": False, "skills": []},
    "approved": False, "booked": [],
    "calls": [],
}
service = {"id": "s1", "name": "Elderly Home Nursing", "description": "Synthetic care only",
           "durationMinutes": 60, "basePrice": 300, "requiredSkillId": "sk1", "requiredSkill": "Elderly Care"}
nurse_search = {"id": "n1", "fullName": "Synthetic Nurse", "city": "Mansoura",
                "yearsOfExperience": 5, "hourlyRate": 350,
                "skills": [{"skillId": "sk1", "name": "Elderly Care"}]}
slot = {"id": "slot1", "startsAtUtc": "2027-01-03T11:00:00Z",
        "endsAtUtc": "2027-01-03T13:00:00Z"}


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *_):
        pass

    def reply(self, status, data):
        self.send_response(status)
        if status == 204:
            self.end_headers()
            return
        raw = json.dumps(data, ensure_ascii=False).encode()
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(raw)))
        self.end_headers()
        self.wfile.write(raw)

    def token_role(self):
        auth = self.headers.get("Authorization", "")
        return auth.removeprefix("Bearer tok-") if auth.startswith("Bearer tok-") else ""

    def do_GET(self):
        path = urlsplit(self.path).path
        if not path.startswith("/api/v1/"):
            assets = {"/": ("darecare-lite.html", "text/html"),
                      "/assets/darecare-lite.css": ("darecare-lite.css", "text/css"),
                      "/assets/darecare-lite.js": ("darecare-lite.js", "text/javascript")}
            target = assets.get(path)
            if target is None:
                return self.reply(404, {"message": "Not found"})
            body = (ROOT / target[0]).read_bytes()
            self.send_response(200)
            self.send_header("Content-Type", target[1] + "; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
            return

        role = self.token_role()
        nurse = state["nurse"]
        if path == "/api/v1/services":
            return self.reply(200, [service])
        if path == "/api/v1/skills":
            return self.reply(200, [{"id": "sk1", "name": "Elderly Care"}])
        if path == "/api/v1/nurses/search":
            return self.reply(200, {"page": 1, "total": int(state["approved"]),
                                    "items": [nurse_search] if state["approved"] else []})
        if path == "/api/v1/nurses/n1/slots":
            return self.reply(200, [slot])
        if path == "/api/v1/pharmacies/available":
            return self.reply(200, [])
        if path == "/api/v1/auth/me":
            return self.reply(200, {"id": "u1", "fullName": "مستخدم تجريبي", "roles": [role.capitalize()]})
        if path == "/api/v1/dashboard/me":
            data = {
                "nurse": {"role": "Nurse", "pendingRequests": 0, "acceptedVisits": 0,
                          "completedVisits": 0, "upcomingSlots": 0},
                "admin": {"role": "Admin", "totalPatients": 0, "pendingNurses": int(not state["approved"]),
                          "pendingPharmacies": 0, "totalBookings": 0},
                "patient": {"role": "Patient", "pendingBookings": len(state["booked"]),
                            "upcomingVisits": 0, "completedVisits": 0, "completedPharmacyOrders": 0},
            }
            return self.reply(200, data.get(role, {}))
        if path == "/api/v1/nurses/me":
            return self.reply(200, nurse)
        if path == "/api/v1/nurses/me/slots":
            return self.reply(200, [])
        if path == "/api/v1/bookings/mine":
            return self.reply(200, state["booked"])
        if path in ("/api/v1/orders/mine", "/api/v1/prescriptions/mine"):
            return self.reply(200, [])
        if path == "/api/v1/admin/overview":
            return self.reply(200, {"activePatients": 0, "nursesApproved": int(state["approved"]),
                                    "nursesPending": int(not state["approved"]), "pharmaciesApproved": 0,
                                    "pharmaciesPending": 0, "prescriptions": 0})
        if path == "/api/v1/admin/nurses/pending":
            return self.reply(200, [] if state["approved"] else [{
                "id": "n1", "fullName": "Synthetic Nurse", "city": nurse["city"],
                "licenseNumber": "CI-DEMO-LICENSE", "email": "nurse@example.test",
                "skills": [x["name"] for x in nurse["skills"]]
            }])
        return self.reply(404, {"message": "Unexpected test route " + path})

    def do_POST(self):
        self.write_request("POST")

    def do_PUT(self):
        self.write_request("PUT")

    def do_PATCH(self):
        self.write_request("PATCH")

    def write_request(self, method):
        path = urlsplit(self.path).path
        length = int(self.headers.get("Content-Length", "0"))
        raw = self.rfile.read(length)
        payload = json.loads(raw) if raw else {}
        state["calls"].append((method, path, payload))
        if path == "/api/v1/auth/register/nurse":
            assert payload["licenseNumber"] and payload["yearsOfExperience"] >= 0
            return self.reply(200, {"accessToken": "tok-nurse", "refreshToken": "ref-nurse",
                                    "roles": ["Nurse"], "userId": "u-nurse"})
        if path == "/api/v1/auth/login":
            role = "Admin" if payload["email"] == "ci-admin@example.test" else "Patient"
            return self.reply(200, {"accessToken": "tok-" + role.lower(),
                                    "refreshToken": "ref-" + role.lower(),
                                    "roles": [role], "userId": "u-" + role.lower()})
        if path == "/api/v1/auth/logout":
            return self.reply(204, None)
        if path == "/api/v1/nurses/me" and method == "PUT":
            state["nurse"].update(payload)
            return self.reply(200, state["nurse"])
        if path == "/api/v1/nurses/me/skills" and method == "PUT":
            state["nurse"]["skills"] = [{"skillId": x, "name": "Elderly Care"} for x in payload["skillIds"]]
            return self.reply(200, {"skillIds": payload["skillIds"], "isApproved": False})
        if path == "/api/v1/admin/nurses/n1/approval" and method == "PATCH":
            state["approved"] = bool(payload["approved"])
            state["nurse"]["isApproved"] = state["approved"]
            return self.reply(200, {"id": "n1", "isApproved": state["approved"]})
        if path == "/api/v1/bookings" and method == "POST":
            state["booked"].append({"id": "booking1", "serviceName": service["name"],
                                    "status": "Pending", "startsAtUtc": slot["startsAtUtc"],
                                    "serviceAddress": payload["serviceAddress"]})
            return self.reply(201, state["booked"][0])
        return self.reply(404, {"message": "Unexpected test operation " + path})


def check():
    server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
    t = threading.Thread(target=server.serve_forever, daemon=True)
    t.start()
    base = f"http://127.0.0.1:{server.server_port}"
    try:
        with sync_playwright() as runner:
            browser = runner.chromium.launch(headless=True)
            page = browser.new_page(viewport={"width": 1440, "height": 900})
            errors = []
            page.on("pageerror", lambda error: errors.append(str(error)))
            page.on("dialog", lambda dialog: dialog.accept())
            page.goto(base)
            page.locator("#serviceList .card").first.wait_for()
            assert page.locator("#nurseList .empty").is_visible(), "new demo should have no approved nurses"

            page.locator("#authBtn").click()
            page.locator("#nurseMode").click()
            assert page.locator("#licenseField").is_visible()
            for key, value in {
                "fullName": "Synthetic Nurse", "email": "nurse@example.test",
                "phone": "+201099991111", "password": "OnlySynthetic!2026Pwd",
                "licenseNumber": "CI-DEMO-LICENSE", "nurseYears": "5",
            }.items():
                page.locator("#" + key).fill(value)
            page.locator("#authSubmit").click()
            page.locator(".onboarding-panel").wait_for()
            page.locator('.profile-form [name="city"]').fill("Mansoura")
            page.locator('.profile-form [name="serviceArea"]').fill("Mansoura")
            page.locator('.profile-form [name="hourlyRate"]').fill("350")
            page.locator(".profile-form button").click()
            page.wait_for_function("() => document.querySelector('.profile-form button')?.disabled === false")
            page.locator(".skills-form input[type=checkbox]").first.check()
            page.locator(".skills-form button").click()
            page.wait_for_function("() => document.querySelector('.skills-form button')?.disabled === false")
            assert state["nurse"]["city"] == "Mansoura" and len(state["nurse"]["skills"]) == 1

            page.locator("#logoutBtn").click()
            page.locator("#authBtn").wait_for()
            page.locator("#authBtn").click()
            page.locator("#email").fill("ci-admin@example.test")
            page.locator("#password").fill("OnlySynthetic!2026Pwd")
            page.locator("#authSubmit").click()
            page.get_by_role("button", name="اعتماد الممرض").wait_for()
            page.get_by_role("button", name="اعتماد الممرض").click()
            page.wait_for_function("() => !document.querySelector('#accountSections').textContent.includes('اعتماد الممرض')")
            assert state["approved"], "admin approval action not triggered"

            page.locator("#logoutBtn").click()
            page.locator("#serviceList .card button").first.click()
            page.locator("#nurseList .card button").first.wait_for()
            page.locator("#nurseList .card button").first.click()
            page.locator("#slotSelect option").last.wait_for()
            page.locator("#slotSelect").select_option("slot1")
            page.locator("#visitAddress").fill("Mansoura Synthetic Street 123")
            page.locator("#bookingForm button").click()
            assert page.locator("#authDialog").evaluate("(dialog) => dialog.open")
            page.locator("#email").fill("patient@example.test")
            page.locator("#password").fill("OnlySynthetic!2026Pwd")
            page.locator("#authSubmit").click()
            page.locator("#dashboardTitle").get_by_text("أهلاً").wait_for()
            assert page.locator("#bookingPanel").is_visible(), "booking intent lost across login"
            page.locator("#bookingForm button").click()
            page.wait_for_function("() => document.querySelector('#toast')?.textContent.includes('تم إرسال الحجز')")
            assert len(state["booked"]) == 1, "patient booking not sent"
            page.set_viewport_size({"width": 390, "height": 844})
            page.wait_for_timeout(200)
            width = page.evaluate("document.documentElement.scrollWidth - window.innerWidth")
            assert width <= 2, f"mobile horizontal overflow by {width}px"
            assert not errors, "browser JS exceptions: " + repr(errors)
            browser.close()
            print("PASS REAL CHROMIUM UI: nurse signup/profile/skills -> admin approval -> patient booking after login; mobile width 390")
    finally:
        server.shutdown()
        server.server_close()


if __name__ == "__main__":
    check()
