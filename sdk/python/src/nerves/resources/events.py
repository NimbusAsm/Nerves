"""Chainable builders for /api/v1/events."""

from __future__ import annotations

from dataclasses import dataclass, field, replace
from typing import List, Tuple

from ..models import IdentityEvent
from ..transport import Transport


def _iso(value: object | None) -> str | None:
    if value is None:
        return None
    if hasattr(value, "isoformat"):
        return str(value.isoformat())
    return str(value)


@dataclass(frozen=True, slots=True)
class ListEventsBuilder:
    """Builds GET /api/v1/events; the endpoint returns a plain array ordered by occurredAt."""

    _transport: Transport = field(repr=False, compare=False)
    _account_id: str | None = None
    _after: object | None = None
    _limit: int | None = None

    def for_account(self, account_id: str) -> "ListEventsBuilder":
        """Restricts the stream to one account."""
        return replace(self, _account_id=str(account_id))

    def since(self, after: object) -> "ListEventsBuilder":
        """Returns events strictly newer than the given instant."""
        return replace(self, _after=after)

    def limit(self, page_size: int) -> "ListEventsBuilder":
        """Sets the batch size (server default 100, max 500)."""
        return replace(self, _limit=page_size)

    def send(self) -> List[IdentityEvent]:
        """Sends the request."""
        entries = (
            ("accountId", self._account_id),
            ("after", _iso(self._after)),
            ("limit", None if self._limit is None else str(self._limit)),
        )
        params: List[Tuple[str, str]] = [(key, value) for key, value in entries if value is not None]
        payload = self._transport.request("GET", "events", params=params or None)
        return [IdentityEvent.from_mapping(item) for item in payload]


class EventsResource:
    """Entry point for the identity domain event stream."""

    def __init__(self, transport: Transport) -> None:
        self._transport = transport

    def list(self) -> ListEventsBuilder:
        """Starts an incremental read."""
        return ListEventsBuilder(self._transport)
