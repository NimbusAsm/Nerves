"""Client lifecycle and injection."""

from __future__ import annotations

import nerves
from nerves import NervesClient

from helpers import FakeSession, make_client


def test_injected_session_is_not_closed() -> None:
    client, session = make_client()

    client.close()

    assert session.closed is False


def test_context_manager_closes_an_owned_session(monkeypatch) -> None:
    owned = FakeSession()
    monkeypatch.setattr("nerves.transport.requests.Session", lambda: owned)

    with NervesClient("https://nerves.test", "pat"):
        pass

    assert owned.closed is True


def test_module_level_client_factory_is_a_context_manager(monkeypatch) -> None:
    owned = FakeSession()
    monkeypatch.setattr("nerves.transport.requests.Session", lambda: owned)

    with nerves.client("https://nerves.test", token="pat") as created:
        assert isinstance(created, NervesClient)

    assert owned.closed is True


def test_base_url_and_custom_headers_are_applied() -> None:
    client, session = make_client(headers={"X-Tenant": "acme"})
    session.enqueue(200, {"items": [], "nextCursor": None})

    client.roles.list().send()

    call = session.calls[0]
    assert call.url == "https://nerves.test/api/v1/roles"
    assert call.headers["X-Tenant"] == "acme"
    assert call.headers["Authorization"] == "Bearer pat-test-token"