"""Account CRUD, cursor pagination, and error behaviour."""

from __future__ import annotations

import pytest

from nerves import AccountStatus, AccountType, NervesApiException

from helpers import ACCOUNT, ACCOUNT_ID, ENTITY_ID, FakeSession, make_client


def test_create_sends_the_documented_request() -> None:
    client, session = make_client()
    session.enqueue(201, ACCOUNT)

    created = (
        client.accounts.create()
        .for_entity(ENTITY_ID)
        .in_realm("default")
        .with_username("alice")
        .with_email("alice@example.com")
        .with_roles("member", "admin")
        .with_primary()
        .send()
    )

    assert created.username == "alice"
    assert created.roles == ("member",)

    call = session.calls[0]
    assert call.method == "POST"
    assert call.url == "https://nerves.test/api/v1/accounts"
    assert call.headers["Authorization"] == "Bearer pat-test-token"
    assert call.headers["Idempotency-Key"]
    assert call.json is not None
    assert call.json["legalEntityId"] == ENTITY_ID
    assert call.json["realm"] == "default"
    assert call.json["username"] == "alice"
    assert call.json["email"] == "alice@example.com"
    assert call.json["type"] == "personal"
    assert call.json["isPrimary"] is True
    assert call.json["roles"] == ["member", "admin"]


def test_create_rejects_a_personal_account_without_an_entity() -> None:
    client, session = make_client()

    with pytest.raises(ValueError, match="for_entity"):
        client.accounts.create().with_username("alice").send()

    assert session.calls == []


def test_create_lets_service_accounts_omit_the_entity() -> None:
    client, session = make_client()
    session.enqueue(201, ACCOUNT)

    client.accounts.create().with_type(AccountType.SERVICE).in_realm("internal").with_username("svc-billing").send()

    assert "legalEntityId" not in session.calls[0].json
    assert session.calls[0].json["type"] == "service"


def test_builders_are_immutable_and_reusable() -> None:
    client, session = make_client()
    session.enqueue(201, ACCOUNT).enqueue(201, ACCOUNT)

    base = client.accounts.create().for_entity(ENTITY_ID).with_username("alice")
    base.with_email("first@example.com").send()
    base.with_email("second@example.com").send()

    assert session.calls[0].json["email"] == "first@example.com"
    assert session.calls[1].json["email"] == "second@example.com"
    assert session.calls[0].json["username"] == "alice"


def test_get_reads_one_account_by_id() -> None:
    client, session = make_client()
    session.enqueue(200, ACCOUNT)

    account = client.accounts.get(ACCOUNT_ID).send()

    assert account.id == ACCOUNT_ID
    assert session.calls[0].method == "GET"
    assert session.calls[0].url == f"https://nerves.test/api/v1/accounts/{ACCOUNT_ID}"


def test_list_builds_a_cursor_query_and_returns_the_next_cursor() -> None:
    client, session = make_client()
    session.enqueue(200, {"items": [ACCOUNT], "nextCursor": "cursor-2"})

    page = (
        client.accounts.list()
        .for_entity(ENTITY_ID)
        .in_realm("default")
        .with_status(AccountStatus.ACTIVE)
        .filter("username", "alice")
        .sort("-createdAt")
        .limit(2)
        .send()
    )

    assert len(page.items) == 1
    assert page.next_cursor == "cursor-2"
    assert page.has_more is True

    params = dict(session.calls[0].params or [])
    assert params["legalEntityId"] == ENTITY_ID
    assert params["realm"] == "default"
    assert params["filter[status]"] == "active"
    assert params["filter[username]"] == "alice"
    assert params["sort"] == "-createdAt"
    assert params["limit"] == "2"


def test_list_iterator_follows_cursors_until_exhausted() -> None:
    client, session = make_client()
    session.enqueue(200, {"items": [ACCOUNT], "nextCursor": "cursor-2"})
    session.enqueue(200, {"items": [ACCOUNT], "nextCursor": None})

    usernames = [item.username for item in client.accounts.list().for_entity(ENTITY_ID).limit(1)]

    assert usernames == ["alice", "alice"]
    assert len(session.calls) == 2
    assert dict(session.calls[0].params or []).get("cursor") is None
    assert dict(session.calls[1].params or [])["cursor"] == "cursor-2"


def test_update_sends_only_changed_fields_with_if_match() -> None:
    client, session = make_client()
    session.enqueue(200, ACCOUNT)

    client.accounts.update(ACCOUNT_ID).with_display_name("Alice A.").if_match('"v1"').send()

    call = session.calls[0]
    assert call.method == "PATCH"
    assert call.headers["If-Match"] == '"v1"'
    assert call.json == {"displayName": "Alice A."}


def test_update_rejects_an_empty_patch() -> None:
    client, _ = make_client()

    with pytest.raises(ValueError, match="at least one field"):
        client.accounts.update(ACCOUNT_ID).send()


def test_delete_accepts_no_content() -> None:
    client, session = make_client()
    session.enqueue(204)

    client.accounts.delete(ACCOUNT_ID).if_match('"v1"').send()

    call = session.calls[0]
    assert call.method == "DELETE"
    assert call.url == f"https://nerves.test/api/v1/accounts/{ACCOUNT_ID}"
    assert call.headers["If-Match"] == '"v1"'


def test_problem_details_become_nerves_api_exception() -> None:
    client, session = make_client()
    session.enqueue(
        409,
        {
            "type": "https://nerves.dev/problems/conflict",
            "title": "Conflict",
            "status": 409,
            "detail": "username already exists",
            "traceId": "trace-42",
        },
        headers={"Content-Type": "application/problem+json"},
        reason="Conflict",
    )

    with pytest.raises(NervesApiException) as raised:
        client.accounts.create().for_entity(ENTITY_ID).with_username("alice").send()

    error = raised.value
    assert error.status == 409
    assert error.type == "https://nerves.dev/problems/conflict"
    assert error.trace_id == "trace-42"
    assert error.detail == "username already exists"
    assert error.problem.status == 409


def test_non_json_errors_carry_status_and_trace_header() -> None:
    client, session = make_client()
    session.enqueue(502, "upstream unavailable", headers={"X-Trace-Id": "trace-proxy"}, reason="Bad Gateway")

    with pytest.raises(NervesApiException) as raised:
        client.accounts.get(ACCOUNT_ID).send()

    error = raised.value
    assert error.status == 502
    assert error.trace_id == "trace-proxy"
    assert error.raw_body == "upstream unavailable"


def test_retries_transient_failures_with_the_same_idempotency_key() -> None:
    client, session = make_client(max_retries=1)
    session.enqueue(503, reason="Service Unavailable").enqueue(201, ACCOUNT)

    client.accounts.create().for_entity(ENTITY_ID).with_username("alice").send()

    assert len(session.calls) == 2
    assert session.calls[0].headers["Idempotency-Key"] == session.calls[1].headers["Idempotency-Key"]
