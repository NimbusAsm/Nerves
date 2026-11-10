"""Chainable builders for /api/v1/audit-events."""

from __future__ import annotations

from dataclasses import dataclass, field, replace
from typing import Iterator, List, Tuple

from ..models import AuditEvent, AuditVerifyResult
from ..pagination import Page
from ..transport import DownloadedFile, Transport


def _iso(value: object | None) -> str | None:
    if value is None:
        return None
    if hasattr(value, "isoformat"):
        return str(value.isoformat())
    return str(value)


@dataclass(frozen=True, slots=True)
class ListAuditEventsBuilder:
    """Builds GET /api/v1/audit-events with cursor pagination."""

    _transport: Transport = field(repr=False, compare=False)
    _stream: str | None = None
    _actor_id: str | None = None
    _action: str | None = None
    _result: str | None = None
    _from: object | None = None
    _to: object | None = None
    _limit: int | None = None
    _cursor: str | None = None

    def in_stream(self, stream: str) -> "ListAuditEventsBuilder":
        """Restricts the result to one hash-chained stream."""
        return replace(self, _stream=stream)

    def by_actor(self, actor_id: str) -> "ListAuditEventsBuilder":
        """Restricts the result to one actor."""
        return replace(self, _actor_id=actor_id)

    def with_action(self, action: str) -> "ListAuditEventsBuilder":
        """Restricts the result to one dotted action name."""
        return replace(self, _action=action)

    def with_result(self, result: str) -> "ListAuditEventsBuilder":
        """Restricts the result to one outcome."""
        return replace(self, _result=result)

    def since(self, from_: object) -> "ListAuditEventsBuilder":
        """Sets the inclusive lower bound."""
        return replace(self, _from=from_)

    def until(self, to: object) -> "ListAuditEventsBuilder":
        """Sets the inclusive upper bound."""
        return replace(self, _to=to)

    def limit(self, page_size: int) -> "ListAuditEventsBuilder":
        """Sets the page size."""
        return replace(self, _limit=page_size)

    def cursor(self, cursor: str) -> "ListAuditEventsBuilder":
        """Continues from an opaque cursor."""
        return replace(self, _cursor=cursor)

    def send(self) -> Page[AuditEvent]:
        """Fetches one page."""
        page = self._transport.request_page("GET", "audit-events", params=self._params())
        return Page(
            items=tuple(AuditEvent.from_mapping(item) for item in page.items),
            next_cursor=page.next_cursor,
        )

    def __iter__(self) -> Iterator[AuditEvent]:
        """Iterates every matching event, following next_cursor."""
        cursor = self._cursor
        seen: set[str] = set()

        while True:
            builder = replace(self, _cursor=cursor)
            page = builder.send()
            yield from page.items

            next_cursor = page.next_cursor
            if not next_cursor or next_cursor in seen:
                return
            seen.add(next_cursor)
            cursor = next_cursor

    def _params(self) -> List[Tuple[str, str]]:
        params: List[Tuple[str, str]] = []
        entries = (
            ("stream", self._stream),
            ("actorId", self._actor_id),
            ("action", self._action),
            ("result", self._result),
            ("from", _iso(self._from)),
            ("to", _iso(self._to)),
            ("limit", None if self._limit is None else str(self._limit)),
            ("cursor", self._cursor),
        )
        for key, value in entries:
            if value is not None:
                params.append((key, value))
        return params


@dataclass(frozen=True, slots=True)
class VerifyAuditBuilder:
    """Builds GET /api/v1/audit-events/verify."""

    _transport: Transport = field(repr=False, compare=False)
    _stream: str | None = None

    def in_stream(self, stream: str) -> "VerifyAuditBuilder":
        """Restricts verification to one stream."""
        return replace(self, _stream=stream)

    def send(self) -> AuditVerifyResult:
        """Sends the request."""
        params = [("stream", self._stream)] if self._stream is not None else None
        payload = self._transport.request("GET", "audit-events/verify", params=params)
        return AuditVerifyResult.from_mapping(payload)


@dataclass(frozen=True, slots=True)
class ExportAuditBuilder:
    """Builds GET /api/v1/audit-events/export."""

    _transport: Transport = field(repr=False, compare=False)
    _format: str | None = None
    _limit: int | None = None

    def as_(self, format_: str) -> "ExportAuditBuilder":
        """Selects csv (default) or json."""
        return replace(self, _format=format_)

    def limit(self, rows: int) -> "ExportAuditBuilder":
        """Sets the row cap (server default 5000, max 20000)."""
        return replace(self, _limit=rows)

    def send(self) -> DownloadedFile:
        """Sends the request and returns the downloaded document."""
        params: List[Tuple[str, str]] = []
        if self._format is not None:
            params.append(("format", self._format))
        if self._limit is not None:
            params.append(("limit", str(self._limit)))

        return self._transport.request_file("GET", "audit-events/export", params=params or None)


class AuditResource:
    """Entry point for the audit trail."""

    def __init__(self, transport: Transport) -> None:
        self._transport = transport

    def list(self) -> ListAuditEventsBuilder:
        """Starts a list request."""
        return ListAuditEventsBuilder(self._transport)

    def verify(self) -> VerifyAuditBuilder:
        """Starts a chain-verification request."""
        return VerifyAuditBuilder(self._transport)

    def export(self) -> ExportAuditBuilder:
        """Starts an export request."""
        return ExportAuditBuilder(self._transport)
