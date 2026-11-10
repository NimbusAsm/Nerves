"""Chainable builders for /api/v1/roles."""

from __future__ import annotations

from dataclasses import dataclass, field, replace
from typing import Any, Iterator, List, Tuple
from uuid import uuid4

from ..models import Role
from ..pagination import ListQuery, Page
from ..transport import Transport


@dataclass(frozen=True, slots=True)
class CreateRoleBuilder:
    """Builds POST /api/v1/roles."""

    _transport: Transport = field(repr=False, compare=False)
    name: str | None = None
    description: str | None = None
    permissions: Tuple[str, ...] = ()
    idempotency_key: str | None = None

    def with_name(self, name: str) -> "CreateRoleBuilder":
        return replace(self, name=name)

    def with_description(self, description: str | None) -> "CreateRoleBuilder":
        return replace(self, description=description)

    def with_permissions(self, *permissions: str) -> "CreateRoleBuilder":
        return replace(self, permissions=tuple(permissions))

    def with_idempotency_key(self, idempotency_key: str) -> "CreateRoleBuilder":
        return replace(self, idempotency_key=idempotency_key)

    def send(self) -> Role:
        """Sends the request."""
        if not self.name or not self.name.strip():
            raise ValueError("with_name() is required before send().")

        body: dict[str, Any] = {"name": self.name}
        if self.description is not None:
            body["description"] = self.description
        if self.permissions:
            body["permissions"] = list(self.permissions)

        payload = self._transport.request(
            "POST",
            "roles",
            body=body,
            idempotency_key=self.idempotency_key or str(uuid4()),
        )
        return Role.from_mapping(payload)


@dataclass(frozen=True, slots=True)
class GetRoleBuilder:
    """Builds GET /api/v1/roles/{id}."""

    _transport: Transport = field(repr=False, compare=False)
    role_id: str

    def send(self) -> Role:
        payload = self._transport.request("GET", f"roles/{self.role_id}")
        return Role.from_mapping(payload)


@dataclass(frozen=True, slots=True)
class ListRolesBuilder:
    """Builds GET /api/v1/roles with cursor pagination."""

    _transport: Transport = field(repr=False, compare=False)
    _query: ListQuery = field(default_factory=ListQuery)

    def filter(self, field_name: str, value: str) -> "ListRolesBuilder":
        return replace(self, _query=self._query.filter(field_name, value))

    def sort(self, expression: str) -> "ListRolesBuilder":
        return replace(self, _query=self._query.with_sort(expression))

    def limit(self, page_size: int) -> "ListRolesBuilder":
        return replace(self, _query=self._query.with_limit(page_size))

    def cursor(self, cursor: str) -> "ListRolesBuilder":
        return replace(self, _query=self._query.with_cursor(cursor))

    def send(self) -> Page[Role]:
        """Fetches one page."""
        page = self._transport.request_page("GET", "roles", params=self._query.params())
        return Page(
            items=tuple(Role.from_mapping(item) for item in page.items),
            next_cursor=page.next_cursor,
        )

    def __iter__(self) -> Iterator[Role]:
        """Iterates every role, following next_cursor until the end."""
        cursor = self._query.cursor
        seen: set[str] = set()

        while True:
            query = self._query if cursor is None else replace(self._query, cursor=cursor)
            page = replace(self, _query=query).send()
            yield from page.items

            next_cursor = page.next_cursor
            if not next_cursor or next_cursor in seen:
                return
            seen.add(next_cursor)
            cursor = next_cursor


@dataclass(frozen=True, slots=True)
class UpdateRoleBuilder:
    """Builds PATCH /api/v1/roles/{id}."""

    _transport: Transport = field(repr=False, compare=False)
    role_id: str
    _patch: Tuple[Tuple[str, Any], ...] = ()
    _if_match: str | None = None

    def with_name(self, name: str) -> "UpdateRoleBuilder":
        return replace(self, _patch=(*self._patch, ("name", name)))

    def with_description(self, description: str | None) -> "UpdateRoleBuilder":
        return replace(self, _patch=(*self._patch, ("description", description)))

    def with_permissions(self, *permissions: str) -> "UpdateRoleBuilder":
        return replace(self, _patch=(*self._patch, ("permissions", list(permissions))))

    def if_match(self, etag: str) -> "UpdateRoleBuilder":
        return replace(self, _if_match=etag)

    def send(self) -> Role:
        """Sends the request."""
        if not self._patch:
            raise ValueError("Set at least one field with with_*() before send().")

        payload = self._transport.request(
            "PATCH",
            f"roles/{self.role_id}",
            body=dict(self._patch),
            if_match=self._if_match,
        )
        return Role.from_mapping(payload)


@dataclass(frozen=True, slots=True)
class DeleteRoleBuilder:
    """Builds DELETE /api/v1/roles/{id}."""

    _transport: Transport = field(repr=False, compare=False)
    role_id: str
    _if_match: str | None = None

    def if_match(self, etag: str) -> "DeleteRoleBuilder":
        return replace(self, _if_match=etag)

    def send(self) -> None:
        """Sends the request."""
        self._transport.request_void("DELETE", f"roles/{self.role_id}", if_match=self._if_match)


@dataclass(frozen=True, slots=True)
class AssignRoleBuilder:
    """Builds POST /api/v1/roles/{id}/assign."""

    _transport: Transport = field(repr=False, compare=False)
    role_id: str
    subject_type: str | None = None
    subject_id: str | None = None
    idempotency_key: str | None = None

    def with_subject(self, subject_type: str, subject_id: str) -> "AssignRoleBuilder":
        """Sets the subject kind: account, legalEntity or group."""
        return replace(self, subject_type=subject_type, subject_id=str(subject_id))

    def with_idempotency_key(self, idempotency_key: str) -> "AssignRoleBuilder":
        """Overrides the generated idempotency key."""
        return replace(self, idempotency_key=idempotency_key)

    def send(self) -> None:
        """Sends the request."""
        self._send_to("assign")

    def _send_to(self, action: str) -> None:
        if not self.subject_type or not self.subject_id:
            raise ValueError("with_subject() is required before send().")

        self._transport.request_void(
            "POST",
            f"roles/{self.role_id}/{action}",
            body={"subjectType": self.subject_type, "subjectId": self.subject_id},
            idempotency_key=self.idempotency_key or str(uuid4()),
        )


@dataclass(frozen=True, slots=True)
class RevokeRoleBuilder(AssignRoleBuilder):
    """Builds POST /api/v1/roles/{id}/revoke."""

    def send(self) -> None:
        """Sends the request."""
        self._send_to("revoke")


class RolesResource:
    """Entry point for every role operation."""

    def __init__(self, transport: Transport) -> None:
        self._transport = transport

    def create(self) -> CreateRoleBuilder:
        return CreateRoleBuilder(self._transport)

    def get(self, role_id: str) -> GetRoleBuilder:
        return GetRoleBuilder(self._transport, str(role_id))

    def list(self) -> ListRolesBuilder:
        return ListRolesBuilder(self._transport)

    def update(self, role_id: str) -> UpdateRoleBuilder:
        return UpdateRoleBuilder(self._transport, str(role_id))

    def delete(self, role_id: str) -> DeleteRoleBuilder:
        return DeleteRoleBuilder(self._transport, str(role_id))

    def assign(self, role_id: str) -> AssignRoleBuilder:
        """Starts a request that grants the role to a subject."""
        return AssignRoleBuilder(self._transport, str(role_id))

    def revoke(self, role_id: str) -> RevokeRoleBuilder:
        """Starts a request that revokes the role from a subject."""
        return RevokeRoleBuilder(self._transport, str(role_id))