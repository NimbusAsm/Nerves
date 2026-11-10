using Nerves.Core;

namespace Nerves.Notification.Templates;

/// <summary>One named template and its default subject/body.</summary>
public sealed record EmailTemplate(string Code, string Subject, string Body);

/// <summary>
/// Built-in templates. Bodies are plain text (not HTML) because the notification
/// service never renders user-supplied markup.
/// </summary>
public static class EmailTemplateCatalog
{
    private static readonly Dictionary<string, EmailTemplate> Templates = new(StringComparer.Ordinal)
    {
        ["account.welcome"] = new(
            "account.welcome",
            "欢迎加入 {{displayName}}",
            """
            您好 {{displayName}}：

            您的 Nerves 账户 {{username}} 已创建完成，可以在 {{issuer}} 登录。

            如果这不是您本人的操作，请联系管理员。
            """),

        ["account.password-reset"] = new(
            "account.password-reset",
            "重置您的 Nerves 账户密码",
            """
            您好 {{displayName}}：

            请在 {{minutes}} 分钟内打开以下链接重置口令：

            {{resetUrl}}

            如果这不是您本人的操作，请忽略本邮件。
            """),

        ["security.sign-in"] = new(
            "security.sign-in",
            "新的登录：{{device}}",
            """
            您好 {{displayName}}：

            您的账户于 {{occurredAt}} 在 {{device}} 登录。
            如果这不是您本人的操作，请立即重置口令并联系管理员。
            """),

        ["account.realname-changed"] = new(
            "account.realname-changed",
            "实名信息更新：{{state}}",
            """
            您好 {{displayName}}：

            您的实名信息于 {{occurredAt}} 更新，当前状态为 {{state}}。

            如果这不是您本人的操作，请联系管理员。
            """),

        // Short enough to stay in one SMS segment after rendering; the SMS
        // transport sends the body only.
        ["security.sms-code"] = new(
            "security.sms-code",
            "验证码",
            "您的验证码是 {{code}}，{{minutes}} 分钟内有效。请勿转发给他人。"),
    };

    public static IReadOnlyCollection<string> Codes => Templates.Keys;

    public static bool TryGet(string code, out EmailTemplate template) =>
        Templates.TryGetValue(code, out template!);

    /// <summary>
    /// Renders a template. Unknown placeholders are a validation failure: sending
    /// a message that still contains <c>{{resetUrl}}</c> would confuse users more
    /// than refusing the request.
    /// </summary>
    public static Result<RenderedEmail> Render(string code, IReadOnlyDictionary<string, string>? values)
    {
        if (!TryGet(code, out var template))
        {
            return Result.Failure<RenderedEmail>(
                Reason.Validation($"unknown template '{code}'; known templates: {string.Join(", ", Codes)}"));
        }

        var subject = RenderText(template.Subject, values, out var subjectError);
        if (subjectError is not null)
        {
            return Result.Failure<RenderedEmail>(Reason.Validation(subjectError));
        }

        var body = RenderText(template.Body, values, out var bodyError);
        return bodyError is not null
            ? Result.Failure<RenderedEmail>(Reason.Validation(bodyError))
            : Result.Success(new RenderedEmail(template.Code, subject, body));
    }

    private static string RenderText(
        string text,
        IReadOnlyDictionary<string, string>? values,
        out string? error)
    {
        error = null;
        var missing = new List<string>();

        var rendered = TemplateRenderer.Render(text, values, missing);

        if (missing.Count > 0)
        {
            error = $"missing template values: {string.Join(", ", missing.Distinct(StringComparer.Ordinal))}";
        }

        return rendered;
    }
}
