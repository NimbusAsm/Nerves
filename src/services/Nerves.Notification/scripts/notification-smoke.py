"""Live smoke for the notification service (expects the service on :5108)."""

import base64
import hashlib
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

BASE = os.environ.get("NOTIFY_BASE", "http://127.0.0.1:5108")
IDP = os.environ.get("E2E_IDP", "http://127.0.0.1:5101")
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
        fail(label + ": expected " + str(expected) + ", got " + str(actual))
    print("   ok  " + label + " -> " + str(actual))


def token():
    status, _, body = call(
        IDP + "/api/v1/auth/login",
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
        "state": "smoke",
        "code_challenge": challenge,
        "code_challenge_method": "S256",
        "login_ticket": ticket,
    })
    status, headers, _ = call(IDP + "/connect/authorize?" + query)
    expect(status, 302, "authorize")
    location = headers.get("Location", "")
    code = urllib.parse.parse_qs(urllib.parse.urlparse(location).query).get("code", [None])[0]
    if not code:
        fail("authorize did not return a code: " + location)

    form = urllib.parse.urlencode({
        "grant_type": "authorization_code",
        "code": code,
        "redirect_uri": REDIRECT,
        "client_id": CLIENT_ID,
        "code_verifier": verifier,
    }).encode()
    status, _, body = call(
        IDP + "/connect/token", form, {"Content-Type": "application/x-www-form-urlencoded"})
    expect(status, 200, "token")
    return json.loads(body)["access_token"]


def api(method, path, payload=None, auth=None):
    body = json.dumps(payload).encode() if payload is not None else None
    headers = {"Content-Type": "application/json"} if body else {}
    if auth:
        headers["Authorization"] = "Bearer " + auth
    return call(BASE + path, body, headers, method)


def create(auth, template="account.welcome", to="smoke@example.com", values=None):
    payload = {
        "to": to,
        "template": template,
        "values": values if values is not None else {
            "displayName": "Smoke Test",
            "username": "smoke.test",
            "issuer": "https://nerves.example.com/",
        },
    }
    status, _, body = api("POST", "/api/v1/notifications/email", payload, auth)
    return status, body


def wait_for(auth, notification_id, target, timeout=40):
    deadline = time.time() + timeout
    last = None
    while time.time() < deadline:
        status, _, body = api("GET", "/api/v1/notifications/" + notification_id, auth=auth)
        expect(status, 200, "detail")
        last = json.loads(body)
        if last["status"] == target:
            return last
        time.sleep(1)
    fail("notification " + notification_id + " did not reach " + target + "; last=" + str(last))
    return None


def phase1():
    auth = token()

    status, body = create(auth, to="smoke-sent@example.com")
    expect(status, 201, "create email")
    created = json.loads(body)
    print("   created:", created["id"], created["status"], created["attempts"])
    expect(created["status"], "pending", "initial status")

    sent = wait_for(auth, created["id"], "sent")
    print("   sent-at:", sent["sentAt"], "attempts:", sent["attempts"], "error:", sent["error"])
    expect(sent["attempts"], 0, "attempts after success")
    if sent["sentAt"] is None:
        fail("sentAt was not set")

    status, _, body = api("GET", "/api/v1/notifications?status=sent&limit=5", auth=auth)
    expect(status, 200, "list sent")
    page = json.loads(body)
    if not any(item["id"] == created["id"] for item in page["items"]):
        fail("the sent notification is missing from the sent filter")
    print("   list items:", len(page["items"]), "nextCursor:", page["nextCursor"])

    status, _, _ = api("GET", "/api/v1/notifications?status=bogus", auth=auth)
    expect(status, 400, "invalid status filter")

    status, body = create(auth, to="not-an-address")
    expect(status, 400, "invalid recipient")
    print("   invalid recipient detail:", json.loads(body).get("detail"))

    status, body = create(auth, template="does.not.exist", to="smoke-sent@example.com")
    expect(status, 400, "unknown template")
    print("   unknown template detail:", json.loads(body).get("detail"))

    status, body = create(
        auth,
        template="account.password-reset",
        to="smoke-sent@example.com",
        values={"displayName": "Smoke Test"},
    )
    expect(status, 400, "missing template values")
    print("   missing values detail:", json.loads(body).get("detail"))

    status, _, _ = api("GET", "/api/v1/notifications", auth="invalid-token")
    expect(status, 401, "invalid token")


def phase2():
    auth = token()
    status, body = create(auth, to="smoke-failed@example.com")
    expect(status, 201, "create email")
    created = json.loads(body)

    failed = wait_for(auth, created["id"], "failed")
    print("   failed:", failed["status"], "attempts:", failed["attempts"], "error:", failed["error"])
    if failed["attempts"] < 2:
        fail("expected at least 2 attempts before failing, got " + str(failed["attempts"]))
    if not failed["error"]:
        fail("a failed notification must record the error")
    if failed["sentAt"] is not None:
        fail("a failed notification must not have sentAt")


if __name__ == "__main__":
    phase = sys.argv[1] if len(sys.argv) > 1 else "phase1"
    mapping = {"phase1": phase1, "phase2": phase2}
    mapping[phase]()
    print(phase + " passed")
