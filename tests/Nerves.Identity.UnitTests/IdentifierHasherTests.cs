using Microsoft.Extensions.Options;
using Nerves.Identity.Security;
using Shouldly;

namespace Nerves.Identity.UnitTests;

public sealed class IdentifierHasherTests
{
    private static readonly string Key = Convert.ToBase64String(new byte[32]);

    private static HmacIdentifierHasher Create() =>
        new(Options.Create(new IdentitySecurityOptions { IdentifierHashKey = Key }));

    [Fact]
    public void Is_deterministic_and_case_insensitive()
    {
        var hasher = Create();

        hasher.Hash("username", "Core", "  Alice ")
            .ShouldBe(hasher.Hash("username", "core", "alice"));
    }

    [Fact]
    public void Realm_and_type_are_part_of_the_key()
    {
        var hasher = Create();

        hasher.Hash("username", "core", "alice")
            .ShouldNotBe(hasher.Hash("username", "other", "alice"));

        hasher.Hash("username", "core", "alice")
            .ShouldNotBe(hasher.Hash("email", "core", "alice"));
    }

    [Fact]
    public void Refuses_to_start_without_a_key()
    {
        Should.Throw<InvalidOperationException>(() =>
            new HmacIdentifierHasher(Options.Create(new IdentitySecurityOptions())));
    }

    [Fact]
    public void Refuses_short_keys()
    {
        var shortKey = Convert.ToBase64String(new byte[16]);

        Should.Throw<InvalidOperationException>(() =>
            new HmacIdentifierHasher(Options.Create(new IdentitySecurityOptions { IdentifierHashKey = shortKey })));
    }
}
