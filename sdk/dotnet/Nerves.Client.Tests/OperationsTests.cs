using System.Net;
using System.Text.Json;
using Shouldly;

namespace Nerves.Client.Tests;

public sealed class AuditTests
{
    private const string AuditJson =
        "{\"id\":\"0199a0c0-0000-7000-8000-000000000060\",\"stream\":\"identity\",\"sequence\":1,\"occurredAt\":\"2026-01-01T00:00:00Z\",\"actorType\":\"admin\",\"actorId\":\"admin\",\"action\":\"account.created\",\"targetType\":\"account\",\"targetId\":\"x\",\"result\":\"success\",\"traceId\":\"t\",\"hash\":\"h\"}";

    [Fact]
    public async Task List_sends_filters_and_reads_the_cursor_page()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.OK, "{\"items\":[" + AuditJson + "],\"nextCursor\":\"c1\"}");

        var page = await client.Audit.List()
            .InStream("identity")
            .WithAction("account.created")
            .Limit(10)
            .SendAsync();

        page.Items.Single().Action.ShouldBe("account.created");
        page.NextCursor.ShouldBe("c1");

        var query = Uri.UnescapeDataString(handler.Requests.Single().Uri.Query);
        query.ShouldContain("stream=identity");
        query.ShouldContain("action=account.created");
        query.ShouldContain("limit=10");
    }

    [Fact]
    public async Task Stream_follows_cursors()
    {
        var (client, handler) = TestClient.Create();
        using var clientScope = client;
        handler
            .Respond(HttpStatusCode.OK, "{\"items\":[" + AuditJson + "],\"nextCursor\":\"c2\"}")
            .Respond(HttpStatusCode.OK, "{\"items\":[" + AuditJson + "],\"nextCursor\":null}");

        var count = 0;
        await foreach (var item in client.Audit.List().StreamAsync())
        {
            item.Action.ShouldBe("account.created");
            count++;
        }

        _ = clientScope;

        count.ShouldBe(2);
        Uri.UnescapeDataString(handler.Requests[1].Uri.Query).ShouldContain("cursor=c2");
    }

    [Fact]
    public async Task Verify_reports_the_broken_event()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.OK,
            "{\"valid\":false,\"brokenAt\":\"0199a0c0-0000-7000-8000-000000000060\",\"reason\":\"hash_mismatch\"}");

        var result = await client.Audit.Verify().InStream("identity").SendAsync();

        result.Valid.ShouldBeFalse();
        result.Reason.ShouldBe("hash_mismatch");
        Uri.UnescapeDataString(handler.Requests.Single().Uri.Query).ShouldContain("stream=identity");
    }

    [Fact]
    public async Task Export_downloads_csv_with_the_server_file_name()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.RespondWith(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("id,stream\n1,identity\n", System.Text.Encoding.UTF8, "text/csv"),
            };
            response.Content.Headers.ContentDisposition =
                new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                {
                    FileNameStar = "nerves-audit-20260101-000000.csv",
                };
            return response;
        });

        var file = await client.Audit.Export().As("csv").Limit(10).SendAsync();

        file.ContentType.ShouldBe("text/csv");
        file.FileName.ShouldBe("nerves-audit-20260101-000000.csv");
        System.Text.Encoding.UTF8.GetString(file.Content.Span).ShouldContain("id,stream");
        Uri.UnescapeDataString(handler.Requests.Single().Uri.Query).ShouldContain("format=csv");
    }
}

public sealed class ComplianceTests
{
    private static readonly Guid ConsentId = Guid.Parse("0199a0c0-0000-7000-8000-000000000070");

    private const string ConsentJson =
        "{\"id\":\"0199a0c0-0000-7000-8000-000000000070\",\"subjectId\":\"alice\",\"purpose\":\"marketing\",\"version\":\"v1\",\"grantedAt\":\"2026-01-01T00:00:00Z\",\"expiresAt\":null,\"revokedAt\":null,\"revocationReason\":null}";

    [Fact]
    public async Task Consents_are_listed_granted_and_revoked()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler
            .Respond(HttpStatusCode.OK, "[" + ConsentJson + "]")
            .Respond(HttpStatusCode.Created, ConsentJson)
            .Respond(HttpStatusCode.OK, ConsentJson);

        var consents = await client.Compliance.Consents().ForSubject("alice").SendAsync();
        var granted = await client.Compliance.GrantConsent()
            .ForSubject("alice").WithPurpose("marketing").WithVersion("v1").SendAsync();
        await client.Compliance.RevokeConsent(ConsentId).WithReason("withdrawn").SendAsync();

        consents.Single().Purpose.ShouldBe("marketing");
        granted.Version.ShouldBe("v1");

        var query = Uri.UnescapeDataString(handler.Requests[0].Uri.Query);
        query.ShouldContain("subjectId=alice");

        using var body = Json.Parse(handler.Requests[1].Body);
        body.RootElement.GetProperty("subjectId").GetString().ShouldBe("alice");
        body.RootElement.GetProperty("purpose").GetString().ShouldBe("marketing");
        body.RootElement.GetProperty("version").GetString().ShouldBe("v1");

        using var revoke = Json.Parse(handler.Requests[2].Body);
        revoke.RootElement.GetProperty("reason").GetString().ShouldBe("withdrawn");
        handler.Requests[2].Uri.AbsolutePath.ShouldBe($"/api/v1/compliance/consents/{ConsentId:D}/revoke");
    }

    [Fact]
    public void GrantConsent_requires_its_three_fields()
    {
        var (client, _) = TestClient.Create();
        using var __ = client;

        Should.Throw<InvalidOperationException>(() => client.Compliance.GrantConsent().WithPurpose("p").WithVersion("v").SendAsync());
        Should.Throw<InvalidOperationException>(() => client.Compliance.GrantConsent().ForSubject("s").WithVersion("v").SendAsync());
        Should.Throw<InvalidOperationException>(() => client.Compliance.GrantConsent().ForSubject("s").WithPurpose("p").SendAsync());
    }

    [Fact]
    public async Task Retention_policies_are_listed_and_upserted_with_put()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler
            .Respond(HttpStatusCode.OK,
                "[{\"id\":\"0199a0c0-0000-7000-8000-000000000071\",\"category\":\"audit\",\"retentionDays\":180,\"legalBasis\":\"CSL\",\"updatedAt\":\"2026-01-01T00:00:00Z\"}]")
            .Respond(HttpStatusCode.OK,
                "{\"id\":\"0199a0c0-0000-7000-8000-000000000071\",\"category\":\"audit\",\"retentionDays\":365,\"legalBasis\":\"CSL\"}");

        var policies = await client.Compliance.RetentionPolicies().SendAsync();
        var updated = await client.Compliance.UpsertRetentionPolicy("audit")
            .WithRetention(365).WithLegalBasis("CSL").SendAsync();

        policies.Single().RetentionDays.ShouldBe(180);
        updated.RetentionDays.ShouldBe(365);

        var request = handler.Requests[1];
        request.Method.ShouldBe(HttpMethod.Put);
        request.Uri.AbsolutePath.ShouldBe("/api/v1/compliance/retention-policies/audit");
        using var body = Json.Parse(request.Body);
        body.RootElement.GetProperty("retentionDays").GetInt32().ShouldBe(365);
    }

    [Fact]
    public async Task Summary_reads_the_aggregate_counts()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.OK, "{\"consents\":10,\"active\":8,\"revoked\":2,\"retentionPolicies\":3}");

        var summary = await client.Compliance.Summary().SendAsync();

        summary.Active.ShouldBe(8);
        summary.RetentionPolicies.ShouldBe(3);
    }
}

public sealed class SubjectsTests
{
    [Fact]
    public async Task Export_reads_the_subject_document()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.OK,
            "{\"subjectId\":\"alice\",\"exportedAt\":\"2026-01-01T00:00:00Z\",\"account\":null,\"consents\":[],\"retentionPolicies\":[]}");

        var export = await client.Subjects.Export("alice").SendAsync();

        export.SubjectId.ShouldBe("alice");
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe("/api/v1/compliance/subjects/alice/export");
    }

    [Fact]
    public async Task Erase_posts_the_reason_and_returns_the_request_status()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.OK,
            "{\"id\":\"0199a0c0-0000-7000-8000-000000000080\",\"subjectId\":\"alice\",\"status\":\"completed\",\"consentRevoked\":2,\"accountDisabled\":true}");

        var result = await client.Subjects.Erase("alice").WithReason("gdpr").SendAsync();

        result.Status.ShouldBe("completed");
        result.AccountDisabled.ShouldBeTrue();
        using var body = Json.Parse(handler.Requests.Single().Body);
        body.RootElement.GetProperty("reason").GetString().ShouldBe("gdpr");
    }

    [Fact]
    public async Task Erasures_lists_the_history()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.OK,
            "[{\"id\":\"0199a0c0-0000-7000-8000-000000000080\",\"status\":\"completed\",\"reason\":\"gdpr\",\"requestedAt\":\"2026-01-01T00:00:00Z\",\"completedAt\":\"2026-01-01T00:01:00Z\"}]");

        var erasures = await client.Subjects.Erasures("alice").SendAsync();

        erasures.Single().Reason.ShouldBe("gdpr");
    }
}

public sealed class NotificationsTests
{
    private const string NotificationJson =
        "{\"id\":\"0199a0c0-0000-7000-8000-000000000090\",\"to\":\"alice@example.com\",\"template\":\"welcome\",\"subject\":\"Welcome\",\"status\":\"pending\",\"attempts\":0,\"error\":null,\"createdAt\":\"2026-01-01T00:00:00Z\",\"sentAt\":null,\"nextAttemptAt\":null}";

    [Fact]
    public async Task SendEmail_posts_the_template_and_values()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.Created, NotificationJson);

        var message = await client.Notifications.SendEmail()
            .To("alice@example.com")
            .WithTemplate("welcome")
            .WithValue("name", "Alice")
            .SendAsync();

        message.Template.ShouldBe("welcome");
        var request = handler.Requests.Single();
        request.Uri.AbsolutePath.ShouldBe("/api/v1/notifications/email");
        request.Header("Idempotency-Key").ShouldNotBeNullOrWhiteSpace();

        using var body = Json.Parse(request.Body);
        body.RootElement.GetProperty("to").GetString().ShouldBe("alice@example.com");
        body.RootElement.GetProperty("template").GetString().ShouldBe("welcome");
        body.RootElement.GetProperty("values").GetProperty("name").GetString().ShouldBe("Alice");
    }

    [Fact]
    public async Task List_filters_by_status_and_recipient()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        handler.Respond(HttpStatusCode.OK, "{\"items\":[" + NotificationJson + "],\"nextCursor\":null}");

        var page = await client.Notifications.List()
            .WithStatus("pending")
            .To("alice@example.com")
            .Limit(5)
            .SendAsync();

        page.Items.Single().Status.ShouldBe("pending");
        var query = Uri.UnescapeDataString(handler.Requests.Single().Uri.Query);
        query.ShouldContain("status=pending");
        query.ShouldContain("to=alice@example.com");
        query.ShouldContain("limit=5");
    }

    [Fact]
    public async Task Get_reads_one_delivery()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        var id = Guid.Parse("0199a0c0-0000-7000-8000-000000000090");
        handler.Respond(HttpStatusCode.OK, NotificationJson);

        var message = await client.Notifications.Get(id).SendAsync();

        message.To.ShouldBe("alice@example.com");
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe($"/api/v1/notifications/{id:D}");
    }
}

public sealed class EventsTests
{
    private const string EventJson =
        "{\"id\":\"0199a0c0-0000-7000-8000-0000000000a0\",\"type\":\"account.created\",\"accountId\":\"0199a0c0-0000-7000-8000-000000000001\",\"legalEntityId\":null,\"payload\":\"{}\",\"occurredAt\":\"2026-01-01T00:00:00Z\",\"dispatchedAt\":null}";

    [Fact]
    public async Task List_sends_account_after_and_limit()
    {
        var (client, handler) = TestClient.Create();
        using var _ = client;
        var accountId = Guid.Parse("0199a0c0-0000-7000-8000-000000000001");
        var after = DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        handler.Respond(HttpStatusCode.OK, "[" + EventJson + "]");

        var events = await client.Events.List().ForAccount(accountId).Since(after).Limit(50).SendAsync();

        events.Single().Type.ShouldBe("account.created");
        var query = Uri.UnescapeDataString(handler.Requests.Single().Uri.Query);
        query.ShouldContain("accountId=" + accountId.ToString("D"));
        query.ShouldContain("limit=50");
        query.ShouldContain("after=");
    }
}
