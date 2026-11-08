namespace Nerves.ComplianceCn.Domain;

/// <summary>
/// How long one category of personal data is kept. The service stores the
/// policy; enforcement runs in the owning service.
/// </summary>
public sealed class RetentionPolicy
{
    private RetentionPolicy() => Category = string.Empty;

    private RetentionPolicy(Guid id, string category, int retentionDays, string? legalBasis)
    {
        Id = id;
        Category = category;
        RetentionDays = retentionDays;
        LegalBasis = legalBasis;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public string Category { get; private set; }

    public int RetentionDays { get; private set; }

    public string? LegalBasis { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static RetentionPolicy Create(string category, int retentionDays, string? legalBasis)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        return new RetentionPolicy(Guid.CreateVersion7(), category, retentionDays, legalBasis);
    }

    public void Update(int retentionDays, string? legalBasis)
    {
        RetentionDays = retentionDays;
        LegalBasis = legalBasis;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
