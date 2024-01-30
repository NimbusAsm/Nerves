using System.Security.Cryptography;
using System.Text;
using Nerves.Shared.Models.User;

namespace Nerves.ApiServer.Utils;

public static class UserUtil
{
    public static void UpdatePasswordHash(User user, string new_passwd)
    {
        var salts = user.SecurityInfo!.UserPasswordHashSalt!.Split(" ~ ");
        var saltPrefix = salts[0];
        var saltSuffix = salts[1];

        user.SecurityInfo!.UserPasswordHash = GetSHA256($"{saltPrefix}{new_passwd}{saltSuffix}");
    }

    public static bool VerifyPassword(string given_pwd, User target_user)
    {
        var salts = target_user.SecurityInfo!.UserPasswordHashSalt!.Split(" ~ ");
        var saltPrefix = salts[0];
        var saltSuffix = salts[1];

        return GetSHA256($"{saltPrefix}{given_pwd}{saltSuffix}").Equals(target_user.SecurityInfo.UserPasswordHash);
    }

    public static string GetSHA256(string source)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(source));

        var builder = new StringBuilder();
        for (int i = 0; i < bytes.Length; i++)
            builder.Append(bytes[i].ToString("x2"));

        return builder.ToString();
    }

    public static User GetDefaultAdmin()
    {
        var joinTime = DateTime.Now;
        var saltPrefix = joinTime.ToString("yyyy-MM-dd HH:mm:ss");
        var saltSuffix = joinTime.ToString("ss:mm:HH dd-MM-yyyy");

        return new()
        {
            Id = "admin",
            NickName = "admin",
            JoinTime = joinTime,
            LastLoginTime = joinTime,
            Data = new()
            {

            },
            SecurityInfo = new()
            {
                VerifiedEmails =
                [
                    $"admin@nerves.io"
                ],
                VerifiedPhoneNumbers = [],
                VerifiedDevices = [],
                UserPasswordHash = GetSHA256($"{saltPrefix}admin1q2w3E*{saltSuffix}"),
                UserPasswordHashSalt = $"{saltPrefix} ~ {saltSuffix}"
            },
            IsOnline = true
        };
    }
}