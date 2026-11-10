"""Chainable builders for /api/v1/groups."""

from __future__ import annotations

from dataclasses import dataclass, field, replace
from typing import List
from uuid import uuid4

from ..models import Group, GroupDetail, GroupMember
from ..transport import Transport


@dataclass(frozen=True, slots=True)
class ListGroupsBuilder:
    """Builds GET /api/v1/groups; the endpoint returns a plain array."""

    _transport: Transport = field(repr=False, compare=False)
    realm: str | None = None

    def in_realm(self, realm: str) -> "ListGroupsBuilder":
        """Restricts the result to one realm."""
        return replace(self, realm=realm)

    def send(self) -> List[Group]:
        """Sends the request."""
        params = [("realm", self.realm)] if self.realm is not None else None
        payload = self._transport.request("GET", "groups", params=params)
        return [Group.from_mapping(item) for item in payload]


@dataclass(frozen=True, slots=True)
class CreateGroupBuilder:
    """Builds POST /api/v1/groups."""

    _transport: Transport = field(repr=False, compare=False)
    realm: str | None = None
    code: str | None = None
    name: str | None = None
    idempotency_key: str | None = None

    def in_realm(self, realm: str) -> "CreateGroupBuilder":
        """Sets the realm."""
        return replace(self, realm=realm)

    def with_code(self, code: str) -> "CreateGroupBuilder":
        """Sets the unique code."""
        return replace(self, code=code)

    def with_name(self, name: str) -> "CreateGroupBuilder":
        """Sets the display name."""
        return replace(self, name=name)

    def with_idempotency_key(self, idempotency_key: str) -> "CreateGroupBuilder":
        """Overrides the generated idempotency key."""
        return replace(self, idempotency_key=idempotency_key)

    def send(self) -> Group:
        """Sends the request."""
        if not self.realm or not self.realm.strip():
            raise ValueError("in_realm() is required before send().")
        if not self.code or not self.code.strip():
            raise ValueError("with_code() is required before send().")
        if not self.name or not self.name.strip():
            raise ValueError("with_name() is required before send().")

        payload = self._transport.request(
            "POST",
            "groups",
            body={"realm": self.realm, "code": self.code, "name": self.name},
            idempotency_key=self.idempotency_key or str(uuid4()),
        )
        return Group.from_mapping(payload)


@dataclass(frozen=True, slots=True)
class GetGroupBuilder:
    """Builds GET /api/v1/groups/{id}."""

    _transport: Transport = field(repr=False, compare=False)
    group_id: str

    def send(self) -> GroupDetail:
        """Sends the request."""
        payload = self._transport.request("GET", f"groups/{self.group_id}")
        return GroupDetail.from_mapping(payload)


@dataclass(frozen=True, slots=True)
class DeleteGroupBuilder:
    """Builds DELETE /api/v1/groups/{id}."""

    _transport: Transport = field(repr=False, compare=False)
    group_id: str

    def send(self) -> None:
        """Sends the request."""
        self._transport.request_void("DELETE", f"groups/{self.group_id}")


@dataclass(frozen=True, slots=True)
class ListGroupMembersBuilder:
    """Builds GET /api/v1/groups/{id}/members."""

    _transport: Transport = field(repr=False, compare=False)
    group_id: str

    def send(self) -> List[GroupMember]:
        """Sends the request."""
        payload = self._transport.request("GET", f"groups/{self.group_id}/members")
        return [GroupMember.from_mapping(item) for item in payload]


@dataclass(frozen=True, slots=True)
class AddGroupMemberBuilder:
    """Builds POST /api/v1/groups/{id}/members."""

    _transport: Transport = field(repr=False, compare=False)
    group_id: str
    account_id: str | None = None
    idempotency_key: str | None = None

    def with_account(self, account_id: str) -> "AddGroupMemberBuilder":
        """Sets the account to add."""
        return replace(self, account_id=str(account_id))

    def with_idempotency_key(self, idempotency_key: str) -> "AddGroupMemberBuilder":
        """Overrides the generated idempotency key."""
        return replace(self, idempotency_key=idempotency_key)

    def send(self) -> None:
        """Sends the request."""
        if not self.account_id:
            raise ValueError("with_account() is required before send().")

        self._transport.request_void(
            "POST",
            f"groups/{self.group_id}/members",
            body={"accountId": self.account_id},
            idempotency_key=self.idempotency_key or str(uuid4()),
        )


@dataclass(frozen=True, slots=True)
class RemoveGroupMemberBuilder:
    """Builds DELETE /api/v1/groups/{id}/members/{accountId}."""

    _transport: Transport = field(repr=False, compare=False)
    group_id: str
    account_id: str

    def send(self) -> None:
        """Sends the request."""
        self._transport.request_void("DELETE", f"groups/{self.group_id}/members/{self.account_id}")


class GroupsResource:
    """Entry point for groups and their members."""

    def __init__(self, transport: Transport) -> None:
        self._transport = transport

    def list(self) -> ListGroupsBuilder:
        """Starts a list request."""
        return ListGroupsBuilder(self._transport)

    def create(self) -> CreateGroupBuilder:
        """Starts a create request."""
        return CreateGroupBuilder(self._transport)

    def get(self, group_id: str) -> GetGroupBuilder:
        """Starts a read request for one group."""
        return GetGroupBuilder(self._transport, str(group_id))

    def delete(self, group_id: str) -> DeleteGroupBuilder:
        """Starts a delete request for one group."""
        return DeleteGroupBuilder(self._transport, str(group_id))

    def members(self, group_id: str) -> ListGroupMembersBuilder:
        """Starts a member list request."""
        return ListGroupMembersBuilder(self._transport, str(group_id))

    def add_member(self, group_id: str) -> AddGroupMemberBuilder:
        """Starts an add-member request."""
        return AddGroupMemberBuilder(self._transport, str(group_id))

    def remove_member(self, group_id: str, account_id: str) -> RemoveGroupMemberBuilder:
        """Starts a remove-member request."""
        return RemoveGroupMemberBuilder(self._transport, str(group_id), str(account_id))
