using Nerves.Identity.Domain;
using Shouldly;

namespace Nerves.Identity.UnitTests;

public sealed class NetworkAccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid EntityId = Guid.CreateVersion7();

    [Fact]
    public void Create_personal_account_anchored_to_entity()
    {
        var result = NetworkAccount.Create(
            EntityId, "Nimatattic", "Alice", "alice@example.com", null, "Alice", AccountType.Personal, Now);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Realm.ShouldBe("nimatattic");
        result.Value.Status.ShouldBe(AccountStatus.Active);
        result.Value.LegalEntityId.ShouldBe(EntityId);
    }

    [Fact]
    public void Service_account_must_not_have_entity()
    {
        NetworkAccount.Create(null, "core", "svc", null, null, null, AccountType.Service, Now)
            .IsSuccess.ShouldBeTrue();

        NetworkAccount.Create(EntityId, "core", "svc", null, null, null, AccountType.Service, Now)
            .IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Personal_account_must_have_entity()
    {
        var result = NetworkAccount.Create(null, "core", "alice", null, null, null, AccountType.Personal, Now);

        result.IsFailure.ShouldBeTrue();
        result.Reason.Code.ShouldBe("validation.failed");
    }

    [Theory]
    [InlineData("", "alice")]
    [InlineData("core", "")]
    public void Rejects_missing_realm_or_username(string realm, string username)
    {
        var result = NetworkAccount.Create(EntityId, realm, username, null, null, null, AccountType.Personal, Now);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Disable_and_lock_transitions_are_guarded()
    {
        var account = NetworkAccount.Create(EntityId, "core", "alice", null, null, null, AccountType.Personal, Now).Value;

        account.Lock(Now).IsSuccess.ShouldBeTrue();
        account.Status.ShouldBe(AccountStatus.Locked);
        account.Disable(Now).IsSuccess.ShouldBeTrue();
        account.Status.ShouldBe(AccountStatus.Disabled);
        account.Disable(Now).IsFailure.ShouldBeTrue();
        account.Unlock(Now).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Only_personal_accounts_can_be_primary()
    {
        var service = NetworkAccount.Create(null, "core", "svc", null, null, null, AccountType.Service, Now).Value;

        service.MarkPrimary().IsFailure.ShouldBeTrue();
    }
}
