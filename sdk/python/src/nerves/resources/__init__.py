"""Resource entry points."""

from .accounts import AccountsResource
from .audit import AuditResource
from .compliance import ComplianceResource
from .decisions import DecisionsResource
from .events import EventsResource
from .groups import GroupsResource
from .legal_entities import LegalEntitiesResource
from .notifications import NotificationsResource
from .permissions import PermissionsResource
from .roles import RolesResource
from .sessions import SessionsResource
from .subjects import SubjectsResource

__all__ = [
    "AccountsResource",
    "AuditResource",
    "ComplianceResource",
    "DecisionsResource",
    "EventsResource",
    "GroupsResource",
    "LegalEntitiesResource",
    "NotificationsResource",
    "PermissionsResource",
    "RolesResource",
    "SessionsResource",
    "SubjectsResource",
]
