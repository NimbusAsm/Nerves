"""Obtains a management access token from a locally booted Nerves IdP.

Used by the password-reset smoke: the identity service needs a bearer token to
queue the reset email through the notification service.
"""

import base64
import hashlib
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request

IDP = os.environ.get("RESET_IDP", "http://127.0.0.1:5131")
USERNAME = os.environ.get("RESET_USERNAME", "admin")
PASSWORD = os.environ.get("RESET_PASSWORD", "ChangeMe!123")
REDIRECT = os.environ.get("RESET_REDIRECT", "http://127.0.0.1:5300/oidc-callback.html")
CLIENT_ID = os.environ.get("RESET_CLIENT_ID", "nerves-admin")
OUTPUT = os.environ.get("RESET_TOKEN_FILE", "/tmp/nerves-reset-admin-token")


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


def main():
    status, _, body = call(
        f"{IDP}/api/v1/auth/login",
        json.dumps({"username": USERNAME, "password": PASSWORD}).encode(),
        {"Content-Type": "application/json"},
    )
    if status != 200:
        print("login failed:", status, body)
        sys.exit(1)
    ticket = json.loads(body)["loginTicket"]

    verifier = base64.urlsafe_b64encode(os.urandom(32)).decode().rstrip("=")
    challenge = base64.urlsafe_b64encode(hashlib.sha256(verifier.encode()).digest()).decode().rstrip("=")
    query = urllib.parse.urlencode({
        "client_id": CLIENT_ID,
        "response_type": "code",
        "scope": "openid profile email nerves.api",
        "redirect_uri": REDIRECT,
        "state": "token-bootstrap",
        "code_challenge": challenge,
        "code_challenge_method": "S256",
        "login_ticket": ticket,
    })
    status, headers, _ = call(f"{IDP}/connect/authorize?{query}")
    if status != 302:
        print("authorize failed:", status)
        sys.exit(1)

    code = urllib.parse.parse_qs(urllib.parse.urlparse(headers.get("Location", "")).query).get("code", [None])[0]
    if not code:
        print("no code in the authorize redirect")
        sys.exit(1)

    form = urllib.parse.urlencode({
        "grant_type": "authorization_code",
        "code": code,
        "redirect_uri": REDIRECT,
        "client_id": CLIENT_ID,
        "code_verifier": verifier,
    }).encode()
    status, _, body = call(f"{IDP}/connect/token", form,
                           {"Content-Type": "application/x-www-form-urlencoded"})
    if status != 200:
        print("token failed:", status, body)
        sys.exit(1)

    token = json.loads(body)["access_token"]
    with open(OUTPUT, "w", encoding="utf-8") as handle:
        handle.write(token)
    print(f"   ok  management token written to {OUTPUT}")


if __name__ == "__main__":
    main()
