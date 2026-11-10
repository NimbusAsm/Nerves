"""Phase 2 of the password reset smoke: proves the whole chain.

Requires a management token (RESET_ADMIN_TOKEN) plus the identity log directory
(RESET_LOG_DIR) where the notification no-op sender records the rendered link.
"""

import json
import os
import re
import shlex
import subprocess
import sys
import urllib.error
import urllib.request

IDP = os.environ.get("RESET_IDP", "http://127.0.0.1:5131")
GATEWAY = os.environ.get("RESET_GATEWAY", "http://127.0.0.1:5130")
NOTIFICATION = os.environ.get("RESET_NOTIFICATION", "http://127.0.0.1:5138")
TOKEN = os.environ.get("RESET_ADMIN_TOKEN", "")
LOG_DIR = os.environ.get("RESET_LOG_DIR", "")
USERNAME = os.environ.get("RESET_USERNAME", "admin")
NEW_PASSWORD = os.environ.get("RESET_NEW_PASSWORD", "Reset!Passw0rd2026")


def call(url, data=None, headers=None, method=None):
    request = urllib.request.Request(url, data=data, headers=headers or {}, method=method)
    try:
        response = urllib.request.urlopen(request)
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
    if not TOKEN:
        fail("RESET_ADMIN_TOKEN is empty")
    auth = {"Authorization": f"Bearer {TOKEN}", "Content-Type": "application/json"}

    # 1) uniform responses
    unknown, _, body_unknown = call(
        f"{IDP}/api/v1/auth/password-reset/request",
        json.dumps({"identifier": "no-such-user", "realm": "nerves"}).encode(), auth)
    expect(unknown, 202, "reset request for an unknown identifier")

    known, _, body_known = call(
        f"{IDP}/api/v1/auth/password-reset/request",
        json.dumps({"identifier": USERNAME, "realm": "nerves"}).encode(), auth)
    expect(known, 202, "reset request for the seeded account")
    if body_unknown != body_known:
        fail("reset responses differ; account existence leaks")

    # 2) the notification service queued exactly one reset email
    status, _, body = call(f"{NOTIFICATION}/api/v1/notifications?limit=20", headers=auth)
    expect(status, 200, "notification log")
    items = json.loads(body)["items"]
    resets = [item for item in items if item["template"] == "account.password-reset"]
    if not resets:
        fail("no account.password-reset message was queued")
    print(f"   ok  queued reset emails -> {len(resets)}")

    # 3) recover the token from the rendered email body. Development-only: the
    #    notification no-op sender never opens a socket, so the row in the dev
    #    database is the only place the link exists.
    token = None
    db = os.environ.get("RESET_DB_NAME", "nerves_reset_smoke")
    query = 'select "Body" from notification.notification_message order by "Id" desc limit 1'
    psql_cmd = os.environ.get("RESET_PSQL_CMD", "").strip()
    result = subprocess.run(
        shlex.split(psql_cmd) + ["-d", db, "-tAc", query],
        capture_output=True, text=True, check=False) if psql_cmd else None
    if result is None:
        fail("RESET_PSQL_CMD is empty; cannot read the rendered email")
    if result.returncode == 0:
        matches = re.findall(r"reset-password\?token=([A-Za-z0-9_\-]+)", result.stdout)
        token = matches[-1] if matches else None
    if not token:
        fail("could not recover the reset token from the notification log")

    # 4) confirm with the token, then replay it
    status, _, body = call(
        f"{IDP}/api/v1/auth/password-reset/confirm",
        json.dumps({"token": token, "newPassword": NEW_PASSWORD}).encode(), auth)
    expect(status, 200, "confirm with the emailed token")

    status, _, _ = call(
        f"{IDP}/api/v1/auth/password-reset/confirm",
        json.dumps({"token": token, "newPassword": "Another!Passw0rd"}).encode(), auth)
    expect(status, 400, "replaying the spent token")

    # 5) the old password is gone and the new one works
    status, _, _ = call(
        f"{IDP}/api/v1/auth/login",
        json.dumps({"username": USERNAME, "password": "ChangeMe!123"}).encode(), auth)
    expect(status, 401, "login with the old password")

    status, _, _ = call(
        f"{IDP}/api/v1/auth/login",
        json.dumps({"username": USERNAME, "password": NEW_PASSWORD}).encode(), auth)
    expect(status, 200, "login with the new password")

    print("password reset smoke passed")


if __name__ == "__main__":
    main()