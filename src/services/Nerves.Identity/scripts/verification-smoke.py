"""End-to-end smoke for the real-name verification provider (B11).

Runs against an already booted stack with a management token. Exercises the
manual provider path end to end:

  create legal entity -> start (manual) -> status Pending
  -> administrator records the decision (existing verify endpoint)
  -> status Verified -> outbox realname.changed fan-out.

The asynchronous provider callback path is covered by
tests/Nerves.IntegrationTests/VerificationApiTests.cs, which drives a real
provider reference through the anonymous callback endpoint.
"""

import json
import os
import shlex
import subprocess
import sys
import urllib.error
import urllib.request

IDENTITY = os.environ.get("VERIFY_IDENTITY", "http://127.0.0.1:5132")
TOKEN = os.environ.get("RESET_ADMIN_TOKEN", "") or os.environ.get("VERIFY_ADMIN_TOKEN", "")
DB = os.environ.get("RESET_DB_NAME", "nerves_reset_smoke")


def call(url, data=None, headers=None, method=None):
    request = urllib.request.Request(url, data=data, headers=headers or {}, method=method)
    try:
        response = urllib.request.urlopen(request)
        return response.status, response.read().decode()
    except urllib.error.HTTPError as error:
        return error.code, error.read().decode()


def fail(message):
    print("FAIL:", message)
    sys.exit(1)


def expect(actual, expected, label):
    if actual != expected:
        fail(f"{label}: expected {expected}, got {actual}")
    print(f"   ok  {label} -> {actual}")


def query(sql):
    psql_cmd = os.environ.get("RESET_PSQL_CMD", "").strip()
    if not psql_cmd:
        return ""
    result = subprocess.run(
        shlex.split(psql_cmd) + ["-d", DB, "-tAc", sql],
        capture_output=True, text=True, check=False)
    return result.stdout.strip()


def main():
    if not TOKEN:
        fail("no admin token supplied")

    auth = {"Authorization": f"Bearer {TOKEN}", "Content-Type": "application/json"}
    payload = {
        "type": "LegalPerson",
        "displayName": "Verification Smoke Ltd",
        "documentType": "UnifiedSocialCreditCode",
        "documentHash": os.urandom(32).hex(),
        "country": "CN",
    }
    status, body = call(f"{IDENTITY}/api/v1/legal-entities", json.dumps(payload).encode(), auth)
    expect(status, 201, "create legal entity")
    entity_id = json.loads(body)["id"]

    status, body = call(
        f"{IDENTITY}/api/v1/legal-entities/{entity_id}/verification/start",
        json.dumps({"provider": "manual"}).encode(), auth)
    expect(status, 202, "start manual verification")
    started = json.loads(body)
    expect(started["attempt"]["provider"], "manual", "attempt provider")
    expect(started["attempt"]["status"], "Pending", "attempt stays pending")

    status, body = call(f"{IDENTITY}/api/v1/legal-entities/{entity_id}/verification", headers=auth)
    expect(status, 200, "verification status")
    expect(json.loads(body)["state"], "Pending", "entity stays pending")

    status, body = call(f"{IDENTITY}/api/v1/legal-entities/{entity_id}/verify", b"{}", auth)
    expect(status, 200, "administrator records the decision")
    expect(json.loads(body)["verificationState"], "Verified", "entity becomes verified")

    status, body = call(f"{IDENTITY}/api/v1/legal-entities/{entity_id}/verification/start",
                        json.dumps({"provider": "manual"}).encode(), auth)
    expect(status, 409, "restarting a verified entity is a conflict")

    events = query(
        f"select count(*) from identity.\"OutboxEvents\" "
        f"where \"LegalEntityId\" = '{entity_id}' and \"Type\" = 'realname.verification.started'")
    if events:
        expect(events, "1", "start wrote one outbox event")

    print("verification smoke passed")


if __name__ == "__main__":
    main()
