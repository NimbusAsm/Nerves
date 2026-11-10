"""Chainable builders for /api/v1/sessions."""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import List

from ..models import SessionInfo
from ..transport import Transport


@dataclass(frozen=True, slots=True)
class ListSessionsBuilder:
    """Builds GET /api/v1/accounts/{accountId}/sessions."""

    _transport: Transport = field(repr=False, compare=False)
    account_id: str

    def send(self) -> List[SessionInfo]:
        """Sends the request."""
        payload = self._transport.request("GET", f"accounts/{self.account_id}/sessions")
        return [SessionInfo.from_mapping(item) for item in payload]


@dataclass(frozen=True, slots=True)
class RevokeSessionBuilder:
    """Builds DELETE /api/v1/sessions/{sessionId}."""

    _transport: Transport = field(repr=False, compare=False)
    session_id: str

    def send(self) -> None:
        """Sends the request."""
        self._transport.request_void("DELETE", f"sessions/{self.session_id}")


class SessionsResource:
    """Entry point for session inspection and revocation."""

    def __init__(self, transport: Transport) -> None:
        self._transport = transport

    def list(self, account_id: str) -> ListSessionsBuilder:
        """Starts a list request for one account."""
        return ListSessionsBuilder(self._transport, str(account_id))

    def revoke(self, session_id: str) -> RevokeSessionBuilder:
        """Starts a revoke request for one session."""
        return RevokeSessionBuilder(self._transport, str(session_id))
