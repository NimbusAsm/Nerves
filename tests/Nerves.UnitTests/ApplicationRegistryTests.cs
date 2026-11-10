using Nerves.Idp.Applications;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class ApplicationScopeTests
{
    [Fact]
    public void Normalize_trims_lowercases_and_deduplicates()
    {
        ApplicationScopes.Normalize([" PROFILE ", "email", "profile", ""])
            .ShouldBe(["email", "profile"]);
    }

    [Fact]
    public void Permissions_always_include_the_code_flow_baseline()
    {
        var permissions = ApplicationScopes.BuildPermissions(["profile", "nerves.api"]);

        permissions.ShouldContain("ept:authorization");
        permissions.ShouldContain("ept:token");
        permissions.ShouldContain("gt:authorization_code");
        permissions.ShouldContain("rst:code");
        permissions.ShouldContain("scp:profile");
        permissions.ShouldContain("scp:nerves.api");
    }

    [Fact]
    public void Openid_does_not_become_a_scope_permission()
    {
        var permissions = ApplicationScopes.BuildPermissions(["openid", "email"]);

        permissions.ShouldNotContain("scp:openid");
        permissions.ShouldContain("scp:email");
    }

    [Fact]
    public void Scopes_round_trip_through_the_permission_list()
    {
        var scopes = new[] { "openid", "profile", "email", "nerves.api" };
        var permissions = ApplicationScopes.BuildPermissions(scopes);

        ApplicationScopes.ReadScopes(permissions).ShouldBe(scopes.OrderBy(scope => scope, StringComparer.Ordinal));
    }

    [Fact]
    public void Unknown_scopes_are_not_offered_back()
    {
        ApplicationScopes.ReadScopes(["scp:profile", "scp:unknown"]).ShouldBe(["openid", "profile"]);
    }
}

public sealed class ApplicationValidationTests
{
    [Theory]
    [InlineData("crequency-site")]
    [InlineData("abc")]
    [InlineData("nerves-admin")]
    public void Valid_client_ids_pass(string clientId) =>
        ApplicationValidation.ValidateClientId(clientId).IsSuccess.ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("1abc")]
    [InlineData("UPPER")]
    [InlineData("has space")]
    [InlineData("trailing-")]
    public void Invalid_client_ids_fail(string clientId) =>
        ApplicationValidation.ValidateClientId(clientId).IsFailure.ShouldBeTrue();

    [Fact]
    public void Redirect_uris_must_be_https_or_loopback_http()
    {
        ApplicationValidation.ValidateRedirectUris(["https://app.example.com/cb"], required: true)
            .IsSuccess.ShouldBeTrue();
        ApplicationValidation.ValidateRedirectUris(["http://127.0.0.1:5300/cb"], required: true)
            .IsSuccess.ShouldBeTrue();
        ApplicationValidation.ValidateRedirectUris(["http://app.example.com/cb"], required: true)
            .IsFailure.ShouldBeTrue();
        ApplicationValidation.ValidateRedirectUris(["not-a-uri"], required: true)
            .IsFailure.ShouldBeTrue();
        ApplicationValidation.ValidateRedirectUris([], required: true)
            .IsFailure.ShouldBeTrue();
        ApplicationValidation.ValidateRedirectUris([], required: false)
            .IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Line_breaks_in_uris_are_rejected()
    {
        ApplicationValidation.ValidateRedirectUris(["https://app.example.com/cb\nhttps://evil.example.com"], required: true)
            .IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Scopes_are_validated_against_the_catalog()
    {
        ApplicationValidation.ValidateScopes(["openid", "profile"]).IsSuccess.ShouldBeTrue();
        ApplicationValidation.ValidateScopes([]).IsFailure.ShouldBeTrue();
        ApplicationValidation.ValidateScopes(["openid", "admin"]).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Display_name_is_required_and_bounded()
    {
        ApplicationValidation.ValidateDisplayName("  ").IsFailure.ShouldBeTrue();
        ApplicationValidation.ValidateDisplayName("官网").IsSuccess.ShouldBeTrue();
        ApplicationValidation.ValidateDisplayName(new string('x', ApplicationValidation.MaxDisplayNameLength + 1))
            .IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Built_in_clients_are_protected()
    {
        ApplicationMetadata.IsBuiltIn("nerves-admin").ShouldBeTrue();
        ApplicationMetadata.IsBuiltIn("nerves-demo").ShouldBeTrue();
        ApplicationMetadata.IsBuiltIn("crequency-site").ShouldBeFalse();
        ApplicationMetadata.IsBuiltIn(null).ShouldBeFalse();
    }

    [Fact]
    public void Status_reads_from_properties()
    {
        var descriptor = new OpenIddict.Abstractions.OpenIddictApplicationDescriptor();
        ApplicationMetadata.ReadStatus(descriptor).ShouldBe(ApplicationStatus.Active);

        descriptor.SetString(ApplicationMetadata.StatusKey, "disabled");
        ApplicationMetadata.ReadStatus(descriptor).ShouldBe(ApplicationStatus.Disabled);
        ApplicationStatus.Disabled.ToWire().ShouldBe("disabled");
        ApplicationStatus.Active.ToWire().ShouldBe("active");
    }

    [Fact]
    public void Timestamps_round_trip_in_roundtrip_format()
    {
        var now = new DateTimeOffset(2026, 11, 10, 18, 0, 0, TimeSpan.Zero);
        var descriptor = new OpenIddict.Abstractions.OpenIddictApplicationDescriptor();
        descriptor.SetString(ApplicationMetadata.CreatedAtKey, ApplicationMetadata.FormatTimestamp(now));

        ApplicationMetadata.ReadTimestamp(descriptor.Properties, ApplicationMetadata.CreatedAtKey).ShouldBe(now);
        ApplicationMetadata.ReadTimestamp(null, ApplicationMetadata.CreatedAtKey)
            .ShouldBe(DateTimeOffset.UnixEpoch);
    }
}

public sealed class ApplicationCursorTests
{
    [Fact]
    public void Encode_returns_null_for_the_first_page()
    {
        ApplicationCursor.Encode(0).ShouldBeNull();
        ApplicationCursor.Encode(-1).ShouldBeNull();
        ApplicationCursor.Encode(10).ShouldNotBeNull();
    }

    [Fact]
    public void Cursors_round_trip()
    {
        var cursor = ApplicationCursor.Encode(42);

        ApplicationCursor.TryDecode(cursor, out var offset, out var error).ShouldBeTrue();
        offset.ShouldBe(42);
        error.ShouldBeNull();
    }

    [Theory]
    [InlineData("not-a-cursor")]
    [InlineData("!!")]
    public void Malformed_cursors_fail(string cursor)
    {
        ApplicationCursor.TryDecode(cursor, out _, out var error).ShouldBeFalse();
        error.ShouldNotBeNull();
    }

    [Fact]
    public void An_empty_cursor_means_the_first_page()
    {
        ApplicationCursor.TryDecode(null, out var offset, out _).ShouldBeTrue();
        offset.ShouldBe(0);
        ApplicationCursor.TryDecode("", out _, out _).ShouldBeTrue();
    }

    [Fact]
    public void A_cursor_without_the_expected_prefix_fails()
    {
        var raw = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("o:12"))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        ApplicationCursor.TryDecode(raw, out _, out var error).ShouldBeFalse();
        error.ShouldNotBeNull();
    }
}
