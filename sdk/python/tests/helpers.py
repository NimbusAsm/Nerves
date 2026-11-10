"""Test doubles: a requests.Session lookalike and sample payloads."""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from typing import Any, Dict, List, Mapping

from nerves import NervesClient


@dataclass
class RecordedCall:
    method: str
    url: str
    params: List[tuple[str, str]] | None
    json: Mapping[str, Any] | None
    headers: Mapping[str, str]
    timeout: Any


class FakeResponse:
    def __init__(
        self,
        status_code: int,
        body: Any = None,
        headers: Mapping[str, str] | None = None,
        reason: str = "",
    ) -> None:
        self.status_code = status_code
        if body is None:
            self.text = ""
            self.content = b""
        elif isinstance(body, str):
            self.text = body
            self.content = body.encode()
        else:
            self.text = json.dumps(body)
            self.content = self.text.encode()
        self.headers = dict(headers or {})
        self.reason = reason


class FakeSession:
    """Records calls and replays queued responses; never touches the network."""

    def __init__(self) -> None:
        self.calls: List[RecordedCall] = []
        self.closed = False
        self._responses: List[FakeResponse] = []

    def enqueue(
        self,
        status_code: int,
        body: Any = None,
        headers: Mapping[str, str] | None = None,
        reason: str = "",
    ) -> "FakeSession":
        self._responses.append(FakeResponse(status_code, body, headers, reason))
        return self

    def request(
        self,
        method: str,
        url: str,
        *,
        params: List[tuple[str, str]] | None = None,
        json: Mapping[str, Any] | None = None,
        headers: Mapping[str, str] | None = None,
        timeout: Any = None,
    ) -> FakeResponse:
        self.calls.append(
            RecordedCall(
                method=method,
                url=url,
                params=params,
                json=json,
                headers=dict(headers or {}),
                timeout=timeout,
            )
        )
        if not self._responses:
            raise AssertionError(f"No response queued for {method} {url}")
        return self._responses.pop(0)

    def close(self) -> None:
        self.closed = True


def make_client(
    session: FakeSession | None = None,
    **overrides: Any,
) -> tuple[NervesClient, FakeSession]:
    fake = session or FakeSession()
    kwargs: Dict[str, Any] = {
        "session": fake,
        "max_retries": 0,
        "retry_base_delay": 0.0,
    }
    kwargs.update(overrides)
    return NervesClient("https://nerves.test", "pat-test-token", **kwargs), fake


ENTITY_ID = "0199a0c0-0000-7000-8000-000000000010"
ACCOUNT_ID = "0199a0c0-0000-7000-8000-000000000001"
ROLE_ID = "0199a0c0-0000-7000-8000-000000000020"

ACCOUNT = {
    "id": ACCOUNT_ID,
    "legalEntityId": ENTITY_ID,
    "realm": "default",
    "username": "alice",
    "email": "alice@example.com",
    "phoneNumber": None,
    "displayName": None,
    "type": "personal",
    "status": "active",
    "isPrimary": False,
    "roles": ["member"],
    "createdAt": "2026-01-01T00:00:00Z",
    "updatedAt": "2026-01-01T00:00:00Z",
    "disabledAt": None,
}

LEGAL_ENTITY = {
    "id": ENTITY_ID,
    "type": "naturalPerson",
    "displayName": "Alice",
    "realName": {"documentType": "passport", "country": "CN"},
    "verificationState": "pending",
    "verifiedAt": None,
    "providerReference": None,
    "createdAt": "2026-01-01T00:00:00Z",
    "updatedAt": "2026-01-01T00:00:00Z",
}

ROLE = {
    "id": ROLE_ID,
    "name": "member",
    "description": "Members",
    "permissions": ["accounts.read"],
    "createdAt": "2026-01-01T00:00:00Z",
    "updatedAt": "2026-01-01T00:00:00Z",
}

SESSION = {
    "id": "0199a0c0-0000-7000-8000-000000000030",
    "deviceId": "web-1",
    "createdAt": "2026-01-01T00:00:00Z",
    "lastSeenAt": "2026-01-02T00:00:00Z",
    "absoluteExpiresAt": "2026-02-01T00:00:00Z",
    "revoked": False,
    "revokedReason": None,
}

GROUP = {
    "id": "0199a0c0-0000-7000-8000-000000000050",
    "realm": "default",
    "code": "ops",
    "name": "Operations",
}

CONSENT = {
    "id": "0199a0c0-0000-7000-8000-000000000070",
    "subjectId": "alice",
    "purpose": "marketing",
    "version": "v1",
    "grantedAt": "2026-01-01T00:00:00Z",
    "expiresAt": None,
    "revokedAt": None,
    "revocationReason": None,
}

NOTIFICATION = {
    "id": "0199a0c0-0000-7000-8000-000000000090",
    "to": "alice@example.com",
    "template": "welcome",
    "subject": "Welcome",
    "status": "pending",
    "attempts": 0,
    "error": None,
    "createdAt": "2026-01-01T00:00:00Z",
    "sentAt": None,
    "nextAttemptAt": None,
}

AUDIT_EVENT = {
    "id": "0199a0c0-0000-7000-8000-000000000060",
    "stream": "identity",
    "sequence": 1,
    "occurredAt": "2026-01-01T00:00:00Z",
    "actorType": "admin",
    "actorId": "admin",
    "action": "account.created",
    "targetType": "account",
    "targetId": "x",
    "result": "success",
    "traceId": "t",
    "hash": "h",
}

SESSION_ID = SESSION["id"]
GROUP_ID = GROUP["id"]
CONSENT_ID = CONSENT["id"]
NOTIFICATION_ID = NOTIFICATION["id"]
