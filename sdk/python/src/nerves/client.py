"""The chainable client entry point."""

from __future__ import annotations

from typing import Mapping

import requests

from .resources.accounts import AccountsResource
from .resources.audit import AuditResource
from .resources.compliance import ComplianceResource
from .resources.decisions import DecisionsResource
from .resources.events import EventsResource
from .resources.groups import GroupsResource
from .resources.legal_entities import LegalEntitiesResource
from .resources.notifications import NotificationsResource
from .resources.permissions import PermissionsResource
from .resources.roles import RolesResource
from .resources.sessions import SessionsResource
from .resources.subjects import SubjectsResource
from .transport import NervesClientOptions, Transport


class NervesClient:
    """Entry point of the chainable client.

    Every operation follows entry -> verb -> modifier -> terminal:

        account = (nerves.accounts.create()
            .for_entity(entity_id)
            .with_username("alice")
            .send())
    """

    def __init__(
        self,
        base_url: str,
        token: str | None = None,
        *,
        session: requests.Session | None = None,
        headers: Mapping[str, str] | None = None,
        timeout: float = 30.0,
        max_retries: int = 2,
        retry_base_delay: float = 0.2,
        api_base_path: str = "/api/v1",
    ) -> None:
        options = NervesClientOptions(
            base_url=base_url,
            token=token,
            session=session,
            headers=headers or {},
            timeout=timeout,
            max_retries=max_retries,
            retry_base_delay=retry_base_delay,
            api_base_path=api_base_path,
        )
        self._transport = Transport(options)
        self.accounts = AccountsResource(self._transport)
        self.legal_entities = LegalEntitiesResource(self._transport)
        self.roles = RolesResource(self._transport)
        self.permissions = PermissionsResource(self._transport)
        self.groups = GroupsResource(self._transport)
        self.decisions = DecisionsResource(self._transport)
        self.sessions = SessionsResource(self._transport)
        self.audit = AuditResource(self._transport)
        self.compliance = ComplianceResource(self._transport)
        self.subjects = SubjectsResource(self._transport)
        self.notifications = NotificationsResource(self._transport)
        self.events = EventsResource(self._transport)

    def close(self) -> None:
        """Closes the underlying session when the client owns it."""
        self._transport.close()

    def __enter__(self) -> "NervesClient":
        return self

    def __exit__(self, exc_type: object, exc: object, traceback: object) -> None:
        self.close()


def client(base_url: str, token: str | None = None, **kwargs: object) -> NervesClient:
    """Creates a client; use it as a context manager to close owned sessions."""
    return NervesClient(base_url, token, **kwargs)  # type: ignore[arg-type]