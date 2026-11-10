"""Chainable builders for /api/v1/compliance/subjects."""

from __future__ import annotations

from dataclasses import dataclass, field, replace
from typing import Any, List, Mapping
from uuid import uuid4

from ..models import EraseResult, ErasureRecord
from ..transport import Transport


@dataclass(frozen=True, slots=True)
class ExportSubjectBuilder:
    """Builds GET /api/v1/compliance/subjects/{subjectId}/export."""

    _transport: Transport = field(repr=False, compare=False)
    subject_id: str

    def send(self) -> Mapping[str, Any]:
        """Sends the request and returns the export document unchanged."""
        payload = self._transport.request("GET", f"compliance/subjects/{self.subject_id}/export")
        return payload if isinstance(payload, Mapping) else {}


@dataclass(frozen=True, slots=True)
class EraseSubjectBuilder:
    """Builds POST /api/v1/compliance/subjects/{subjectId}/erase."""

    _transport: Transport = field(repr=False, compare=False)
    subject_id: str
    _reason: str | None = None
    idempotency_key: str | None = None

    def with_reason(self, reason: str | None) -> "EraseSubjectBuilder":
        """Sets the erasure reason."""
        return replace(self, _reason=reason)

    def with_idempotency_key(self, idempotency_key: str) -> "EraseSubjectBuilder":
        """Overrides the generated idempotency key."""
        return replace(self, idempotency_key=idempotency_key)

    def send(self) -> EraseResult:
        """Sends the request."""
        payload = self._transport.request(
            "POST",
            f"compliance/subjects/{self.subject_id}/erase",
            body={"reason": self._reason},
            idempotency_key=self.idempotency_key or str(uuid4()),
        )
        return EraseResult.from_mapping(payload)


@dataclass(frozen=True, slots=True)
class ListErasuresBuilder:
    """Builds GET /api/v1/compliance/subjects/{subjectId}/erasures."""

    _transport: Transport = field(repr=False, compare=False)
    subject_id: str

    def send(self) -> List[ErasureRecord]:
        """Sends the request."""
        payload = self._transport.request("GET", f"compliance/subjects/{self.subject_id}/erasures")
        return [ErasureRecord.from_mapping(item) for item in payload]


class SubjectsResource:
    """Entry point for data-subject rights."""

    def __init__(self, transport: Transport) -> None:
        self._transport = transport

    def export(self, subject_id: str) -> ExportSubjectBuilder:
        """Starts a subject export request."""
        return ExportSubjectBuilder(self._transport, str(subject_id))

    def erase(self, subject_id: str) -> EraseSubjectBuilder:
        """Starts a subject erasure request."""
        return EraseSubjectBuilder(self._transport, str(subject_id))

    def erasures(self, subject_id: str) -> ListErasuresBuilder:
        """Starts a request for the subject's erasure history."""
        return ListErasuresBuilder(self._transport, str(subject_id))
