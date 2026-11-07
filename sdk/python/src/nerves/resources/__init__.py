"""Resource entry points."""

from .accounts import AccountsResource
from .legal_entities import LegalEntitiesResource
from .roles import RolesResource

__all__ = ["AccountsResource", "LegalEntitiesResource", "RolesResource"]
