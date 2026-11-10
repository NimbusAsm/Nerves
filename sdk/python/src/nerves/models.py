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
# ---- Operations models (sessions, authorization, audit, compliance, notification, events) ----


@dataclass(frozen=True, slots=True)
class SessionInfo:
    """One login session returned by /api/v1/accounts/{id}/sessions."""

    id: str
    device_id: str
    created_at: datetime
    last_seen_at: datetime
    absolute_expires_at: datetime
    revoked: bool
    revoked_reason: str | None

    @classmethod
    def from_mapping(cls, data: Mapping[str, Any]) -> "SessionInfo":
        return cls(
            id=str(data["id"]),
            device_id=str(data.get("deviceId", "")),
            created_at=_required_datetime(data.get("createdAt")),
            last_seen_at=_required_datetime(data.get("lastSeenAt")),
            absolute_expires_at=_required_datetime(data.get("absoluteExpiresAt")),
            revoked=bool(data.get("revoked", False)),
            revoked_reason=_optional(data.get("revokedReason")),
        )


@dataclass(frozen=True, slots=True)
class PermissionInfo:
    """One permission code returned by /api/v1/permissions."""

    id: str
    code: str
    description: str | None

    @classmethod
    def from_mapping(cls, data: Mapping[str, Any]) -> "PermissionInfo":
        return cls(
            id=str(data["id"]),
            code=str(data.get("code", "")),
            description=_optional(data.get("description")),
        )


@dataclass(frozen=True, slots=True)
class Group:
    """A group of accounts returned by /api/v1/groups."""

    id: str
    realm: str
    code: str
    name: str

    @classmethod
    def from_mapping(cls, data: Mapping[str, Any]) -> "Group":
        return cls(
            id=str(data["id"]),
            realm=str(data.get("realm", "")),
            code=str(data.get("code", "")),
            name=str(data.get("name", "")),
        )


@dataclass(frozen=True, slots=True)
class GroupDetail:
    """A group plus its member count."""

    id: str
    realm: str
    code: str
    name: str
    member_count: int

    @classmethod
    def from_mapping(cls, data: Mapping[str, Any]) -> "GroupDetail":
        return cls(
            id=str(data["id"]),
            realm=str(data.get("realm", "")),
            code=str(data.get("code", "")),
            name=str(data.get("name", "")),
            member_count=int(data.get("memberCount", 0)),
        )


@dataclass(frozen=True, slots=True)
class GroupMember:
    """One group membership."""

    account_id: str
    added_at: datetime

    @classmethod
    def from_mapping(cls, data: Mapping[str, Any]) -> "GroupMember":
        return cls(
            account_id=str(data.get("accountId", "")),
            added_at=_required_datetime(data.get("addedAt")),
        )


@dataclass(frozen=True, slots=True)
class DecisionResult:
    """Result of one authorization decision."""

    allowed: bool
    reason: str | None
    source: str
    authorization_version: int
    permission: str

    @classmethod
    def from_mapping(cls, data: Mapping[str, Any]) -> "DecisionResult":
        return cls(
            allowed=bool(data.get("allowed", False)),
            reason=_optional(data.get("reason")),
            source=str(data.get("source", "")),
            authorization_version=int(data.get("authorizationVersion", 0)),
            permission=str(data.get("permission", "")),
        )


@dataclass(frozen=True, slots=True)
class DecisionStats:
    """Permission-cache statistics."""

    entries: int
    hits: int
    misses: int
    hit_rate: float

    @classmethod
    def from_mapping(cls, data: Mapping[str, Any]) -> "DecisionStats":
        return cls(
            entries=int(data.get("entries", 0)),
            hits=int(data.get("hits", 0)),
            misses=int(data.get("misses", 0)),
            hit_rate=float(data.get("hitRate", 0.0)),
        )


@dataclass(frozen=True, slots=True)
class AuditEvent:
    """One append-only audit record."""

    id: str
    stream: str
    sequence: int
    occurred_at: datetime
    actor_type: str
    actor_id: str | None
    action: str
    target_type: str | None
    target_id: str | None
    result: str
    trace_id: str | None
    hash: str

    @classmethod
    def from_mapping(cls, data: Mapping[str, Any]) -> "AuditEvent":
        return cls(
            id=str(data["id"]),
            stream=str(data.get("stream", "")),
            sequence=int(data.get("sequence", 0)),
            occurred_at=_required_datetime(data.get("occurredAt")),
            actor_type=str(data.get("actorType", "")),
            actor_id=_optional(data.get("actorId")),
            action=str(data.get("action", "")),
            target_type=_optional(data.get("targetType")),
            target_id=_optional(data.get("targetId")),
            result=str(data.get("result", "")),
            trace_id=_optional(data.get("traceId")),
            hash=str(data.get("hash", "")),
        )


@dataclass(frozen=True, slots=True)
class AuditVerifyResult:
    """Result of verifying one audit stream."""

    valid: bool
    broken_at: str | None
    reason: str | None
    count: int

    @classmethod
    def from_mapping(cls, data: Mapping[str, Any]) -> "AuditVerifyResult":
        return cls(
            valid=bool(data.get("valid", False)),
            broken_at=_optional(data.get("brokenAt")),
            reason=_optional(data.get("reason")),
            count=int(data.get("count", 0)),
        )


@dataclass(frozen=True, slots=True)
class ConsentRecord:
    """One consent record."""

    id: str
    subject_id: str
    purpose: str
    version: str
    granted_at: datetime
    expires_at: datetime | None
    revoked_at: datetime | None
    revocation_reason: str | None

    @classmethod
    def from_mapping(cls, data: Mapping[str, Any]) -> "ConsentRecord":
        return cls(
            id=str(data["id"]),
            subject_id=str(data.get("subjectId", "")),
            purpose=str(data.get("purpose", "")),
            version=str(data.get("version", "")),
            granted_at=_required_datetime(data.get("grantedAt")),
            expires_at=_datetime(data.get("expiresAt")),
            revoked_at=_datetime(data.get("revokedAt")),
            revocation_reason=_optional(data.get("revocationReason")),
        )


@dataclass(frozen=True, slots=True)
class RetentionPolicy:
    """A retention policy for one data category."""

    id: str
    category: str
    retention_days: int
    legal_basis: str | None
    updated_at: datetime

    @classmethod
    def from_mapping(cls, data: Mapping[str, Any]) -> "RetentionPolicy":
        return cls(
            id=str(data["id"]),
            category=str(data.get("category", "")),
            retention_days=int(data.get("retentionDays", 0)),
            legal_basis=_optional(data.get("legalBasis")),
            updated_at=_required_datetime(data.get("updatedAt")),
        )


@dataclass(frozen=True, slots=True)
class ComplianceSummary:
    """Aggregate counts for the compliance module."""

    consents: int
    active: int
    revoked: int
    retention_policies: int

    @classmethod
    def from_mapping(cls, data: Mapping[str, Any]) -> "ComplianceSummary":
        return cls(
            consents=int(data.get("consents", 0)),
            active=int(data.get("active", 0)),
            revoked=int(data.get("revoked", 0)),
            retention_policies=int(data.get("retentionPolicies", 0)),
        )


@dataclass(frozen=True, slots=True)
class EraseResult:
    """Result of an erasure request."""

    request_id: str
    subject_id: str
    status: str
    consent_revoked: int
    account_disabled: bool

    @classmethod
    def from_mapping(cls, data: Mapping[str, Any]) -> "EraseResult":
        return cls(
            request_id=str(data.get("id", "")),
            subject_id=str(data.get("subjectId", "")),
            status=str(data.get("status", "")),
            consent_revoked=int(data.get("consentRevoked", 0)),
            account_disabled=bool(data.get("accountDisabled", False)),
        )


@dataclass(frozen=True, slots=True)
class ErasureRecord:
    """One recorded erasure request."""

    id: str
    status: str
    reason: str
    requested_at: datetime
    completed_at: datetime | None

    @classmethod
    def from_mapping(cls, data: Mapping[str, Any]) -> "ErasureRecord":
        return cls(
            id=str(data["id"]),
            status=str(data.get("status", "")),
            reason=str(data.get("reason", "")),
            requested_at=_required_datetime(data.get("requestedAt")),
            completed_at=_datetime(data.get("completedAt")),
        )


@dataclass(frozen=True, slots=True)
class NotificationMessage:
    """One queued or delivered notification."""

    id: str
    to: str
    template: str
    subject: str
    status: str
    attempts: int
    error: str | None
    created_at: datetime
    sent_at: datetime | None
    next_attempt_at: datetime | None

    @classmethod
    def from_mapping(cls, data: Mapping[str, Any]) -> "NotificationMessage":
        return cls(
            id=str(data["id"]),
            to=str(data.get("to", "")),
            template=str(data.get("template", "")),
            subject=str(data.get("subject", "")),
            status=str(data.get("status", "")),
            attempts=int(data.get("attempts", 0)),
            error=_optional(data.get("error")),
            created_at=_required_datetime(data.get("createdAt")),
            sent_at=_datetime(data.get("sentAt")),
            next_attempt_at=_datetime(data.get("nextAttemptAt")),
        )


@dataclass(frozen=True, slots=True)
class IdentityEvent:
    """One identity domain event."""

    id: str
    type: str
    account_id: str | None
    legal_entity_id: str | None
    payload: str
    occurred_at: datetime
    dispatched_at: datetime | None

    @classmethod
    def from_mapping(cls, data: Mapping[str, Any]) -> "IdentityEvent":
        return cls(
            id=str(data["id"]),
            type=str(data.get("type", "")),
            account_id=_optional(data.get("accountId")),
            legal_entity_id=_optional(data.get("legalEntityId")),
            payload=str(data.get("payload", "")),
            occurred_at=_required_datetime(data.get("occurredAt")),
            dispatched_at=_datetime(data.get("dispatchedAt")),
        )
