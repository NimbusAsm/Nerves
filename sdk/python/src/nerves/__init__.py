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
    IdDocumentType,
    LegalEntity,
    LegalEntityType,
    RealName,
    Role,
    VerificationState,
)
from .pagination import ListQuery, Page

__version__ = "0.1.0"

__all__ = [
    "Account",
    "AccountStatus",
    "AccountType",
    "IdDocumentType",
    "LegalEntity",
    "LegalEntityType",
    "ListQuery",
    "NervesApiException",
    "NervesClient",
    "Page",
    "ProblemDetails",
    "RealName",
    "Role",
    "VerificationState",
    "client",
]
