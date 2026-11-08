using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Nerves.Idp.Logout;
using Nerves.Idp.Security;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class LogoutTokenTests
{
    private static LogoutTokenIssuer CreateIssuer()
    {
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var environment = new TestEnvironment();
        var provider = new SigningKeyProvider(configuration, environment);
        return new LogoutTokenIssuer(provider);
    }

    [Fact]
    public void Issues_a_signed_token_with_the_required_claims()
    {
        var issuer = CreateIssuer();
        var token = issuer.Issue("http://127.0.0.1:5101/", "nerves-demo", "account-1", DateTimeOffset.UtcNow);

        token.Split('.').Length.ShouldBe(3);

        var payload = Decode(token.Split('.')[1]);
        payload.GetProperty("iss").GetString().ShouldBe("http://127.0.0.1:5101/");
        payload.GetProperty("aud").GetString().ShouldBe("nerves-demo");
        payload.GetProperty("sub").GetString().ShouldBe("account-1");
        payload.GetProperty("events").TryGetProperty(LogoutTokenIssuer.BackchannelLogoutEvent, out _).ShouldBeTrue();
        payload.TryGetProperty("nonce", out _).ShouldBeFalse();
    }

    [Fact]
    public void Signs_the_token_with_the_published_key()
    {
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var environment = new TestEnvironment();
        var provider = new SigningKeyProvider(configuration, environment);
        var issuer = new LogoutTokenIssuer(provider);

        var token = issuer.Issue("issuer", "client", "subject", DateTimeOffset.UtcNow);
        var parts = token.Split('.');

        var header = Decode(parts[0]);
        header.GetProperty("alg").GetString().ShouldBe("RS256");
        header.GetProperty("kid").GetString().ShouldBe(provider.Key.KeyId);

        var valid = provider.Key.Rsa!.VerifyData(
            Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]),
            System.Buffers.Text.Base64Url.DecodeFromChars(parts[2]),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        valid.ShouldBeTrue();
    }

    private static JsonElement Decode(string part) =>
        JsonDocument.Parse(System.Buffers.Text.Base64Url.DecodeFromChars(part)).RootElement;

    private sealed class TestEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Microsoft.Extensions.Hosting.Environments.Development;

        public string ApplicationName { get; set; } = "tests";

        public string ContentRootPath { get; set; } = Path.GetTempPath();

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
