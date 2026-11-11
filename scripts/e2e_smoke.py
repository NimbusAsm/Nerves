"""End-to-end smoke: login API -> OIDC -> token -> gateway calls."""

import base64
import hashlib
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request

IDP = os.environ.get("E2E_IDP", "http://127.0.0.1:5101")
GATEWAY = os.environ.get("E2E_GATEWAY", "http://127.0.0.1:5100")
CONSOLE = os.environ.get("E2E_CONSOLE", "http://127.0.0.1:5109")
USERNAME = os.environ.get("E2E_USERNAME", "admin")
PASSWORD = os.environ.get("E2E_PASSWORD", "ChangeMe!123")
REDIRECT = os.environ.get("E2E_REDIRECT", "http://127.0.0.1:5300/oidc-callback.html")
CLIENT_ID = os.environ.get("E2E_CLIENT_ID", "nerves-admin")


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None


opener = urllib.request.build_opener(NoRedirect)


def call(url, data=None, headers=None, method=None):
    request = urllib.request.Request(url, data=data, headers=headers or {}, method=method)
    try:
        response = opener.open(request)
        return response.status, dict(response.headers), response.read().decode()
    except urllib.error.HTTPError as error:
        return error.code, dict(error.headers), error.read().decode()


def fail(message):
    print("FAIL:", message)
    sys.exit(1)


def expect(actual, expected, label):
    if actual != expected:
        fail(f"{label}: expected {expected}, got {actual}")
    print(f"   ok  {label} -> {actual}")


def main():
    for name, port in [("idp", 5101), ("gateway", 5100), ("identity", 5102), ("session", 5103),
                       ("authorization", 5104), ("audit", 5105), ("directory", 5106), ("compliance", 5107)]:
        status, _, _ = call(f"http://127.0.0.1:{port}/healthz")
        expect(status, 200, f"{name} health")

    status, _, _ = call(f"{CONSOLE}/healthz")
    expect(status, 200, "console health")

    status, _, _ = call(f"{CONSOLE}/api/v1/console/overview")
    expect(status, 401, "console overview without a token")

    status, _, body = call(
        f"{IDP}/api/v1/auth/login",
        json.dumps({"username": USERNAME, "password": PASSWORD}).encode(),
        {"Content-Type": "application/json"},
    )
    expect(status, 200, "login API")
    payload = json.loads(body)
    if payload.get("mfaRequired"):
        fail("the seeded administrator unexpectedly requires MFA")
    ticket = payload["loginTicket"]

    verifier = base64.urlsafe_b64encode(os.urandom(32)).decode().rstrip("=")
    challenge = base64.urlsafe_b64encode(hashlib.sha256(verifier.encode()).digest()).decode().rstrip("=")
    query = urllib.parse.urlencode({
        "client_id": CLIENT_ID,
        "response_type": "code",
        "scope": "openid profile email nerves.api",
        "redirect_uri": REDIRECT,
        "state": "e2e",
        "code_challenge": challenge,
        "code_challenge_method": "S256",
        "login_ticket": ticket,
    })
    status, headers, _ = call(f"{IDP}/connect/authorize?{query}")
    expect(status, 302, "authorize")
    location = headers.get("Location", "")
    code = urllib.parse.parse_qs(urllib.parse.urlparse(location).query).get("code", [None])[0]
    if not code:
        fail(f"authorize did not return a code: {location}")

    form = urllib.parse.urlencode({
        "grant_type": "authorization_code",
        "code": code,
        "redirect_uri": REDIRECT,
        "client_id": CLIENT_ID,
        "code_verifier": verifier,
    }).encode()
    status, _, body = call(
        f"{IDP}/connect/token", form, {"Content-Type": "application/x-www-form-urlencoded"})
    expect(status, 200, "token")
    tokens = json.loads(body)
    access_token = tokens["access_token"]

    auth = {"Authorization": f"Bearer {access_token}"}
    for path in ["/api/v1/stats", "/api/v1/organizations", "/api/v1/compliance/summary", "/api/v1/audit-events"]:
        status, _, _ = call(f"{GATEWAY}{path}", headers=auth)
        expect(status, 200, f"gateway {path}")

    email = {
        "to": "e2e@nerves.local",
        "template": "account.welcome",
        "values": {"displayName": "E2E", "username": "e2e", "issuer": IDP},
    }
    status, _, _ = call(
        f"{GATEWAY}/api/v1/notifications/email",
        json.dumps(email).encode(),
        {**auth, "Content-Type": "application/json"},
    )
    expect(status, 201, "gateway notifications send")

    status, _, _ = call(f"{GATEWAY}/api/v1/notifications?limit=1", headers=auth)
    expect(status, 200, "gateway notifications list")

    status, _, _ = call(
        f"{IDP}/api/v1/auth/password-reset/request",
        json.dumps({"identifier": "e2e@nerves.local"}).encode(),
        {"Content-Type": "application/json"},
    )
    expect(status, 202, "password reset request")

    status, _, _ = call(f"{IDP}/api/v1/admin/directory-sync/status", headers=auth)
    expect(status, 200, "directory sync status")

    status, _, _ = call(f"{GATEWAY}/api/v1/stats")
    expect(status, 401, "gateway without a token")

    status, _, _ = call(f"{GATEWAY}/api/v1/notifications")
    expect(status, 401, "gateway notifications without a token")

    status, _, body = call(f"{CONSOLE}/api/v1/console/overview", headers=auth)
    expect(status, 200, "console overview")
    overview = json.loads(body)
    services = overview.get("services") or []
    if len(services) != 5:
        fail(f"console overview: expected 5 service statuses, got {len(services)}")
    print(f"   ok  console overview services -> {len(services)}")
    if not isinstance(overview.get("warnings"), list):
        fail("console overview: warnings must be a list")
    print("   ok  console overview warnings -> list")

    status, _, body = call(f"{GATEWAY}/api/v1/accounts?limit=1", headers=auth)
    expect(status, 200, "gateway accounts list")
    accounts = json.loads(body).get("items") or []
    if not accounts:
        fail("gateway accounts list: no accounts to inspect")

    account_id = accounts[0]["id"]
    status, _, body = call(f"{CONSOLE}/api/v1/console/account/{account_id}", headers=auth)
    expect(status, 200, "console account detail")
    detail = json.loads(body)
    for key in ("account", "sessions", "auditEvents", "warnings"):
        if key not in detail:
            fail(f"console account detail: missing {key}")
    print("   ok  console account detail -> account, sessions, auditEvents, warnings")

    print("e2e smoke passed")


if __name__ == "__main__":
    main()
