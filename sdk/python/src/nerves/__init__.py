"""Nerves Python SDK.

    import nerves

    with nerves.client("https://accounts.example.com", token="pat") as c:
        account = (c.accounts.create()
            .for_entity(entity_id)
            .with_username("alice")
            .send())
"""

from .client import NervesClient, client
from .errors import NervesApiException, ProblemDetails
from .models import (
    Account,
    AccountStatus,
    AccountType,
    AuditEvent,
    AuditVerifyResult,
    ComplianceSummary,
    ConsentRecord,
    DecisionResult,
    DecisionStats,
    EraseResult,
    ErasureRecord,
    Group,
    GroupDetail,
    GroupMember,
    IdDocumentType,
    IdentityEvent,
    LegalEntity,
    LegalEntityType,
    NotificationMessage,
    PermissionInfo,
    RealName,
    RetentionPolicy,
    Role,
    SessionInfo,
    VerificationState,
)
from .transport import DownloadedFile
from .pagination import ListQuery, Page

__version__ = "0.1.0"

__all__ = [
    "Account",
    "AccountStatus",
    "AccountType",
    "AuditEvent",
    "AuditVerifyResult",
    "ComplianceSummary",
    "ConsentRecord",
    "DecisionResult",
    "DecisionStats",
    "DownloadedFile",
    "EraseResult",
    "ErasureRecord",
    "Group",
    "GroupDetail",
    "GroupMember",
    "IdDocumentType",
    "IdentityEvent",
    "LegalEntity",
    "LegalEntityType",
    "ListQuery",
    "NervesApiException",
    "NervesClient",
    "NotificationMessage",
    "Page",
    "PermissionInfo",
    "ProblemDetails",
    "RealName",
    "RetentionPolicy",
    "Role",
    "SessionInfo",
    "VerificationState",
    "client",
]