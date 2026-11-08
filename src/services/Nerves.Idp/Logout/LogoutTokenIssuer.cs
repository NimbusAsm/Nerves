using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Nerves.Idp.Security;

namespace Nerves.Idp.Logout;

/// <summary>Signs OpenID Connect back-channel logout tokens (RS256).</summary>
public sealed class LogoutTokenIssuer(SigningKeyProvider signingKey)
{
    public const string BackchannelLogoutEvent = "http://schemas.openid.net/event/backchannel-logout";

    public string Issue(string issuer, string clientId, string subject, DateTimeOffset now)
    {
        var header = new Dictionary<string, object>
        {
            ["alg"] = "RS256",
            ["typ"] = "JWT",
            ["kid"] = signingKey.Key.KeyId,
        };

        var payload = new Dictionary<string, object>
        {
            ["iss"] = issuer,
            ["aud"] = clientId,
            ["sub"] = subject,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(2).ToUnixTimeSeconds(),
            ["jti"] = Guid.CreateVersion7(now).ToString(),
            ["events"] = new Dictionary<string, object> { [BackchannelLogoutEvent] = new Dictionary<string, object>() },
        };

        var signingInput = Base64Url(JsonSerializer.SerializeToUtf8Bytes(header))
            + "."
            + Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload));

        var signature = signingKey.Key.Rsa!.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return signingInput + "." + Base64Url(signature);
    }

    private static string Base64Url(byte[] bytes) =>
        System.Buffers.Text.Base64Url.EncodeToString(bytes);
}
