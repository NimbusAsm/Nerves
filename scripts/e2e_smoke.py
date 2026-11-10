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

    status, _, _ = call(f"{GATEWAY}/api/v1/stats")
    expect(status, 401, "gateway without a token")

    status, _, _ = call(f"{GATEWAY}/api/v1/notifications")
    expect(status, 401, "gateway notifications without a token")
    print("e2e smoke passed")


if __name__ == "__main__":
    main()
