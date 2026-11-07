namespace Nerves.Identity.Domain;

/// <summary>Kind of legal entity behind an account.</summary>
public enum LegalEntityType
{
    NaturalPerson = 1,
    LegalPerson = 2,
    Government = 3,
    Other = 4,
}

/// <summary>Kind of document used to identify a legal entity.</summary>
public enum IdDocumentType
{
    ResidentIdentityCard = 1,
    Passport = 2,
    UnifiedSocialCreditCode = 3,
    Other = 4,
}

/// <summary>Real-name verification lifecycle.</summary>
public enum VerificationState
{
    Pending = 1,
    Verified = 2,
    Rejected = 3,
    Expired = 4,
}

/// <summary>What the network account is used for.</summary>
public enum AccountType
{
    Personal = 1,
    Service = 2,
    Managed = 3,
}

/// <summary>Account lifecycle.</summary>
public enum AccountStatus
{
    Active = 1,
    Locked = 2,
    Disabled = 3,
}
