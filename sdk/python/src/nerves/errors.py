"""RFC 9457 problem documents and the SDK's single exception type."""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Mapping


@dataclass(frozen=True, slots=True)
class ProblemDetails:
    """RFC 9457 application/problem+json body returned by the management API."""

    type: str | None = None
    title: str | None = None
    status: int | None = None
    detail: str | None = None
    instance: str | None = None
    trace_id: str | None = None
    errors: Mapping[str, tuple[str, ...]] | None = None

    @classmethod
    def from_mapping(cls, data: Mapping[str, Any]) -> "ProblemDetails":
        raw_errors = data.get("errors")
        parsed_errors: Mapping[str, tuple[str, ...]] | None = None
        if isinstance(raw_errors, Mapping):
            parsed_errors = {
                str(key): tuple(str(item) for item in value)
                for key, value in raw_errors.items()
                if isinstance(value, (list, tuple))
            }

        return cls(
            type=_optional_str(data.get("type")),
            title=_optional_str(data.get("title")),
            status=data.get("status") if isinstance(data.get("status"), int) else None,
            detail=_optional_str(data.get("detail")),
            instance=_optional_str(data.get("instance")),
            trace_id=_optional_str(data.get("traceId")),
            errors=parsed_errors,
        )


def _optional_str(value: object) -> str | None:
    return value if isinstance(value, str) else None


@dataclass(frozen=True, slots=True)
class NervesApiException(Exception):
    """The single exception raised for non-success responses.

    It always carries the HTTP status, the problem type, and the trace id.
    """

    status: int
    problem: ProblemDetails = field(default_factory=ProblemDetails)
    raw_body: str | None = None

    def __str__(self) -> str:
        detail = self.problem.detail or self.problem.title or f"HTTP {self.status}"
        suffix = f" (traceId: {self.problem.trace_id})" if self.problem.trace_id else ""
        return f"Nerves API request failed with status {self.status}: {detail}{suffix}"

    @property
    def type(self) -> str | None:  # noqa: A003 - mirrors the RFC 9457 field name
        return self.problem.type

    @property
    def trace_id(self) -> str | None:
        return self.problem.trace_id

    @property
    def title(self) -> str | None:
        return self.problem.title

    @property
    def detail(self) -> str | None:
        return self.problem.detail

    @property
    def errors(self) -> Mapping[str, tuple[str, ...]] | None:
        return self.problem.errors
