namespace Nerves.Client.Resources;

/// <summary>Entry point for every role operation.</summary>
public sealed class RolesResource
{
    private readonly ApiRequester _api;

    internal RolesResource(ApiRequester api) => _api = api;

    /// <summary>Starts a create request.</summary>
    public RoleCreateBuilder Create() => new(_api);

    /// <summary>Starts a read request for one role.</summary>
    public RoleGetBuilder Get(Guid id) => new(_api, id);

    /// <summary>Starts a list request.</summary>
    public RoleListBuilder List() => new(_api);

    /// <summary>Starts a patch request for one role.</summary>
    public RoleUpdateBuilder Update(Guid id) => new(_api, id);

    /// <summary>Starts a delete request for one role.</summary>
    public RoleDeleteBuilder Delete(Guid id) => new(_api, id);

    /// <summary>Starts a request that grants the role to a subject.</summary>
    public RoleAssignBuilder Assign(Guid id) => new(_api, id);

    /// <summary>Starts a request that revokes the role from a subject.</summary>
    public RoleRevokeBuilder Revoke(Guid id) => new(_api, id);
}
