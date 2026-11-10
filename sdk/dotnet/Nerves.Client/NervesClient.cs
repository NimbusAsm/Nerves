using Nerves.Client.Resources;

namespace Nerves.Client;

/// <summary>
/// Entry point of the chainable client. Every operation starts here and is
/// expressed as <c>entry → verb → modifier → terminal</c>:
/// <code>
/// await nerves.Accounts.Create().ForEntity(id).WithUsername("alice").SendAsync(ct);
/// </code>
/// </summary>
public sealed class NervesClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private bool _disposed;

    /// <summary>Creates a client from explicit options.</summary>
    /// <exception cref="ArgumentException">The base URL is missing or not absolute.</exception>
    public NervesClient(NervesClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.BaseUrl);

        if (!options.BaseUrl.IsAbsoluteUri)
        {
            throw new ArgumentException("baseUrl must be an absolute URI.", nameof(options));
        }

        if (options.MaxRetries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxRetries cannot be negative.");
        }

        _httpClient = options.HttpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _ownsHttpClient = options.HttpClient is null;

        var api = new ApiRequester(_httpClient, options);
        Accounts = new AccountsResource(api);
        LegalEntities = new LegalEntitiesResource(api);
        Roles = new RolesResource(api);
        Permissions = new PermissionsResource(api);
        Groups = new GroupsResource(api);
        Decisions = new DecisionsResource(api);
        Sessions = new SessionsResource(api);
        Audit = new AuditResource(api);
        Compliance = new ComplianceResource(api);
        Subjects = new SubjectsResource(api);
        Notifications = new NotificationsResource(api);
        Events = new EventsResource(api);
    }

    /// <summary>Convenience overload for the common case.</summary>
    /// <param name="baseUrl">Origin of the deployment, e.g. <c>https://accounts.example.com</c>.</param>
    /// <param name="token">Personal access token, or <see langword="null"/> for anonymous calls.</param>
    /// <param name="httpClient">Optional caller-owned HTTP client.</param>
    public NervesClient(string baseUrl, string? token = null, HttpClient? httpClient = null)
        : this(new NervesClientOptions
        {
            BaseUrl = new Uri(baseUrl ?? throw new ArgumentNullException(nameof(baseUrl)), UriKind.Absolute),
            Token = token,
            HttpClient = httpClient,
        })
    {
    }

    /// <summary>Network accounts: <c>/api/v1/accounts</c>.</summary>
    public AccountsResource Accounts { get; }

    /// <summary>Legal entities: <c>/api/v1/legal-entities</c>.</summary>
    public LegalEntitiesResource LegalEntities { get; }

    /// <summary>Roles: <c>/api/v1/roles</c>.</summary>
    public RolesResource Roles { get; }

    /// <summary>Permission codes: <c>/api/v1/permissions</c>.</summary>
    public PermissionsResource Permissions { get; }

    /// <summary>Groups and members: <c>/api/v1/groups</c>.</summary>
    public GroupsResource Groups { get; }

    /// <summary>Authorization decisions: <c>/api/v1/decisions</c>.</summary>
    public DecisionsResource Decisions { get; }

    /// <summary>Login sessions: <c>/api/v1/accounts/{{accountId}}/sessions</c>.</summary>
    public SessionsResource Sessions { get; }

    /// <summary>Audit trail: <c>/api/v1/audit-events</c>.</summary>
    public AuditResource Audit { get; }

    /// <summary>Consents and retention: <c>/api/v1/compliance</c>.</summary>
    public ComplianceResource Compliance { get; }

    /// <summary>Data-subject rights: <c>/api/v1/compliance/subjects</c>.</summary>
    public SubjectsResource Subjects { get; }

    /// <summary>Notification log: <c>/api/v1/notifications</c>.</summary>
    public NotificationsResource Notifications { get; }

    /// <summary>Identity events: <c>/api/v1/events</c>.</summary>
    public EventsResource Events { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
