"""Chainable builders for /api/v1/legal-entities."""

from __future__ import annotations

from dataclasses import dataclass, field, replace
from typing import Any, Iterator, List, Tuple
from uuid import uuid4

from ..models import IdDocumentType, LegalEntity, LegalEntityType, VerificationState, wire
from ..pagination import ListQuery, Page
from ..transport import Transport


@dataclass(frozen=True, slots=True)
class CreateLegalEntityBuilder:
    """Builds POST /api/v1/legal-entities."""

    _transport: Transport = field(repr=False, compare=False)
    entity_type: str = LegalEntityType.NATURAL_PERSON
    display_name: str | None = None
    document_type: str | None = None
    document_number: str | None = None
    country: str | None = None
    idempotency_key: str | None = None

    def with_type(self, entity_type: LegalEntityType | str) -> "CreateLegalEntityBuilder":
        return replace(self, entity_type=wire(entity_type))

    def with_display_name(self, display_name: str) -> "CreateLegalEntityBuilder":
        return replace(self, display_name=display_name)

    def with_document(
        self,
        document_type: IdDocumentType | str,
        number: str,
        country: str,
    ) -> "CreateLegalEntityBuilder":
        """Supplies the identity document; the number is sent once and never returned."""
        return replace(
            self,
            document_type=wire(document_type),
            document_number=number,
            country=country,
        )

    def with_idempotency_key(self, idempotency_key: str) -> "CreateLegalEntityBuilder":
        return replace(self, idempotency_key=idempotency_key)

    def send(self) -> LegalEntity:
        """Sends the request."""
        if not self.display_name or not self.display_name.strip():
            raise ValueError("with_display_name() is required before send().")
        if not self.document_type or not self.document_number or not self.country:
            raise ValueError("with_document(...) is required before send().")

        body = {
            "type": wire(self.entity_type),
            "displayName": self.display_name,
            "realName": {
                "documentType": self.document_type,
                "number": self.document_number,
                "country": self.country,
            },
        }
        payload = self._transport.request(
            "POST",
            "legal-entities",
            body=body,
            idempotency_key=self.idempotency_key or str(uuid4()),
        )
        return LegalEntity.from_mapping(payload)


@dataclass(frozen=True, slots=True)
class GetLegalEntityBuilder:
    """Builds GET /api/v1/legal-entities/{id}."""

    _transport: Transport = field(repr=False, compare=False)
    entity_id: str

    def send(self) -> LegalEntity:
        payload = self._transport.request("GET", f"legal-entities/{self.entity_id}")
        return LegalEntity.from_mapping(payload)


@dataclass(frozen=True, slots=True)
class ListLegalEntitiesBuilder:
    """Builds GET /api/v1/legal-entities with cursor pagination."""

    _transport: Transport = field(repr=False, compare=False)
    _query: ListQuery = field(default_factory=ListQuery)
    _verification_state: str | None = None
    _entity_type: str | None = None

    def with_verification_state(self, state: VerificationState | str) -> "ListLegalEntitiesBuilder":
        return replace(self, _verification_state=wire(state))

    def with_type(self, entity_type: LegalEntityType | str) -> "ListLegalEntitiesBuilder":
        return replace(self, _entity_type=wire(entity_type))

    def filter(self, field_name: str, value: str) -> "ListLegalEntitiesBuilder":
        return replace(self, _query=self._query.filter(field_name, value))

    def sort(self, expression: str) -> "ListLegalEntitiesBuilder":
        return replace(self, _query=self._query.with_sort(expression))

    def limit(self, page_size: int) -> "ListLegalEntitiesBuilder":
        return replace(self, _query=self._query.with_limit(page_size))

    def cursor(self, cursor: str) -> "ListLegalEntitiesBuilder":
        return replace(self, _query=self._query.with_cursor(cursor))

    def send(self) -> Page[LegalEntity]:
        """Fetches one page."""
        params: List[Tuple[str, str]] = []
        if self._verification_state is not None:
            params.append(("filter[verificationState]", self._verification_state))
        if self._entity_type is not None:
            params.append(("filter[type]", self._entity_type))
        params.extend(self._query.params())

        page = self._transport.request_page("GET", "legal-entities", params=params)
        return Page(
            items=tuple(LegalEntity.from_mapping(item) for item in page.items),
            next_cursor=page.next_cursor,
        )

    def __iter__(self) -> Iterator[LegalEntity]:
        """Iterates every legal entity, following next_cursor until the end."""
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
class UpdateLegalEntityBuilder:
    """Builds PATCH /api/v1/legal-entities/{id}."""

    _transport: Transport = field(repr=False, compare=False)
    entity_id: str
    _patch: Tuple[Tuple[str, Any], ...] = ()
    _if_match: str | None = None

    def with_display_name(self, display_name: str) -> "UpdateLegalEntityBuilder":
        return replace(self, _patch=(*self._patch, ("displayName", display_name)))

    def with_verification_state(self, state: VerificationState | str) -> "UpdateLegalEntityBuilder":
        return replace(self, _patch=(*self._patch, ("verificationState", wire(state))))

    def with_provider_reference(self, provider_reference: str | None) -> "UpdateLegalEntityBuilder":
        return replace(self, _patch=(*self._patch, ("providerReference", provider_reference)))

    def if_match(self, etag: str) -> "UpdateLegalEntityBuilder":
        return replace(self, _if_match=etag)

    def send(self) -> LegalEntity:
        """Sends the request."""
        if not self._patch:
            raise ValueError("Set at least one field with with_*() before send().")

        payload = self._transport.request(
            "PATCH",
            f"legal-entities/{self.entity_id}",
            body=dict(self._patch),
            if_match=self._if_match,
        )
        return LegalEntity.from_mapping(payload)


@dataclass(frozen=True, slots=True)
class DeleteLegalEntityBuilder:
    """Builds DELETE /api/v1/legal-entities/{id}."""

    _transport: Transport = field(repr=False, compare=False)
    entity_id: str
    _if_match: str | None = None

    def if_match(self, etag: str) -> "DeleteLegalEntityBuilder":
        return replace(self, _if_match=etag)

    def send(self) -> None:
        """Sends the request."""
        self._transport.request_void(
            "DELETE",
            f"legal-entities/{self.entity_id}",
            if_match=self._if_match,
        )


class LegalEntitiesResource:
    """Entry point for every legal-entity operation."""

    def __init__(self, transport: Transport) -> None:
        self._transport = transport

    def create(self) -> CreateLegalEntityBuilder:
        return CreateLegalEntityBuilder(self._transport)

    def get(self, entity_id: str) -> GetLegalEntityBuilder:
        return GetLegalEntityBuilder(self._transport, str(entity_id))

    def list(self) -> ListLegalEntitiesBuilder:
        return ListLegalEntitiesBuilder(self._transport)

    def update(self, entity_id: str) -> UpdateLegalEntityBuilder:
        return UpdateLegalEntityBuilder(self._transport, str(entity_id))

    def delete(self, entity_id: str) -> DeleteLegalEntityBuilder:
        return DeleteLegalEntityBuilder(self._transport, str(entity_id))
