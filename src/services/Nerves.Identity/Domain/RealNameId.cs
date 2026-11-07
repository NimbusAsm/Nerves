using Nerves.Core;

namespace Nerves.Identity.Domain;

/// <summary>
/// The verified identity of a legal entity, stored only as a salted hash.
/// </summary>
/// <remarks>
/// Plaintext document numbers are never persisted: <see cref="Hash"/> is an
/// HMAC computed by the caller with a server-side key, so a database leak does
/// not expose identity documents (PLAN §3.2, I1).
/// </remarks>
public sealed record RealNameId
{
    private RealNameId(IdDocumentType documentType, string hash, string country)
    {
        DocumentType = documentType;
        Hash = hash;
        Country = country;
    }

    public IdDocumentType DocumentType { get; }

    public string Hash { get; }

    public string Country { get; }

    public static Result<RealNameId> Create(IdDocumentType documentType, string? hash, string? country)
    {
        if (!Enum.IsDefined(documentType))
        {
            return Result.Failure<RealNameId>(Reason.Validation("document type is invalid"));
        }

        if (string.IsNullOrWhiteSpace(hash) || hash.Length != RealNameHashLength)
        {
            return Result.Failure<RealNameId>(Reason.Validation(
                $"document hash must be a {RealNameHashLength}-character hex string"));
        }

        if (string.IsNullOrWhiteSpace(country) || country.Length != 2)
        {
            return Result.Failure<RealNameId>(Reason.Validation("country must be an ISO 3166-1 alpha-2 code"));
        }

        return Result.Success(new RealNameId(documentType, hash.ToLowerInvariant(), country.ToUpperInvariant()));
    }

    internal const int RealNameHashLength = 64;
}
