using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;

namespace Nerves.Idp.Security;

/// <summary>
/// Owns the RSA key used to sign ID tokens and logout tokens. Development uses
/// a PEM file under the user profile so restarts keep tokens valid; production
/// requires a PFX path in configuration.
/// </summary>
public sealed class SigningKeyProvider
{
    public SigningKeyProvider(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        RSA rsa;

        if (environment.IsDevelopment())
        {
            var path = configuration["OpenIddict:DevelopmentSigningKeyPath"]
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".nerves",
                    "dev-signing-key.pem");

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            rsa = RSA.Create(2048);

            if (File.Exists(path))
            {
                rsa.ImportFromPem(File.ReadAllText(path));
            }
            else
            {
                File.WriteAllText(path, rsa.ExportPkcs8PrivateKeyPem());
            }
        }
        else
        {
            var path = configuration["OpenIddict:SigningCertificatePath"]
                ?? throw new InvalidOperationException(
                    "OpenIddict:SigningCertificatePath must point to a PFX file outside Development.");

            var password = configuration["OpenIddict:SigningCertificatePassword"];
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(path, password);
            rsa = certificate.GetRSAPrivateKey()
                ?? throw new InvalidOperationException("The signing certificate has no RSA private key.");
        }

        Key = new RsaSecurityKey(rsa)
        {
            KeyId = Convert.ToHexString(SHA256.HashData(rsa.ExportRSAPublicKey()))[..32],
        };
    }

    public RsaSecurityKey Key { get; }
}
