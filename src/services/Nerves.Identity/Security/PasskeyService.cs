using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.EntityFrameworkCore;
using Nerves.Identity.Domain;
using Nerves.Identity.Persistence;

namespace Nerves.Identity.Security;

/// <summary>
/// WebAuthn ceremonies backed by Fido2NetLib. Challenges persist in the
/// identity schema, so no server-side authentication session is required.
/// </summary>
public sealed class PasskeyService(IFido2 fido2, IdentityDbContext db, TotpSecretProtector protector)
{
    private const string RpId = "nerves.local";
    private const string RpName = "Nerves";

    public async Task<(string Options, string ChallengeId)> StartRegistrationAsync(
        Guid accountId,
        string userName,
        string displayName,
        CancellationToken cancellationToken)
    {
        var exclude = (await LoadPasskeysAsync(accountId, cancellationToken))
            .Select(x => new PublicKeyCredentialDescriptor(Base64Url.DecodeFromChars(x.CredentialId)))
            .ToList();

        var options = fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = new Fido2User
            {
                Id = Encoding.UTF8.GetBytes(accountId.ToString()),
                Name = userName,
                DisplayName = displayName,
            },
            ExcludeCredentials = exclude,
            AuthenticatorSelection = new AuthenticatorSelection
            {
                ResidentKey = ResidentKeyRequirement.Preferred,
                UserVerification = UserVerificationRequirement.Preferred,
            },
            AttestationPreference = AttestationConveyancePreference.None,
        });

        var json = JsonSerializer.Serialize(options);
        var challengeId = await StoreChallengeAsync(accountId, options.Challenge, "registration", json, cancellationToken);
        return (json, challengeId);
    }

    public async Task<(bool Ok, string? Error)> CompleteRegistrationAsync(
        Guid accountId,
        string challengeId,
        JsonElement attestation,
        string? nickname,
        CancellationToken cancellationToken)
    {
        var pending = await TakeChallengeAsync(accountId, challengeId, "registration", cancellationToken);
        if (pending is null)
        {
            return (false, "registration challenge expired");
        }

        var options = JsonSerializer.Deserialize<CredentialCreateOptions>(pending.OptionsJson)!;

        AuthenticatorAttestationRawResponse response;
        try
        {
            response = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(attestation.GetRawText())!;
        }
        catch (JsonException)
        {
            return (false, "invalid attestation payload");
        }

        var stored = await LoadPasskeysAsync(accountId, cancellationToken);

        RegisteredPublicKeyCredential created;
        try
        {
            created = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = response,
                OriginalOptions = options,
                IsCredentialIdUniqueToUserCallback = (args, _) =>
                    Task.FromResult(stored.All(x => x.CredentialId != Base64Url.EncodeToString(args.CredentialId))),
            }, cancellationToken);
        }
        catch (Fido2VerificationException exception)
        {
            return (false, exception.Message);
        }

        var passkey = new StoredPasskey(
            Base64Url.EncodeToString(created.Id),
            Base64Url.EncodeToString(created.PublicKey),
            created.SignCount,
            string.IsNullOrWhiteSpace(nickname) ? "passkey" : nickname);

        var credential = AccountCredential.Create(
            accountId,
            CredentialType.Passkey,
            protector.Protect(JsonSerializer.Serialize(passkey)),
            "webauthn",
            DateTimeOffset.UtcNow);

        if (credential.IsFailure)
        {
            return (false, credential.Reason.Message);
        }

        credential.Value.Activate(DateTimeOffset.UtcNow);
        db.AccountCredentials.Add(credential.Value);
        await db.SaveChangesAsync(cancellationToken);

        return (true, null);
    }

    public async Task<(string Options, string ChallengeId)> StartAssertionAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var stored = await LoadPasskeysAsync(accountId, cancellationToken);

        var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = stored
                .Select(x => new PublicKeyCredentialDescriptor(Base64Url.DecodeFromChars(x.CredentialId)))
                .ToList(),
            UserVerification = UserVerificationRequirement.Preferred,
        });

        var json = JsonSerializer.Serialize(options);
        var challengeId = await StoreChallengeAsync(accountId, options.Challenge, "assertion", json, cancellationToken);
        return (json, challengeId);
    }

    public async Task<(bool Ok, string? Error)> CompleteAssertionAsync(
        Guid accountId,
        string challengeId,
        JsonElement assertion,
        CancellationToken cancellationToken)
    {
        var pending = await TakeChallengeAsync(accountId, challengeId, "assertion", cancellationToken);
        if (pending is null)
        {
            return (false, "assertion challenge expired");
        }

        AuthenticatorAssertionRawResponse response;
        try
        {
            response = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(assertion.GetRawText())!;
        }
        catch (JsonException)
        {
            return (false, "invalid assertion payload");
        }

        var credentialId = Base64Url.EncodeToString(response.RawId);
        AccountCredential? matched = null;
        StoredPasskey? storedPasskey = null;

        foreach (var candidate in await db.AccountCredentials
                     .Where(x => x.AccountId == accountId && x.Type == CredentialType.Passkey && x.ActivatedAt != null)
                     .ToListAsync(cancellationToken))
        {
            var plain = protector.TryUnprotect(candidate.Secret);
            if (plain is null)
            {
                continue;
            }

            var parsed = JsonSerializer.Deserialize<StoredPasskey>(plain)!;
            if (parsed.CredentialId == credentialId)
            {
                matched = candidate;
                storedPasskey = parsed;
                break;
            }
        }

        if (matched is null || storedPasskey is null)
        {
            return (false, "unknown credential");
        }

        var options = JsonSerializer.Deserialize<AssertionOptions>(pending.OptionsJson)!;

        try
        {
            var result = await fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = response,
                OriginalOptions = options,
                StoredPublicKey = Base64Url.DecodeFromChars(storedPasskey.PublicKey),
                StoredSignatureCounter = storedPasskey.SignCount,
                IsUserHandleOwnerOfCredentialIdCallback = (_, _) => Task.FromResult(true),
            }, cancellationToken);

            var updated = storedPasskey with { SignCount = result.SignCount };
            matched.ReplaceSecret(protector.Protect(JsonSerializer.Serialize(updated)), "webauthn", DateTimeOffset.UtcNow);
            matched.MarkUsed(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(cancellationToken);

            return (true, null);
        }
        catch (Fido2VerificationException exception)
        {
            return (false, exception.Message);
        }
    }

    private async Task<List<StoredPasskey>> LoadPasskeysAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var secrets = await db.AccountCredentials
            .Where(x => x.AccountId == accountId && x.Type == CredentialType.Passkey)
            .Select(x => x.Secret)
            .ToListAsync(cancellationToken);

        var result = new List<StoredPasskey>();

        foreach (var secret in secrets)
        {
            var plain = protector.TryUnprotect(secret);
            if (plain is not null)
            {
                result.Add(JsonSerializer.Deserialize<StoredPasskey>(plain)!);
            }
        }

        return result;
    }

    private async Task<string> StoreChallengeAsync(
        Guid accountId,
        byte[] challenge,
        string purpose,
        string optionsJson,
        CancellationToken cancellationToken)
    {
        var record = PasskeyChallenge.Create(
            accountId, Base64Url.EncodeToString(challenge), purpose, optionsJson, TimeSpan.FromMinutes(5));
        db.PasskeyChallenges.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        return record.Id.ToString();
    }

    private async Task<PasskeyChallenge?> TakeChallengeAsync(
        Guid accountId,
        string challengeId,
        string purpose,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(challengeId, out var id))
        {
            return null;
        }

        var record = await db.PasskeyChallenges.FirstOrDefaultAsync(
            x => x.Id == id && x.AccountId == accountId && x.Purpose == purpose,
            cancellationToken);

        if (record is null || record.ConsumedAt is not null || record.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return null;
        }

        record.Consume();
        await db.SaveChangesAsync(cancellationToken);
        return record;
    }

    private sealed record StoredPasskey(string CredentialId, string PublicKey, uint SignCount, string Nickname);
}
