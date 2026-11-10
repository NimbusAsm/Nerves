"""Chainable builders for /api/v1/notifications."""

from __future__ import annotations

from dataclasses import dataclass, field, replace
from typing import Iterator, List, Mapping, Tuple
from uuid import uuid4

from ..models import NotificationMessage
from ..pagination import Page
from ..transport import Transport


@dataclass(frozen=True, slots=True)
class SendEmailBuilder:
    """Builds POST /api/v1/notifications/email."""

    _transport: Transport = field(repr=False, compare=False)
    recipient: str | None = None
    template: str | None = None
    values: Tuple[Tuple[str, str], ...] = ()
    idempotency_key: str | None = None

    def to(self, address: str) -> "SendEmailBuilder":
        """Sets the recipient address."""
        return replace(self, recipient=address)

    def with_template(self, template: str) -> "SendEmailBuilder":
        """Sets the template code."""
        return replace(self, template=template)

    def with_value(self, key: str, value: str) -> "SendEmailBuilder":
        """Sets one template value."""
        return replace(self, values=(*self.values, (key, value)))

    def with_values(self, values: Mapping[str, str]) -> "SendEmailBuilder":
        """Replaces every template value."""
        return replace(self, values=tuple(values.items()))

    def with_idempotency_key(self, idempotency_key: str) -> "SendEmailBuilder":
        """Overrides the generated idempotency key."""
        return replace(self, idempotency_key=idempotency_key)

    def send(self) -> NotificationMessage:
        """Sends the request."""
        if not self.recipient or not self.recipient.strip():
            raise ValueError("to() is required before send().")
        if not self.template or not self.template.strip():
            raise ValueError("with_template() is required before send().")

        body: dict[str, object] = {"to": self.recipient, "template": self.template}
        if self.values:
            body["values"] = dict(self.values)

        payload = self._transport.request(
            "POST",
            "notifications/email",
            body=body,
            idempotency_key=self.idempotency_key or str(uuid4()),
        )
        return NotificationMessage.from_mapping(payload)


@dataclass(frozen=True, slots=True)
class ListNotificationsBuilder:
    """Builds GET /api/v1/notifications with cursor pagination."""

    _transport: Transport = field(repr=False, compare=False)
    _status: str | None = None
    _recipient: str | None = None
    _limit: int | None = None
    _cursor: str | None = None

    def with_status(self, status: str) -> "ListNotificationsBuilder":
        """Restricts the result to pending, sent or failed."""
        return replace(self, _status=status)

    def to(self, address: str) -> "ListNotificationsBuilder":
        """Restricts the result to one recipient."""
        return replace(self, _recipient=address)

    def limit(self, page_size: int) -> "ListNotificationsBuilder":
        """Sets the page size."""
        return replace(self, _limit=page_size)

    def cursor(self, cursor: str) -> "ListNotificationsBuilder":
        """Continues from an opaque cursor."""
        return replace(self, _cursor=cursor)

    def send(self) -> Page[NotificationMessage]:
        """Fetches one page."""
        page = self._transport.request_page("GET", "notifications", params=self._params())
        return Page(
            items=tuple(NotificationMessage.from_mapping(item) for item in page.items),
            next_cursor=page.next_cursor,
        )

    def __iter__(self) -> Iterator[NotificationMessage]:
        """Iterates every matching notification, following next_cursor."""
        cursor = self._cursor
        seen: set[str] = set()

        while True:
            page = replace(self, _cursor=cursor).send()
            yield from page.items

            next_cursor = page.next_cursor
            if not next_cursor or next_cursor in seen:
                return
            seen.add(next_cursor)
            cursor = next_cursor

    def _params(self) -> List[Tuple[str, str]]:
        entries = (
            ("status", self._status),
            ("to", self._recipient),
            ("limit", None if self._limit is None else str(self._limit)),
            ("cursor", self._cursor),
        )
        return [(key, value) for key, value in entries if value is not None]


@dataclass(frozen=True, slots=True)
class GetNotificationBuilder:
    """Builds GET /api/v1/notifications/{id}."""

    _transport: Transport = field(repr=False, compare=False)
    notification_id: str

    def send(self) -> NotificationMessage:
        """Sends the request."""
        payload = self._transport.request("GET", f"notifications/{self.notification_id}")
        return NotificationMessage.from_mapping(payload)


class NotificationsResource:
    """Entry point for the notification delivery log."""

    def __init__(self, transport: Transport) -> None:
        self._transport = transport

    def send_email(self) -> SendEmailBuilder:
        """Starts an email send request."""
        return SendEmailBuilder(self._transport)

    def list(self) -> ListNotificationsBuilder:
        """Starts a list request."""
        return ListNotificationsBuilder(self._transport)

    def get(self, notification_id: str) -> GetNotificationBuilder:
        """Starts a read request for one notification."""
        return GetNotificationBuilder(self._transport, str(notification_id))