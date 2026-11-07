namespace Nerves.Client.Resources;

/// <summary>Entry point for every legal-entity operation.</summary>
public sealed class LegalEntitiesResource
{
    private readonly ApiRequester _api;

    internal LegalEntitiesResource(ApiRequester api) => _api = api;

    /// <summary>Starts a create request.</summary>
    public LegalEntityCreateBuilder Create() => new(_api);

    /// <summary>Starts a read request for one legal entity.</summary>
    public LegalEntityGetBuilder Get(Guid id) => new(_api, id);

    /// <summary>Starts a list request.</summary>
    public LegalEntityListBuilder List() => new(_api);

    /// <summary>Starts a patch request for one legal entity.</summary>
    public LegalEntityUpdateBuilder Update(Guid id) => new(_api, id);

    /// <summary>Starts a delete request for one legal entity.</summary>
    public LegalEntityDeleteBuilder Delete(Guid id) => new(_api, id);
}
