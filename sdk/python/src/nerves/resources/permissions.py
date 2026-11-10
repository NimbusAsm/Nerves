"""Chainable builders for /api/v1/permissions."""

from __future__ import annotations

from dataclasses import dataclass, field, replace
from typing import List
from uuid import uuid4

from ..models import PermissionInfo
from ..transport import Transport


@dataclass(frozen=True, slots=True)
class ListPermissionsBuilder:
    """Builds GET /api/v1/permissions."""

    _transport: Transport = field(repr=False, compare=False)

    def send(self) -> List[PermissionInfo]:
        """Sends the request."""
        payload = self._transport.request("GET", "permissions")
        return [PermissionInfo.from_mapping(item) for item in payload]


@dataclass(frozen=True, slots=True)
class CreatePermissionBuilder:
    """Builds POST /api/v1/permissions."""

    _transport: Transport = field(repr=False, compare=False)
    code: str | None = None
    description: str | None = None
    idempotency_key: str | None = None

    def with_code(self, code: str) -> "CreatePermissionBuilder":
        """Sets the unique permission code."""
        return replace(self, code=code)

    def with_description(self, description: str | None) -> "CreatePermissionBuilder":
        """Sets the human-readable description."""
        return replace(self, description=description)

    def with_idempotency_key(self, idempotency_key: str) -> "CreatePermissionBuilder":
        """Overrides the generated idempotency key."""
        return replace(self, idempotency_key=idempotency_key)

    def send(self) -> PermissionInfo:
        """Sends the request."""
        if not self.code or not self.code.strip():
            raise ValueError("with_code() is required before send().")

        body: dict[str, object] = {"code": self.code}
        if self.description is not None:
            body["description"] = self.description

        payload = self._transport.request(
            "POST",
            "permissions",
            body=body,
            idempotency_key=self.idempotency_key or str(uuid4()),
        )
        return PermissionInfo.from_mapping(payload)


class PermissionsResource:
    """Entry point for permission codes."""

    def __init__(self, transport: Transport) -> None:
        self._transport = transport

    def list(self) -> ListPermissionsBuilder:
        """Starts a list request."""
        return ListPermissionsBuilder(self._transport)

    def create(self) -> CreatePermissionBuilder:
        """Starts a create request."""
        return CreatePermissionBuilder(self._transport)