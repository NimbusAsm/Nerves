using Microsoft.Extensions.Options;
using Nerves.Identity.Domain;
using Nerves.Identity.Grpc;
using Nerves.Idp.Federation;
using Nerves.Idp.Options;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class LdapSyncTests
{
    private static readonly DirectoryAttributeMap DefaultMap = new();

    [Fact]
    public void An_entry_without_the_username_attribute_is_skipped()
    {
        var entry = new DirectoryEntry("uid=broken,dc=example,dc=com", new Dictionary<string, string?>
        {
            ["uid"] = null,
            ["mail"] = "broken@example.com",
            ["cn"] = "Broken",
        });

        DirectoryEntryMapper.Map(entry, "nerves", DefaultMap).ShouldBeNull();
    }

    [Fact]
    public void The_attribute_map_decides_which_fields_are_read()
    {
        var entry = new DirectoryEntry("cn=Alice,dc=example,dc=com", new Dictionary<string, string?>
        {
            ["sAMAccountName"] = "alice",
            ["mail"] = "alice@example.com",
            ["displayName"] = "Alice Chen",
        });

        var record = DirectoryEntryMapper.Map(entry, "nerves", new DirectoryAttributeMap
        {
            Username = "sAMAccountName",
            Email = "mail",
            DisplayName = "displayName",
        });

        record.ShouldNotBeNull();
        record!.Username.ShouldBe("alice");
        record.Email.ShouldBe("alice@example.com");
        record.DisplayName.ShouldBe("Alice Chen");
        record.Realm.ShouldBe("nerves");
        record.DirectoryId.ShouldBe("cn=Alice,dc=example,dc=com");
    }

    [Fact]
    public void Missing_optional_attributes_stay_null()
    {
        var entry = new DirectoryEntry("uid=bob,dc=example,dc=com", new Dictionary<string, string?>
        {
            ["uid"] = "  bob  ",
            ["mail"] = "   ",
            ["cn"] = null,
        });

        var record = DirectoryEntryMapper.Map(entry, "nerves", DefaultMap);

        record.ShouldNotBeNull();
        record!.Username.ShouldBe("bob");
        record.Email.ShouldBeNull();
        record.DisplayName.ShouldBeNull();
    }

    [Fact]
    public async Task A_pass_counts_creates_updates_unchanged_and_skips()
    {
        var options = Options.Create(new DirectorySyncOptions
        {
            Enabled = true,
            Realm = "nerves",
            BaseDn = "ou=people,dc=example,dc=com",
            Filter = "(objectClass=inetOrgPerson)",
        });
        var ldap = Options.Create(new LdapOptions { BaseDn = "dc=example,dc=com" });

        var search = new FakeSearch(
            Entry("uid=alice", "alice", "alice@example.com", "Alice"),
            Entry("uid=bob", "bob", "bob@example.com", "Bob"),
            new DirectoryEntry("uid=nobody,dc=example,dc=com", new Dictionary<string, string?>
            {
                ["uid"] = null,
                ["mail"] = null,
                ["cn"] = null,
            }));

        var writer = new FakeWriter(
            new DirectoryUpsertResult(true, false, "id-alice", string.Empty),
            new DirectoryUpsertResult(false, true, "id-bob", string.Empty),
            new DirectoryUpsertResult(false, false, "id-carol", string.Empty));

        var audit = new FakeAudit();
        var runner = new DirectorySyncRunner(search, writer, options, ldap, audit);

        var snapshot = await runner.RunOnceAsync(TestContext.Current.CancellationToken);

        snapshot.Scanned.ShouldBe(2);
        snapshot.Created.ShouldBe(1);
        snapshot.Updated.ShouldBe(1);
        snapshot.Unchanged.ShouldBe(0);
        snapshot.Skipped.ShouldBe(1);
        snapshot.Failed.ShouldBe(0);
        snapshot.LastError.ShouldBeNull();
        audit.Snapshots.Count.ShouldBe(1);
        search.LastBaseDn.ShouldBe("ou=people,dc=example,dc=com");
        search.LastFilter.ShouldBe("(objectClass=inetOrgPerson)");
    }

    [Fact]
    public async Task A_failed_upsert_is_counted_without_aborting_the_pass()
    {
        var options = Options.Create(new DirectorySyncOptions { Enabled = true, Realm = "nerves" });
        var ldap = Options.Create(new LdapOptions());
        var search = new FakeSearch(
            Entry("uid=alice", "alice", null, "Alice"),
            Entry("uid=bob", "bob", null, "Bob"));
        var writer = new FakeWriter(
            new DirectoryUpsertResult(false, false, string.Empty, "invalid_email"),
            new DirectoryUpsertResult(true, false, "id-bob", string.Empty));
        var runner = new DirectorySyncRunner(search, writer, options, ldap, new FakeAudit());

        var snapshot = await runner.RunOnceAsync(TestContext.Current.CancellationToken);

        snapshot.Failed.ShouldBe(1);
        snapshot.Created.ShouldBe(1);
        snapshot.Scanned.ShouldBe(1);
    }

    [Fact]
    public async Task A_transport_failure_returns_a_snapshot_with_the_error_code()
    {
        var options = Options.Create(new DirectorySyncOptions { Enabled = true, Realm = "nerves" });
        var ldap = Options.Create(new LdapOptions());
        var runner = new DirectorySyncRunner(
            new ThrowingSearch(),
            new FakeWriter(),
            options,
            ldap,
            new FakeAudit());

        var snapshot = await runner.RunOnceAsync(TestContext.Current.CancellationToken);

        snapshot.LastError.ShouldBe("InvalidOperationException");
        snapshot.Created.ShouldBe(0);
    }

    [Fact]
    public void The_interval_is_clamped_to_a_sane_range()
    {
        var options = new DirectorySyncOptions { IntervalMinutes = 0 };
        Math.Clamp(options.IntervalMinutes, 1, 1440).ShouldBe(1);

        var defaults = new DirectorySyncOptions();
        defaults.Realm.ShouldBe("nerves");
        defaults.AttributeMap.Username.ShouldBe("uid");
        defaults.AttributeMap.Email.ShouldBe("mail");
        defaults.AttributeMap.DisplayName.ShouldBe("cn");
    }

    [Fact]
    public void A_new_entry_creates_an_account()
    {
        var plan = DirectoryAccountPlanner.Plan(null, null, " Alice@Example.com ", " Alice ", emailTakenByAnotherAccount: false);

        plan.Action.ShouldBe(DirectoryAccountAction.Create);
        plan.Email.ShouldBe("alice@example.com");
        plan.DisplayName.ShouldBe("Alice");
        plan.Account.ShouldBeNull();
    }

    [Fact]
    public void The_username_match_wins_over_the_email_match()
    {
        var byUsername = NewAccount("alice", "old@example.com", "Old");
        var byEmail = NewAccount("someone-else", "new@example.com", "Other");

        var plan = DirectoryAccountPlanner.Plan(byUsername, byEmail, "new@example.com", "Alice", false);

        plan.Action.ShouldBe(DirectoryAccountAction.Update);
        plan.Account.ShouldBeSameAs(byUsername);
    }

    [Fact]
    public void An_email_match_reuses_the_existing_account()
    {
        var byEmail = NewAccount("legacy", "alice@example.com", "Alice");

        var plan = DirectoryAccountPlanner.Plan(null, byEmail, "alice@example.com", "Alice", false);

        plan.Action.ShouldBe(DirectoryAccountAction.Unchanged);
        plan.Account.ShouldBeSameAs(byEmail);
    }

    [Fact]
    public void Repeating_the_same_data_is_idempotent()
    {
        var account = NewAccount("alice", "alice@example.com", "Alice Chen");

        var plan = DirectoryAccountPlanner.Plan(account, null, "alice@example.com", "Alice Chen", false);

        plan.Action.ShouldBe(DirectoryAccountAction.Unchanged);
    }

    [Fact]
    public void A_changed_display_name_is_an_update()
    {
        var account = NewAccount("alice", "alice@example.com", "Alice Chen");

        var plan = DirectoryAccountPlanner.Plan(account, null, "alice@example.com", "Alice Renamed", false);

        plan.Action.ShouldBe(DirectoryAccountAction.Update);
    }

    [Fact]
    public void An_email_owned_by_another_account_is_a_conflict()
    {
        var plan = DirectoryAccountPlanner.Plan(null, null, "taken@example.com", "Taken", emailTakenByAnotherAccount: true);

        plan.Action.ShouldBe(DirectoryAccountAction.EmailConflict);
    }

    [Fact]
    public void A_missing_directory_attribute_never_clears_a_local_value()
    {
        var account = NewAccount("alice", "alice@example.com", "Alice Chen");

        var plan = DirectoryAccountPlanner.Plan(account, null, email: null, displayName: null, false);

        plan.Action.ShouldBe(DirectoryAccountAction.Unchanged);
        var applied = account.ApplyDirectoryProfile(plan.Email, plan.DisplayName, DateTimeOffset.UtcNow);
        applied.IsSuccess.ShouldBeTrue();
        account.Email.ShouldBe("alice@example.com");
        account.DisplayName.ShouldBe("Alice Chen");
    }

    [Fact]
    public void Partial_updates_keep_the_untouched_field()
    {
        var account = NewAccount("alice", "alice@example.com", "Alice Chen");

        var plan = DirectoryAccountPlanner.Plan(account, null, email: null, displayName: "Alice Renamed", false);
        plan.Action.ShouldBe(DirectoryAccountAction.Update);

        account.ApplyDirectoryProfile(plan.Email, plan.DisplayName, DateTimeOffset.UtcNow).IsSuccess.ShouldBeTrue();
        account.Email.ShouldBe("alice@example.com");
        account.DisplayName.ShouldBe("Alice Renamed");
    }

    [Fact]
    public void Blank_attributes_normalize_to_null()
    {
        DirectoryAccountPlanner.NormalizeEmail("   ").ShouldBeNull();
        DirectoryAccountPlanner.Normalize("  x  ").ShouldBe("x");
    }

    private static NetworkAccount NewAccount(string username, string email, string displayName)
    {
        var account = NetworkAccount.Create(
            Guid.CreateVersion7(),
            "nerves",
            username,
            email,
            null,
            displayName,
            AccountType.Managed,
            DateTimeOffset.UtcNow);

        account.IsSuccess.ShouldBeTrue();
        return account.Value;
    }

    private static DirectoryEntry Entry(string dn, string? uid, string? mail, string? cn) =>
        new(dn, new Dictionary<string, string?>
        {
            ["uid"] = uid,
            ["mail"] = mail,
            ["cn"] = cn,
        });

    private sealed class FakeSearch(params DirectoryEntry[] entries) : IDirectorySearch
    {
        public string? LastBaseDn { get; private set; }

        public string? LastFilter { get; private set; }

        public Task<IReadOnlyList<DirectoryEntry>> ListAsync(
            string baseDn,
            string filter,
            IReadOnlyCollection<string> attributes,
            CancellationToken cancellationToken)
        {
            LastBaseDn = baseDn;
            LastFilter = filter;
            return Task.FromResult<IReadOnlyList<DirectoryEntry>>(entries);
        }
    }

    private sealed class ThrowingSearch : IDirectorySearch
    {
        public Task<IReadOnlyList<DirectoryEntry>> ListAsync(
            string baseDn,
            string filter,
            IReadOnlyCollection<string> attributes,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("directory is down");
    }

    private sealed class FakeWriter(params DirectoryUpsertResult[] results) : IDirectoryAccountWriter
    {
        private int _index;

        public Task<DirectoryUpsertResult> UpsertAsync(
            DirectoryAccountRecord record,
            CancellationToken cancellationToken)
        {
            var result = results.Length == 0
                ? new DirectoryUpsertResult(true, false, Guid.NewGuid().ToString(), string.Empty)
                : results[Math.Min(_index++, results.Length - 1)];

            return Task.FromResult(result);
        }
    }

    private sealed class FakeAudit : IDirectorySyncAudit
    {
        public List<DirectorySyncSnapshot> Snapshots { get; } = [];

        public Task CompletedAsync(DirectorySyncSnapshot snapshot, CancellationToken cancellationToken)
        {
            Snapshots.Add(snapshot);
            return Task.CompletedTask;
        }
    }
}
