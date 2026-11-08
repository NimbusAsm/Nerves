using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nerves.Identity.Domain;
using Nerves.Identity.Persistence;
using Nerves.Identity.Security;
using Nerves.Identity.Sharding;

namespace Nerves.Identity.Seeding;

/// <summary>Creates the development administrator so a fresh database is usable.</summary>
public sealed class DevSeeder(
    IServiceProvider services,
    IOptions<DevSeedOptions> options,
    ShardResolver shards) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var seed = options.Value;
        if (!seed.Enabled)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var identifiers = scope.ServiceProvider.GetRequiredService<IIdentifierHasher>();

        if (await db.NetworkAccounts.AnyAsync(x => x.Realm == seed.Realm && x.Username == seed.Username, cancellationToken))
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

        db.LegalEntities.Add(entity);
        db.NetworkAccounts.Add(account);
        db.AccountCredentials.Add(credential);

        foreach (var (type, value) in new[]
                 {
                     ("username", seed.Username),
                     ("email", seed.Email),
                 })
        {
            db.RoutingIndex.Add(RoutingIndexEntry.Create(
                identifiers.Hash(type, seed.Realm, value),
                type,
                seed.Realm,
                account.Id,
                entity.Id,
                shards.Resolve(account.Id),
                now));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
