"""Chainable builders for /api/v1/compliance."""

from __future__ import annotations

from dataclasses import dataclass, field, replace
from typing import List
from uuid import uuid4

from ..models import (
    ComplianceSummary,
    ConsentRecord,
    RetentionPolicy,
)
from ..transport import Transport


@dataclass(frozen=True, slots=True)
class ListConsentsBuilder:
    """Builds GET /api/v1/compliance/consents; the endpoint returns a plain array."""

    _transport: Transport = field(repr=False, compare=False)
    _subject_id: str | None = None
    _purpose: str | None = None

    def for_subject(self, subject_id: str) -> "ListConsentsBuilder":
        """Restricts the result to one data subject."""
        return replace(self, _subject_id=subject_id)

    def with_purpose(self, purpose: str) -> "ListConsentsBuilder":
        """Restricts the result to one purpose."""
        return replace(self, _purpose=purpose)

    def send(self) -> List[ConsentRecord]:
        """Sends the request."""
        params = []
        if self._subject_id is not None:
            params.append(("subjectId", self._subject_id))
        if self._purpose is not None:
            params.append(("purpose", self._purpose))

        payload = self._transport.request("GET", "compliance/consents", params=params or None)
        return [ConsentRecord.from_mapping(item) for item in payload]


@dataclass(frozen=True, slots=True)
class GrantConsentBuilder:
    """Builds POST /api/v1/compliance/consents."""

    _transport: Transport = field(repr=False, compare=False)
    subject_id: str | None = None
    purpose: str | None = None
    version: str | None = None
    expires_at: object | None = None
    idempotency_key: str | None = None

    def for_subject(self, subject_id: str) -> "GrantConsentBuilder":
        """Sets the data subject."""
        return replace(self, subject_id=subject_id)

    def with_purpose(self, purpose: str) -> "GrantConsentBuilder":
        """Sets the processing purpose."""
        return replace(self, purpose=purpose)

    def with_version(self, version: str) -> "GrantConsentBuilder":
        """Sets the accepted consent-text version."""
        return replace(self, version=version)

    def until(self, expires_at: object) -> "GrantConsentBuilder":
        """Sets the expiry timestamp."""
        return replace(self, expires_at=expires_at)

    def with_idempotency_key(self, idempotency_key: str) -> "GrantConsentBuilder":
        """Overrides the generated idempotency key."""
        return replace(self, idempotency_key=idempotency_key)

    def send(self) -> ConsentRecord:
        """Sends the request."""
        if not self.subject_id or not self.subject_id.strip():
            raise ValueError("for_subject() is required before send().")
        if not self.purpose or not self.purpose.strip():
            raise ValueError("with_purpose() is required before send().")
        if not self.version or not self.version.strip():
            raise ValueError("with_version() is required before send().")

        payload = self._transport.request(
            "POST",
            "compliance/consents",
            body={
                "subjectId": self.subject_id,
                "purpose": self.purpose,
                "version": self.version,
                "expiresAt": None if self.expires_at is None else str(self.expires_at),
            },
            idempotency_key=self.idempotency_key or str(uuid4()),
        )
        return ConsentRecord.from_mapping(payload)


@dataclass(frozen=True, slots=True)
class RevokeConsentBuilder:
    """Builds POST /api/v1/compliance/consents/{id}/revoke."""

    _transport: Transport = field(repr=False, compare=False)
    consent_id: str
    _reason: str | None = None

    def with_reason(self, reason: str | None) -> "RevokeConsentBuilder":
        """Sets the revocation reason."""
        return replace(self, _reason=reason)

    def send(self) -> ConsentRecord:
        """Sends the request."""
        payload = self._transport.request(
            "POST",
            f"compliance/consents/{self.consent_id}/revoke",
            body={"reason": self._reason},
        )
        return ConsentRecord.from_mapping(payload)


@dataclass(frozen=True, slots=True)
class ListRetentionPoliciesBuilder:
    """Builds GET /api/v1/compliance/retention-policies."""

    _transport: Transport = field(repr=False, compare=False)

    def send(self) -> List[RetentionPolicy]:
        """Sends the request."""
        payload = self._transport.request("GET", "compliance/retention-policies")
        return [RetentionPolicy.from_mapping(item) for item in payload]


@dataclass(frozen=True, slots=True)
class UpsertRetentionPolicyBuilder:
    """Builds PUT /api/v1/compliance/retention-policies/{category}."""

    _transport: Transport = field(repr=False, compare=False)
    category: str
    _retention_days: int | None = None
    _legal_basis: str | None = None
    idempotency_key: str | None = None

    def with_retention(self, days: int) -> "UpsertRetentionPolicyBuilder":
        """Sets the retention period in days."""
        return replace(self, _retention_days=days)

    def with_legal_basis(self, legal_basis: str | None) -> "UpsertRetentionPolicyBuilder":
        """Sets the recorded legal basis."""
        return replace(self, _legal_basis=legal_basis)

    def with_idempotency_key(self, idempotency_key: str) -> "UpsertRetentionPolicyBuilder":
        """Overrides the generated idempotency key."""
        return replace(self, idempotency_key=idempotency_key)

    def send(self) -> RetentionPolicy:
        """Sends the request."""
        if self._retention_days is None:
            raise ValueError("with_retention() is required before send().")

        payload = self._transport.request(
            "PUT",
            f"compliance/retention-policies/{self.category}",
            body={"retentionDays": self._retention_days, "legalBasis": self._legal_basis},
            idempotency_key=self.idempotency_key or str(uuid4()),
        )
        return RetentionPolicy.from_mapping(payload)


@dataclass(frozen=True, slots=True)
class GetComplianceSummaryBuilder:
    """Builds GET /api/v1/compliance/summary."""

    _transport: Transport = field(repr=False, compare=False)

    def send(self) -> ComplianceSummary:
        """Sends the request."""
        payload = self._transport.request("GET", "compliance/summary")
        return ComplianceSummary.from_mapping(payload)


class ComplianceResource:
    """Entry point for compliance records."""

    def __init__(self, transport: Transport) -> None:
        self._transport = transport

    def consents(self) -> ListConsentsBuilder:
        """Starts a consent list request."""
        return ListConsentsBuilder(self._transport)

    def grant_consent(self) -> GrantConsentBuilder:
        """Starts a consent grant request."""
        return GrantConsentBuilder(self._transport)

    def revoke_consent(self, consent_id: str) -> RevokeConsentBuilder:
        """Starts a consent revocation request."""
        return RevokeConsentBuilder(self._transport, str(consent_id))

    def retention_policies(self) -> ListRetentionPoliciesBuilder:
        """Starts a retention-policy list request."""
        return ListRetentionPoliciesBuilder(self._transport)

    def upsert_retention_policy(self, category: str) -> UpsertRetentionPolicyBuilder:
        """Starts a retention-policy upsert for one category."""
        return UpsertRetentionPolicyBuilder(self._transport, category)

    def summary(self) -> GetComplianceSummaryBuilder:
        """Starts a summary request."""
        return GetComplianceSummaryBuilder(self._transport)
