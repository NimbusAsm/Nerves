"""Chainable builders for /api/v1/accounts."""

from __future__ import annotations

from dataclasses import dataclass, field, replace
from typing import Any, Iterator, List, Tuple
from uuid import uuid4

from ..models import Account, AccountStatus, AccountType, wire
from ..pagination import ListQuery, Page
from ..transport import Transport


@dataclass(frozen=True, slots=True)
class CreateAccountBuilder:
    """Builds POST /api/v1/accounts. Immutable: every modifier returns a new builder."""

    _transport: Transport = field(repr=False, compare=False)
    legal_entity_id: str | None = None
    realm: str | None = None
    username: str | None = None
    email: str | None = None
    phone_number: str | None = None
    display_name: str | None = None
    account_type: str = AccountType.PERSONAL
    roles: Tuple[str, ...] = ()
    is_primary: bool | None = None
    idempotency_key: str | None = None

    def for_entity(self, legal_entity_id: str) -> "CreateAccountBuilder":
        return replace(self, legal_entity_id=str(legal_entity_id))

    def in_realm(self, realm: str) -> "CreateAccountBuilder":
        return replace(self, realm=realm)

    def with_username(self, username: str) -> "CreateAccountBuilder":
        return replace(self, username=username)

    def with_email(self, email: str | None) -> "CreateAccountBuilder":
        return replace(self, email=email)

    def with_phone_number(self, phone_number: str | None) -> "CreateAccountBuilder":
        return replace(self, phone_number=phone_number)

    def with_display_name(self, display_name: str | None) -> "CreateAccountBuilder":
        return replace(self, display_name=display_name)

    def with_type(self, account_type: AccountType | str) -> "CreateAccountBuilder":
        return replace(self, account_type=wire(account_type))

    def with_roles(self, *roles: str) -> "CreateAccountBuilder":
        return replace(self, roles=tuple(roles))

    def with_primary(self, is_primary: bool = True) -> "CreateAccountBuilder":
        return replace(self, is_primary=is_primary)

    def with_idempotency_key(self, idempotency_key: str) -> "CreateAccountBuilder":
        return replace(self, idempotency_key=idempotency_key)

    def send(self) -> Account:
        """Sends the request."""
        if not self.username or not self.username.strip():
            raise ValueError("with_username() is required before send().")

        account_type = wire(self.account_type)
        if account_type == AccountType.SERVICE.value and self.legal_entity_id is not None:
            raise ValueError("Service accounts cannot belong to a legal entity.")
        if account_type != AccountType.SERVICE.value and self.legal_entity_id is None:
            raise ValueError("Non-service accounts must call for_entity(...) before send().")

        body: dict[str, Any] = {"type": account_type, "username": self.username}
        if self.legal_entity_id is not None:
            body["legalEntityId"] = self.legal_entity_id
        if self.realm is not None:
            body["realm"] = self.realm
        if self.email is not None:
            body["email"] = self.email
        if self.phone_number is not None:
            body["phoneNumber"] = self.phone_number
        if self.display_name is not None:
            body["displayName"] = self.display_name
        if self.roles:
            body["roles"] = list(self.roles)
        if self.is_primary is not None:
            body["isPrimary"] = self.is_primary

        payload = self._transport.request(
            "POST",
            "accounts",
            body=body,
            idempotency_key=self.idempotency_key or str(uuid4()),
        )
        return Account.from_mapping(payload)


@dataclass(frozen=True, slots=True)
class GetAccountBuilder:
    """Builds GET /api/v1/accounts/{id}."""

    _transport: Transport = field(repr=False, compare=False)
    account_id: str

    def send(self) -> Account:
        payload = self._transport.request("GET", f"accounts/{self.account_id}")
        return Account.from_mapping(payload)


@dataclass(frozen=True, slots=True)
class ListAccountsBuilder:
    """Builds GET /api/v1/accounts with cursor pagination."""

    _transport: Transport = field(repr=False, compare=False)
    _query: ListQuery = field(default_factory=ListQuery)
    _legal_entity_id: str | None = None
    _realm: str | None = None
    _status: str | None = None

    def for_entity(self, legal_entity_id: str) -> "ListAccountsBuilder":
        return replace(self, _legal_entity_id=str(legal_entity_id))

    def in_realm(self, realm: str) -> "ListAccountsBuilder":
        return replace(self, _realm=realm)

    def with_status(self, status: AccountStatus | str) -> "ListAccountsBuilder":
        return replace(self, _status=wire(status))

    def filter(self, field_name: str, value: str) -> "ListAccountsBuilder":
        return replace(self, _query=self._query.filter(field_name, value))

    def sort(self, expression: str) -> "ListAccountsBuilder":
        return replace(self, _query=self._query.with_sort(expression))

    def limit(self, page_size: int) -> "ListAccountsBuilder":
        return replace(self, _query=self._query.with_limit(page_size))

    def cursor(self, cursor: str) -> "ListAccountsBuilder":
        return replace(self, _query=self._query.with_cursor(cursor))

    def send(self) -> Page[Account]:
        """Fetches one page."""
        params: List[Tuple[str, str]] = []
        if self._legal_entity_id is not None:
            params.append(("legalEntityId", self._legal_entity_id))
        if self._realm is not None:
            params.append(("realm", self._realm))
        if self._status is not None:
            params.append(("filter[status]", self._status))
        params.extend(self._query.params())

        page = self._transport.request_page("GET", "accounts", params=params)
        return Page(
            items=tuple(Account.from_mapping(item) for item in page.items),
            next_cursor=page.next_cursor,
        )

    def __iter__(self) -> Iterator[Account]:
        """Iterates every account, following next_cursor until the end."""
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
class UpdateAccountBuilder:
    """Builds PATCH /api/v1/accounts/{id}. Only fields set with with_* are sent."""

    _transport: Transport = field(repr=False, compare=False)
    account_id: str
    _patch: Tuple[Tuple[str, Any], ...] = ()
    _if_match: str | None = None

    def with_username(self, username: str) -> "UpdateAccountBuilder":
        return replace(self, _patch=(*self._patch, ("username", username)))

    def with_email(self, email: str | None) -> "UpdateAccountBuilder":
        return replace(self, _patch=(*self._patch, ("email", email)))

    def with_phone_number(self, phone_number: str | None) -> "UpdateAccountBuilder":
        return replace(self, _patch=(*self._patch, ("phoneNumber", phone_number)))

    def with_display_name(self, display_name: str | None) -> "UpdateAccountBuilder":
        return replace(self, _patch=(*self._patch, ("displayName", display_name)))

    def with_status(self, status: AccountStatus | str) -> "UpdateAccountBuilder":
        return replace(self, _patch=(*self._patch, ("status", wire(status))))

    def with_roles(self, *roles: str) -> "UpdateAccountBuilder":
        return replace(self, _patch=(*self._patch, ("roles", list(roles))))

    def if_match(self, etag: str) -> "UpdateAccountBuilder":
        return replace(self, _if_match=etag)

    def send(self) -> Account:
        """Sends the request."""
        if not self._patch:
            raise ValueError("Set at least one field with with_*() before send().")

        body = {key: (list(value) if isinstance(value, tuple) else value) for key, value in self._patch}
        payload = self._transport.request(
            "PATCH",
            f"accounts/{self.account_id}",
            body=body,
            if_match=self._if_match,
        )
        return Account.from_mapping(payload)


@dataclass(frozen=True, slots=True)
class DeleteAccountBuilder:
    """Builds DELETE /api/v1/accounts/{id}."""

    _transport: Transport = field(repr=False, compare=False)
    account_id: str
    _if_match: str | None = None

    def if_match(self, etag: str) -> "DeleteAccountBuilder":
        return replace(self, _if_match=etag)

    def send(self) -> None:
        """Sends the request."""
        self._transport.request_void("DELETE", f"accounts/{self.account_id}", if_match=self._if_match)


class AccountsResource:
    """Entry point for every account operation."""

    def __init__(self, transport: Transport) -> None:
        self._transport = transport

    def create(self) -> CreateAccountBuilder:
        return CreateAccountBuilder(self._transport)

    def get(self, account_id: str) -> GetAccountBuilder:
        return GetAccountBuilder(self._transport, str(account_id))

    def list(self) -> ListAccountsBuilder:
        return ListAccountsBuilder(self._transport)

    def update(self, account_id: str) -> UpdateAccountBuilder:
        return UpdateAccountBuilder(self._transport, str(account_id))

    def delete(self, account_id: str) -> DeleteAccountBuilder:
        return DeleteAccountBuilder(self._transport, str(account_id))
