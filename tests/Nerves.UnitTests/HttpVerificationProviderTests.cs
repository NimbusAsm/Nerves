using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nerves.Identity.Verification;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class HttpVerificationProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_processing_reply_keeps_the_verification_asynchronous()
    {
        var provider = Build(new StubHandler(HttpStatusCode.OK,
            """{"outcome":"processing","reference":"ref-9"}"""));

        var response = await provider.StartAsync(Request(), TestContext.Current.CancellationToken);

        response.Outcome.ShouldBe(VerificationProviderOutcome.Processing);
        response.Reference.ShouldBe("ref-9");
    }

    [Fact]
    public async Task Provider_verdicts_are_mapped_to_the_domain_outcome()
    {
        var verified = await Build(new StubHandler(HttpStatusCode.OK, """{"outcome":"approved"}"""))
            .StartAsync(Request(), TestContext.Current.CancellationToken);
        var rejected = await Build(new StubHandler(HttpStatusCode.OK, """{"outcome":"declined"}"""))
            .StartAsync(Request(), TestContext.Current.CancellationToken);

        verified.Outcome.ShouldBe(VerificationProviderOutcome.Verified);
        rejected.Outcome.ShouldBe(VerificationProviderOutcome.Rejected);
    }

    [Fact]
    public async Task An_error_status_becomes_a_failed_outcome_and_keeps_the_entity_pending()
    {
        var response = await Build(new StubHandler(HttpStatusCode.BadGateway, "nope"))
            .StartAsync(Request(), TestContext.Current.CancellationToken);

        response.Outcome.ShouldBe(VerificationProviderOutcome.Failed);
        response.Reference.ShouldBeNull();
    }

    [Fact]
    public async Task An_unreachable_provider_does_not_throw()
    {
        var response = await Build(new StubHandler(new HttpRequestException("offline")))
            .StartAsync(Request(), TestContext.Current.CancellationToken);

        response.Outcome.ShouldBe(VerificationProviderOutcome.Failed);
    }

    [Fact]
    public async Task A_missing_base_address_is_reported_without_network_calls()
    {
        var provider = new HttpVerificationProvider(
            new StubFactory(new StubHandler(HttpStatusCode.OK, "{}")),
            Options.Create(new HttpVerificationOptions()),
            NullLogger<HttpVerificationProvider>.Instance);

        var response = await provider.StartAsync(Request(), TestContext.Current.CancellationToken);

        response.Outcome.ShouldBe(VerificationProviderOutcome.Failed);
        response.Message.ShouldNotBeNull().ShouldContain("BaseAddress");
    }

    private static HttpVerificationProvider Build(StubHandler handler) =>
        new(new StubFactory(handler),
            Options.Create(new HttpVerificationOptions { BaseAddress = "https://provider.test" }),
            NullLogger<HttpVerificationProvider>.Instance);

    private static VerificationProviderRequest Request() =>
        new(Guid.NewGuid(), "LegalPerson", "UnifiedSocialCreditCode", new string('b', 64), "CN",
            "Acme", "https://nerves.test/callback", Now);

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        private readonly Exception? _exception;

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public StubHandler(Exception exception)
        {
            _exception = exception;
            _status = HttpStatusCode.OK;
            _body = string.Empty;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            _exception is not null
                ? Task.FromException<HttpResponseMessage>(_exception)
                : Task.FromResult(new HttpResponseMessage(_status)
                {
                    Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json"),
                });
    }
}
