using Nerves.Notification.Templates;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class NotificationTemplateTests
{
    private static readonly IReadOnlyDictionary<string, string> Values = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["displayName"] = "张伟",
        ["username"] = "zhang.wei",
        ["issuer"] = "https://nerves.example.com/",
    };

    [Fact]
    public void Renderer_replaces_known_placeholders()
    {
        var rendered = TemplateRenderer.Render("hello {{name}}, welcome to {{place}}", new Dictionary<string, string>
        {
            ["name"] = "Ada",
            ["place"] = "Nerves",
        });

        rendered.ShouldBe("hello Ada, welcome to Nerves");
    }

    [Fact]
    public void Renderer_reports_unknown_placeholders_and_keeps_them()
    {
        var missing = new List<string>();

        var rendered = TemplateRenderer.Render("hello {{name}} / {{other}}", new Dictionary<string, string>
        {
            ["name"] = "Ada",
        }, missing);

        rendered.ShouldBe("hello Ada / {{other}}");
        missing.ShouldBe(["other"]);
    }

    [Fact]
    public void Renderer_treats_an_unclosed_placeholder_as_literal_text()
    {
        TemplateRenderer.Render("a {{unclosed", null).ShouldBe("a {{unclosed");
    }

    [Fact]
    public void Renderer_lists_placeholders_in_first_seen_order()
    {
        TemplateRenderer.Placeholders("{{b}} {{a}} {{b}}").ShouldBe(["b", "a"]);
    }

    [Fact]
    public void The_welcome_template_renders_with_the_documented_values()
    {
        var result = EmailTemplateCatalog.Render("account.welcome", Values);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Template.ShouldBe("account.welcome");
        result.Value.Subject.ShouldContain("张伟");
        result.Value.Body.ShouldContain("zhang.wei");
        result.Value.Body.ShouldNotContain("{{");
    }

    [Fact]
    public void Every_built_in_template_renders_when_its_own_placeholders_are_supplied()
    {
        foreach (var code in EmailTemplateCatalog.Codes)
        {
            EmailTemplateCatalog.TryGet(code, out var template).ShouldBeTrue();
            var placeholders = TemplateRenderer.Placeholders(template.Subject + "\n" + template.Body);
            placeholders.ShouldNotBeEmpty();

            var values = placeholders.ToDictionary(
                placeholder => placeholder,
                placeholder => "<" + placeholder + ">",
                StringComparer.Ordinal);

            var result = EmailTemplateCatalog.Render(code, values);

            result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Reason.Message : code);
            result.Value.Subject.ShouldNotContain("{{");
            result.Value.Body.ShouldNotContain("{{");
            result.Value.Template.ShouldBe(code);
        }
    }

    [Fact]
    public void An_unknown_template_is_a_validation_failure()
    {
        var result = EmailTemplateCatalog.Render("does.not.exist", Values);

        result.IsFailure.ShouldBeTrue();
        result.Reason.Code.ShouldBe("validation.failed");
        result.Reason.Message.ShouldContain("does.not.exist");
    }

    [Fact]
    public void Missing_template_values_are_rejected_instead_of_sent()
    {
        var result = EmailTemplateCatalog.Render("account.password-reset", new Dictionary<string, string>
        {
            ["displayName"] = "张伟",
        });

        result.IsFailure.ShouldBeTrue();
        result.Reason.Message.ShouldContain("resetUrl");
        result.Reason.Message.ShouldContain("minutes");
    }
}
