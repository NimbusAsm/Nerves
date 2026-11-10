"""HTTP transport shared by every builder."""

from __future__ import annotations

import json
import time
from dataclasses import dataclass, field, replace
from typing import Any, Mapping, MutableMapping, Sequence, Tuple

import requests

from .errors import NervesApiException, ProblemDetails
from .pagination import Page


@dataclass(frozen=True, slots=True)
class NervesClientOptions:
    """Everything the client needs: origin, credential, and transport tuning."""

    base_url: str
    token: str | None = None
    session: requests.Session | None = None
    headers: Mapping[str, str] = field(default_factory=dict)
    timeout: float = 30.0
    max_retries: int = 2
    retry_base_delay: float = 0.2
    api_base_path: str = "/api/v1"


class Transport:
    """Small requests-based transport with retries and ProblemDetails parsing."""

    def __init__(self, options: NervesClientOptions) -> None:
        if not options.base_url:
            raise ValueError("base_url is required")
        self._root = options.base_url.rstrip("/") + options.api_base_path
        self._token = options.token or None
        self._headers = dict(options.headers)
        self._timeout = options.timeout
        self._max_retries = max(0, options.max_retries)
        self._retry_base_delay = max(0.0, options.retry_base_delay)
        self._session = options.session or requests.Session()
        self._owns_session = options.session is None

    def close(self) -> None:
        if self._owns_session:
            self._session.close()

    def request(
        self,
        method: str,
        path: str,
        *,
        params: Sequence[Tuple[str, str]] | None = None,
        body: Mapping[str, Any] | None = None,
        idempotency_key: str | None = None,
        if_match: str | None = None,
    ) -> Any:
        text = self._send(
            method,
            path,
            params=params,
            body=body,
            idempotency_key=idempotency_key,
            if_match=if_match,
        )
        if not text.strip():
            raise NervesApiException(
                200,
                ProblemDetails(
                    status=200,
                    title="The API returned an empty body where a document was expected.",
                ),
            )
        return json.loads(text)

    def request_page(
        self,
        method: str,
        path: str,
        *,
        params: Sequence[Tuple[str, str]] | None = None,
    ) -> Page[Any]:
        payload = self.request(method, path, params=params)
        items = payload.get("items") if isinstance(payload, Mapping) else None
        next_cursor = payload.get("nextCursor") if isinstance(payload, Mapping) else None
        return Page(
            items=tuple(items or ()),
            next_cursor=next_cursor if isinstance(next_cursor, str) and next_cursor else None,
        )

    def request_void(
        self,
        method: str,
        path: str,
        *,
        params: Sequence[Tuple[str, str]] | None = None,
        body: Mapping[str, Any] | None = None,
        idempotency_key: str | None = None,
        if_match: str | None = None,
    ) -> None:
        self._send(
            method,
            path,
            params=params,
            body=body,
            idempotency_key=idempotency_key,
            if_match=if_match,
        )

    def request_file(
        self,
        method: str,
        path: str,
        *,
        params: Sequence[Tuple[str, str]] | None = None,
        accept: str = "text/csv, application/json",
    ) -> "DownloadedFile":
        response = self._send_response(
            method,
            path,
            params=params,
            accept=accept,
        )
        disposition = response.headers.get("Content-Disposition") or response.headers.get(
            "content-disposition", ""
        )
        return DownloadedFile(
            content=response.content,
            content_type=(response.headers.get("Content-Type") or "application/octet-stream").split(";")[0].strip(),
            file_name=_file_name(disposition),
        )

    def _send(
        self,
        method: str,
        path: str,
        *,
        params: Sequence[Tuple[str, str]] | None = None,
        body: Mapping[str, Any] | None = None,
        idempotency_key: str | None = None,
        if_match: str | None = None,
    ) -> str:
        return self._send_response(
            method,
            path,
            params=params,
            body=body,
            idempotency_key=idempotency_key,
            if_match=if_match,
        ).text

    def _send_response(
        self,
        method: str,
        path: str,
        *,
        params: Sequence[Tuple[str, str]] | None = None,
        body: Mapping[str, Any] | None = None,
        idempotency_key: str | None = None,
        if_match: str | None = None,
        accept: str = "application/json",
    ) -> requests.Response:
        url = f"{self._root}/{path.lstrip('/')}"

        for attempt in range(self._max_retries + 1):
            headers: MutableMapping[str, str] = {
                "Accept": accept,
                **self._headers,
            }
            if body is not None:
                headers["Content-Type"] = "application/json"
            if self._token is not None:
                headers["Authorization"] = f"Bearer {self._token}"
            if idempotency_key is not None:
                headers["Idempotency-Key"] = idempotency_key
            if if_match is not None:
                headers["If-Match"] = if_match

            try:
                response = self._session.request(
                    method,
                    url,
                    params=list(params) if params is not None else None,
                    json=body,
                    headers=headers,
                    timeout=(5.0, self._timeout),
                )
            except requests.RequestException:
                if attempt < self._max_retries:
                    time.sleep(self._delay(attempt))
                    continue
                raise

            if response.status_code < 400:
                return response

            if (response.status_code == 429 or response.status_code >= 500) and attempt < self._max_retries:
                retry_after = response.headers.get("Retry-After")
                delay = self._delay(attempt)
                if retry_after is not None:
                    try:
                        delay = max(0.0, float(retry_after))
                    except ValueError:
                        pass
                time.sleep(delay)
                continue

            raise self._error(response)

        raise AssertionError("unreachable")  # pragma: no cover - loop always returns or raises

    def _delay(self, attempt: int) -> float:
        return self._retry_base_delay * (2**attempt)

    def _error(self, response: requests.Response) -> NervesApiException:
        body = response.text
        problem = ProblemDetails(status=response.status_code, title=response.reason or None)

        if body.strip():
            try:
                payload = json.loads(body)
            except json.JSONDecodeError:
                payload = None

            if isinstance(payload, Mapping):
                problem = ProblemDetails.from_mapping(payload)
                if problem.status is None:
                    problem = replace(problem, status=response.status_code)

        if problem.trace_id is None:
            header_trace = response.headers.get("X-Trace-Id") or response.headers.get("x-trace-id")
            if header_trace:
                problem = replace(problem, trace_id=header_trace)

        return NervesApiException(response.status_code, problem, body)

@dataclass(frozen=True, slots=True)
class DownloadedFile:
    """A non-JSON response body, currently only the audit export."""

    content: bytes
    content_type: str
    file_name: str | None


def _file_name(disposition: str) -> str | None:
    """Extracts the file name from a Content-Disposition header."""
    for chunk in disposition.split(";"):
        key, _, value = chunk.strip().partition("=")
        if key.lower() in {"filename", "filename*"}:
            return value.strip().strip('"')
    return None
