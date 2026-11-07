namespace Nerves.Client.Resources;

/// <summary>Entry point for every account operation.</summary>
public sealed class AccountsResource
{
    private readonly ApiRequester _api;

    internal AccountsResource(ApiRequester api) => _api = api;

    /// <summary>Starts a create request.</summary>
    public AccountCreateBuilder Create() => new(_api);

    /// <summary>Starts a read request for one account.</summary>
    public AccountGetBuilder Get(Guid id) => new(_api, id);

    /// <summary>Starts a list request.</summary>
    public AccountListBuilder List() => new(_api);

    /// <summary>Starts a patch request for one account.</summary>
    public AccountUpdateBuilder Update(Guid id) => new(_api, id);

    /// <summary>Starts a delete request for one account.</summary>
    public AccountDeleteBuilder Delete(Guid id) => new(_api, id);
}
