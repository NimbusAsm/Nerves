"""Role create/list/update/delete."""

from __future__ import annotations

import pytest

from helpers import ROLE, ROLE_ID, make_client


def test_create_sends_name_description_and_permissions() -> None:
    client, session = make_client()
    session.enqueue(201, ROLE)

    role = (
        client.roles.create()
        .with_name("member")
        .with_description("Members")
        .with_permissions("accounts.read", "accounts.list")
        .send()
    )

    assert role.name == "member"
    assert role.permissions == ("accounts.read",)

    call = session.calls[0]
    assert call.url == "https://nerves.test/api/v1/roles"
    assert call.headers["Idempotency-Key"]
    assert call.json["name"] == "member"
    assert call.json["permissions"] == ["accounts.read", "accounts.list"]


def test_create_requires_a_name() -> None:
    client, _ = make_client()

    with pytest.raises(ValueError, match="with_name"):
        client.roles.create().send()


def test_get_uses_the_role_path() -> None:
    client, session = make_client()
    session.enqueue(200, ROLE)

    role = client.roles.get(ROLE_ID).send()

    assert role.id == ROLE_ID
    assert session.calls[0].url == f"https://nerves.test/api/v1/roles/{ROLE_ID}"


def test_list_supports_filter_sort_limit_and_cursor() -> None:
    client, session = make_client()
    session.enqueue(200, {"items": [ROLE], "nextCursor": "c1"})

    page = client.roles.list().filter("name", "member").sort("name").limit(1).cursor("start").send()

    assert page.items[0].name == "member"
    assert page.next_cursor == "c1"

    params = dict(session.calls[0].params or [])
    assert params["filter[name]"] == "member"
    assert params["sort"] == "name"
    assert params["limit"] == "1"
    assert params["cursor"] == "start"


def test_iterator_walks_every_page() -> None:
    client, session = make_client()
    session.enqueue(200, {"items": [ROLE], "nextCursor": "c2"})
    session.enqueue(200, {"items": [ROLE], "nextCursor": None})

    names = [role.name for role in client.roles.list()]

    assert names == ["member", "member"]
    assert dict(session.calls[1].params or [])["cursor"] == "c2"


def test_update_replaces_permissions_and_delete_is_empty() -> None:
    client, session = make_client()
    session.enqueue(200, ROLE).enqueue(204)

    client.roles.update(ROLE_ID).with_permissions("roles.read").send()
    client.roles.delete(ROLE_ID).send()

    assert session.calls[0].json == {"permissions": ["roles.read"]}
    assert session.calls[1].method == "DELETE"
