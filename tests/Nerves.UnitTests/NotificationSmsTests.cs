using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nerves.Notification.Delivery;
using Nerves.Notification.Domain;
using Nerves.Notification.Options;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class NotificationSmsTests
{
    [Theory]
    [InlineData("+8613800138000", "+8613800138000")]
    [InlineData(" +86 138-0013-8000 ", "+8613800138000")]
    [InlineData("(861)3800138000", "8613800138000")]
    public void International_numbers_are_normalised(string raw, string expected)
    {
        var result = PhoneNumber.Create(raw);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-number")]
    [InlineData("+0123456789")]
    [InlineData("123")]
    [InlineData("(010)12345678")]
    public void Invalid_numbers_are_rejected(string raw)
    {
        var result = PhoneNumber.Create(raw);

        result.IsFailure.ShouldBeTrue();
        result.Reason.Code.ShouldBe("validation.failed");
    }

    [Fact]
    public void A_disabled_sms_sender_needs_no_other_setting()
    {
        new SmsOptions { Disabled = true }.Validate().IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void A_webhook_provider_requires_an_absolute_url()
    {
        var result = new SmsOptions { Disabled = false, Provider = "webhook", WebhookUrl = "not-a-url" }.Validate();

        result.IsFailure.ShouldBeTrue();
        result.Reason.Message.ShouldContain("WebhookUrl");
    }

    [Fact]
    public void A_twilio_provider_requires_credentials_and_a_sender()
    {
        var missing = new SmsOptions { Disabled = false, Provider = "twilio" }.Validate();
        missing.IsFailure.ShouldBeTrue();

        var complete = new SmsOptions
        {
            Disabled = false,
            Provider = "twilio",
            AccountSid = "AC123",
            AuthToken = "token",
            From = "+15550001111",
        }.Validate();
        complete.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void An_unknown_provider_is_rejected()
    {
        var result = new SmsOptions { Disabled = false, Provider = "smoke-signals" }.Validate();

        result.IsFailure.ShouldBeTrue();
        result.Reason.Message.ShouldContain("Sms:Provider");
    }

    [Fact]
    public async Task The_webhook_provider_posts_json_with_a_bearer_token()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "{\"ok\":true}");
        using var factory = new StubHttpClientFactory(handler);
        var sender = new SmsSender(
            factory,
            Options.Create(new SmsOptions
            {
                Disabled = false,
                Provider = "webhook",
                WebhookUrl = "https://sms.example.test/send",
                WebhookToken = "secret",
            }),
            NullLogger<SmsSender>.Instance);

        await sender.SendAsync(new SmsSendRequest("+8613800138000", "验证码 123456"), CancellationToken.None);

        var request = handler.Requests.Single();
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri!.AbsoluteUri.ShouldBe("https://sms.example.test/send");
        request.Authorization.ShouldBe("Bearer secret");
        request.ContentType!.ShouldContain("application/json");

        using var payload = JsonDocument.Parse(request.Body!);
        payload.RootElement.GetProperty("to").GetString().ShouldBe("+8613800138000");
        payload.RootElement.GetProperty("body").GetString().ShouldBe("验证码 123456");
    }

    [Fact]
    public async Task The_twilio_provider_posts_the_messages_form()
    {
        var handler = new RecordingHandler(HttpStatusCode.Created, "{}");
        using var factory = new StubHttpClientFactory(handler);
        var sender = new SmsSender(
            factory,
            Options.Create(new SmsOptions
            {
                Disabled = false,
                Provider = "twilio",
                AccountSid = "AC123",
                AuthToken = "token",
                From = "+15550001111",
                TwilioBaseUrl = "https://twilio.example.test",
            }),
            NullLogger<SmsSender>.Instance);

        await sender.SendAsync(new SmsSendRequest("+8613800138000", "已验证"), CancellationToken.None);

        var request = handler.Requests.Single();
        request.Uri!.AbsoluteUri.ShouldBe("https://twilio.example.test/2010-04-01/Accounts/AC123/Messages.json");
        request.Authorization!.ShouldStartWith("Basic ");
        request.ContentType!.ShouldContain("application/x-www-form-urlencoded");
        request.Body!.ShouldContain("To=%2B8613800138000");
        request.Body!.ShouldContain("From=%2B15550001111");
    }

    [Fact]
    public async Task A_rejected_message_surfaces_the_gateway_status()
    {
        var handler = new RecordingHandler(HttpStatusCode.BadGateway, "upstream exploded");
        using var factory = new StubHttpClientFactory(handler);
        var sender = new SmsSender(
            factory,
            Options.Create(new SmsOptions
            {
                Disabled = false,
                Provider = "webhook",
                WebhookUrl = "https://sms.example.test/send",
            }),
            NullLogger<SmsSender>.Instance);

        var exception = await Should.ThrowAsync<HttpRequestException>(
            () => sender.SendAsync(new SmsSendRequest("+8613800138000", "hi"), CancellationToken.None));

        exception.Message.ShouldContain("502");
        exception.Message.ShouldContain("upstream exploded");
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory, IDisposable
    {
        private readonly HttpClient _client = new(handler, disposeHandler: false);

        public HttpClient CreateClient(string name) => _client;

        public void Dispose() => _client.Dispose();
    }

    private sealed class RecordingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var content = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri,
                request.Headers.Authorization?.ToString(),
                request.Content?.Headers.ContentType?.MediaType,
                content));

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body),
            };
        }
    }

    private sealed record RecordedRequest(
        HttpMethod Method,
        Uri? Uri,
        string? Authorization,
        string? ContentType,
        string? Body);
}
