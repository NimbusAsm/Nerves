using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nerves.Identity.Domain;
using Nerves.Identity.Persistence;
using Nerves.Identity.Security;

namespace Nerves.Identity.Seeding;

/// <summary>Creates the development administrator so a fresh database is usable.</summary>
/// <remarks>
/// With sharding enabled the admin account lands on the shard derived from its
/// id while the entity and routing entries stay in the global database.
/// </remarks>
public sealed class DevSeeder(
    IServiceProvider services,
    IOptions<DevSeedOptions> options,
    Microsoft.Extensions.Hosting.IHostEnvironment environment) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var seed = options.Value;
        if (!seed.Enabled)
        {
            return;
        }

        Nerves.ServiceDefaults.ProductionGuards.RejectSeedOutsideDevelopment(
            environment, seed.Enabled, seed.AllowInProduction, "DevSeed");

        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var identifiers = scope.ServiceProvider.GetRequiredService<IIdentifierHasher>();

        if (await store.FindByUsernameAsync(seed.Realm, seed.Username, cancellationToken) is not null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var realName = RealNameId.Create(
            IdDocumentType.Other,
            identifiers.Hash("realname", seed.Realm, seed.Username),
            "CN").Value;

        var entity = LegalEntity.Create(LegalEntityType.NaturalPerson, seed.DisplayName, realName, now).Value;
        entity.MarkVerified("dev-seed", now);

        var account = NetworkAccount.Create(
            entity.Id, seed.Realm, seed.Username, seed.Email, null, seed.DisplayName, AccountType.Personal, now).Value;
        account.MarkPrimary();

        var credential = AccountCredential.Create(
            account.Id, CredentialType.Password, hasher.Hash(seed.Password), Pbkdf2PasswordHasher.Algorithm, now).Value;

        await using (var global = store.Global())
        {
            global.LegalEntities.Add(entity);
            await global.SaveChangesAsync(cancellationToken);
        }

        var indexes = new List<RoutingIndexEntry>();
        foreach (var (type, value) in new[]
                 {
                     ("username", seed.Username),
                     ("email", seed.Email),
                 })
        {
            indexes.Add(RoutingIndexEntry.Create(
                identifiers.Hash(type, seed.Realm, value),
                type,
                seed.Realm,
                account.Id,
                entity.Id,
                store.ShardFor(account.Id),
                now));
        }

        var created = await store.CreateAccountAsync(account, credential, indexes, cancellationToken);
        if (created.IsFailure)
        {
            throw new InvalidOperationException("development seed failed: " + created.Reason.Message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
