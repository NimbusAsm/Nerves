"""Chainable builders for /api/v1/decisions."""

from __future__ import annotations

from dataclasses import dataclass, field, replace
from uuid import uuid4

from ..models import DecisionResult, DecisionStats
from ..transport import Transport


@dataclass(frozen=True, slots=True)
class CheckDecisionBuilder:
    """Builds POST /api/v1/decisions/check."""

    _transport: Transport = field(repr=False, compare=False)
    permission: str | None = None
    account_id: str | None = None
    legal_entity_id: str | None = None
    idempotency_key: str | None = None

    def with_permission(self, permission: str) -> "CheckDecisionBuilder":
        """Sets the permission code to check."""
        return replace(self, permission=permission)

    def for_account(self, account_id: str) -> "CheckDecisionBuilder":
        """Sets the account the decision is about."""
        return replace(self, account_id=str(account_id))

    def for_entity(self, legal_entity_id: str) -> "CheckDecisionBuilder":
        """Sets the legal entity the decision is about."""
        return replace(self, legal_entity_id=str(legal_entity_id))

    def with_idempotency_key(self, idempotency_key: str) -> "CheckDecisionBuilder":
        """Overrides the generated idempotency key."""
        return replace(self, idempotency_key=idempotency_key)

    def send(self) -> DecisionResult:
        """Sends the request."""
        if not self.permission or not self.permission.strip():
            raise ValueError("with_permission() is required before send().")

        payload = self._transport.request(
            "POST",
            "decisions/check",
            body={
                "accountId": self.account_id,
                "legalEntityId": self.legal_entity_id,
                "permission": self.permission,
            },
            idempotency_key=self.idempotency_key or str(uuid4()),
        )
        return DecisionResult.from_mapping(payload)


@dataclass(frozen=True, slots=True)
class GetDecisionStatsBuilder:
    """Builds GET /api/v1/decisions/stats."""

    _transport: Transport = field(repr=False, compare=False)

    def send(self) -> DecisionStats:
        """Sends the request."""
        payload = self._transport.request("GET", "decisions/stats")
        return DecisionStats.from_mapping(payload)


class DecisionsResource:
    """Entry point for authorization decisions."""

    def __init__(self, transport: Transport) -> None:
        self._transport = transport

    def check(self, permission: str) -> CheckDecisionBuilder:
        """Starts a single decision check."""
        return CheckDecisionBuilder(self._transport, permission=permission)

    def stats(self) -> GetDecisionStatsBuilder:
        """Starts a request for permission-cache statistics."""
        return GetDecisionStatsBuilder(self._transport)
