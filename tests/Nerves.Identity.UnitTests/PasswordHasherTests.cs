using Nerves.Identity.Security;
using Shouldly;

namespace Nerves.Identity.UnitTests;

public sealed class PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_is_self_describing_and_salted()
    {
        var first = _hasher.Hash("correct horse battery staple");
        var second = _hasher.Hash("correct horse battery staple");

        first.ShouldStartWith("pbkdf2-sha512$100000$");
        first.ShouldNotBe(second);
        first.ShouldNotContain("correct horse");
    }

    [Fact]
    public void Verify_accepts_the_right_password()
    {
        var hash = _hasher.Hash("s3cret");

        _hasher.Verify("s3cret", hash).ShouldBeTrue();
        _hasher.Verify("S3cret", hash).ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("pbkdf2-sha512$abc$x$y")]
    [InlineData("md5$1$x$y")]
    public void Verify_rejects_malformed_hashes(string hash)
    {
        _hasher.Verify("s3cret", hash).ShouldBeFalse();
    }
}
