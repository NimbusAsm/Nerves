"""Cursor pagination primitives."""

from __future__ import annotations

from dataclasses import dataclass, replace
from typing import Generic, List, Tuple, TypeVar

T = TypeVar("T")


@dataclass(frozen=True, slots=True)
class Page(Generic[T]):
    """One cursor page. The cursor is opaque; never derive offsets from it."""

    items: Tuple[T, ...]
    next_cursor: str | None

    @property
    def has_more(self) -> bool:
        return bool(self.next_cursor)


@dataclass(frozen=True, slots=True)
class ListQuery:
    """Immutable filters, sort, page size and cursor for a list request."""

    filters: Tuple[Tuple[str, str], ...] = ()
    sort: str | None = None
    limit: int | None = None
    cursor: str | None = None

    def filter(self, field: str, value: str) -> "ListQuery":
        if not field:
            raise ValueError("filter field must not be empty")
        return replace(self, filters=(*self.filters, (field, value)))

    def with_sort(self, expression: str) -> "ListQuery":
        return replace(self, sort=expression)

    def with_limit(self, page_size: int) -> "ListQuery":
        if page_size <= 0:
            raise ValueError("page size must be greater than zero")
        return replace(self, limit=page_size)

    def with_cursor(self, cursor: str) -> "ListQuery":
        if not cursor:
            raise ValueError("cursor must not be empty")
        return replace(self, cursor=cursor)

    def params(self) -> List[Tuple[str, str]]:
        params: List[Tuple[str, str]] = [(f"filter[{field}]", value) for field, value in self.filters]
        if self.sort is not None:
            params.append(("sort", self.sort))
        if self.limit is not None:
            params.append(("limit", str(self.limit)))
        if self.cursor is not None:
            params.append(("cursor", self.cursor))
        return params