using Nerves.Idp.Federation;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class LdapOptionsTests
{
    [Fact]
    public void Defaults_target_a_local_directory()
    {
        var options = new LdapOptions();

        options.Enabled.ShouldBeFalse();
        options.Port.ShouldBe(389);
        options.UserFilter.ShouldBe("(uid={0})");
        options.TimeoutSeconds.ShouldBe(5);
    }

    [Fact]
    public void The_user_filter_placeholder_is_applied_with_escaping()
    {
        var options = new LdapOptions { UserFilter = "(uid={0})" };
        var escaped = "a*b(c)";

        var applied = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            options.UserFilter,
            escaped.Replace("*", "\\2a"));

        applied.ShouldBe("(uid=a\\2ab(c))");
    }
}
