"""Response models and enumerations shared by every resource."""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime
from enum import Enum
from typing import Any, Mapping, Tuple


class AccountType(str, Enum):
    """What the network account is used for."""

    PERSONAL = "personal"
    SERVICE = "service"
    MANAGED = "managed"


class AccountStatus(str, Enum):
    """Account lifecycle."""

    ACTIVE = "active"
    LOCKED = "locked"
    DISABLED = "disabled"


class LegalEntityType(str, Enum):
    """Kind of legal entity."""

    NATURAL_PERSON = "naturalPerson"
    LEGAL_PERSON = "legalPerson"
    GOVERNMENT = "government"
    OTHER = "other"


class IdDocumentType(str, Enum):
    """Kind of identity document."""

    RESIDENT_IDENTITY_CARD = "residentIdentityCard"
    PASSPORT = "passport"
    UNIFIED_SOCIAL_CREDIT_CODE = "unifiedSocialCreditCode"
    OTHER = "other"


class VerificationState(str, Enum):
    """Real-name verification lifecycle."""

    PENDING = "pending"
    VERIFIED = "verified"
    REJECTED = "rejected"
    EXPIRED = "expired"


def wire(value: object) -> str:
    """Converts an enum member (or plain string) to its wire value."""
    if isinstance(value, Enum):
        return str(value.value)
    return str(value)


def _datetime(value: Any) -> datetime | None:
    if not isinstance(value, str) or not value:
        return None
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def _required_datetime(value: Any) -> datetime:
    parsed = _datetime(value)
    if parsed is None:
        raise ValueError(f"expected an ISO-8601 timestamp, got {value!r}")
    return parsed


def _strings(value: Any) -> Tuple[str, ...]:
    return tuple(str(item) for item in value) if isinstance(value, (list, tuple)) else ()


def _optional(value: Any) -> str | None:
    return None if value is None else str(value)


@dataclass(frozen=True, slots=True)
class Account:
    """A login identity returned by /api/v1/accounts."""

    id: str
    legal_entity_id: str | None
    realm: str
    username: str
    email: str | None
    phone_number: str | None
    display_name: str | None
    type: str
    status: str
    is_primary: bool
    roles: Tuple[str, ...]
    created_at: datetime
    updated_at: datetime
    disabled_at: datetime | None

    @classmethod
    def from_mapping(cls, data: Mapping[str, Any]) -> "Account":
        return cls(
            id=str(data["id"]),
            legal_entity_id=_optional(data.get("legalEntityId")),
            realm=str(data.get("realm", "")),
            username=str(data.get("username", "")),
            email=_optional(data.get("email")),
            phone_number=_optional(data.get("phoneNumber")),
            display_name=_optional(data.get("displayName")),
            type=str(data.get("type", "personal")),
            status=str(data.get("status", "active")),
            is_primary=bool(data.get("isPrimary", False)),
            roles=_strings(data.get("roles")),
            created_at=_required_datetime(data.get("createdAt")),
            updated_at=_required_datetime(data.get("updatedAt")),
            disabled_at=_datetime(data.get("disabledAt")),
        )


@dataclass(frozen=True, slots=True)
class RealName:
    """The non-secret part of a legal entity's real-name identity."""

    document_type: str
    country: str

    @classmethod
    def from_mapping(cls, data: Mapping[str, Any]) -> "RealName":
        return cls(document_type=str(data.get("documentType", "")), country=str(data.get("country", "")))


@dataclass(frozen=True, slots=True)
class LegalEntity:
    """A verified subject returned by /api/v1/legal-entities."""

    id: str
    type: str
    display_name: str
    real_name: RealName | None
    verification_state: str
    verified_at: datetime | None
    provider_reference: str | None
    created_at: datetime
    updated_at: datetime

    @classmethod
    def from_mapping(cls, data: Mapping[str, Any]) -> "LegalEntity":
        raw_real_name = data.get("realName")
        return cls(
            id=str(data["id"]),
            type=str(data.get("type", "naturalPerson")),
            display_name=str(data.get("displayName", "")),
            real_name=RealName.from_mapping(raw_real_name) if isinstance(raw_real_name, Mapping) else None,
            verification_state=str(data.get("verificationState", "pending")),
            verified_at=_datetime(data.get("verifiedAt")),
            provider_reference=_optional(data.get("providerReference")),
            created_at=_required_datetime(data.get("createdAt")),
            updated_at=_required_datetime(data.get("updatedAt")),
        )


@dataclass(frozen=True, slots=True)
class Role:
    """A role returned by /api/v1/roles."""

    id: str
    name: str
    description: str | None
    permissions: Tuple[str, ...]
    created_at: datetime
    updated_at: datetime

    @classmethod
    def from_mapping(cls, data: Mapping[str, Any]) -> "Role":
        return cls(
            id=str(data["id"]),
            name=str(data.get("name", "")),
            description=_optional(data.get("description")),
            permissions=_strings(data.get("permissions")),
            created_at=_required_datetime(data.get("createdAt")),
            updated_at=_required_datetime(data.get("updatedAt")),
        )
