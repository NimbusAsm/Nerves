"""Legal entity create/list/update/delete."""

from __future__ import annotations

import pytest

from nerves import LegalEntityType, VerificationState

from helpers import ENTITY_ID, LEGAL_ENTITY, make_client


def test_create_sends_the_document_once_and_maps_the_response() -> None:
    client, session = make_client()
    session.enqueue(201, LEGAL_ENTITY)

    entity = (
        client.legal_entities.create()
        .with_type(LegalEntityType.NATURAL_PERSON)
        .with_display_name("Alice")
        .with_document("passport", "E12345678", "CN")
        .send()
    )

    assert entity.id == ENTITY_ID
    assert entity.real_name is not None
    assert entity.real_name.document_type == "passport"
    assert entity.verification_state == "pending"

    call = session.calls[0]
    assert call.url == "https://nerves.test/api/v1/legal-entities"
    assert call.json["displayName"] == "Alice"
    assert call.json["type"] == "naturalPerson"
    assert call.json["realName"] == {
        "documentType": "passport",
        "number": "E12345678",
        "country": "CN",
    }


def test_create_requires_a_document() -> None:
    client, _ = make_client()

    with pytest.raises(ValueError, match="with_document"):
        client.legal_entities.create().with_display_name("Alice").send()


def test_list_filters_by_state_and_type_with_a_cursor() -> None:
    client, session = make_client()
    session.enqueue(200, {"items": [LEGAL_ENTITY], "nextCursor": "next"})

    page = (
        client.legal_entities.list()
        .with_verification_state(VerificationState.VERIFIED)
        .with_type(LegalEntityType.LEGAL_PERSON)
        .sort("displayName")
        .limit(10)
        .send()
    )

    assert page.next_cursor == "next"
    params = dict(session.calls[0].params or [])
    assert params["filter[verificationState]"] == "verified"
    assert params["filter[type]"] == "legalPerson"
    assert params["sort"] == "displayName"
    assert params["limit"] == "10"


def test_iterator_follows_cursors() -> None:
    client, session = make_client()
    session.enqueue(200, {"items": [LEGAL_ENTITY], "nextCursor": "next"})
    session.enqueue(200, {"items": [LEGAL_ENTITY], "nextCursor": None})

    ids = [entity.id for entity in client.legal_entities.list()]

    assert ids == [ENTITY_ID, ENTITY_ID]
    assert dict(session.calls[1].params or [])["cursor"] == "next"


def test_update_patches_verification_state_with_if_match() -> None:
    client, session = make_client()
    session.enqueue(200, LEGAL_ENTITY)

    client.legal_entities.update(ENTITY_ID).with_verification_state(
        VerificationState.VERIFIED
    ).with_provider_reference("provider-7").if_match('"v2"').send()

    call = session.calls[0]
    assert call.method == "PATCH"
    assert call.headers["If-Match"] == '"v2"'
    assert call.json == {"verificationState": "verified", "providerReference": "provider-7"}


def test_delete_targets_the_legal_entities_path() -> None:
    client, session = make_client()
    session.enqueue(204)

    client.legal_entities.delete(ENTITY_ID).send()

    assert session.calls[0].url == f"https://nerves.test/api/v1/legal-entities/{ENTITY_ID}"
