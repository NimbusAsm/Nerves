using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nerves.Core;
using Nerves.Notification.Consumption;
using Nerves.Notification.Domain;
using Nerves.Notification.Options;
using Nerves.Notification.Persistence;
using Shouldly;

namespace Nerves.UnitTests;

public sealed class NotificationEventConsumerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static EventConsumer Create(FakeEventClient client, FakeStore store, int lookbackSeconds = 2)
    {
        var options = Options.Create(new EventConsumerOptions
        {
            Enabled = true,
            IdentityBaseUrl = "http://127.0.0.1:5102",
            BearerToken = "test-token",
            LookbackSeconds = lookbackSeconds,
            Issuer = "https://nerves.example.com/",
        });

        return new EventConsumer(client, store, options, new FixedClock(Now), NullLogger<EventConsumer>.Instance);
    }

    [Fact]
    public async Task A_known_event_queues_one_email_and_moves_the_cursor()
    {
        var accountId = Guid.CreateVersion7(Now);
        var client = new FakeEventClient();
        client.Accounts[accountId] = new AccountSnapshot(accountId, "user@example.com", "张伟", "zhang.wei");
        var envelope = new EventEnvelope(
            Guid.CreateVersion7(Now),
            EventTemplateMapper.AccountCreated,
            accountId,
            null,
            "{}",
            Now);
        client.Events.Add(envelope);

        var store = new FakeStore();
        var consumer = Create(client, store);

        var created = await consumer.ConsumeOnceAsync(CancellationToken.None);

        created.ShouldBe(1);
        store.Messages.Count.ShouldBe(1);
        store.Messages[0].Template.ShouldBe("account.welcome");
        store.Messages[0].Channel.ShouldBe(NotificationChannel.Email);
        store.Messages[0].SourceEventId.ShouldBe(envelope.Id);
        store.Messages[0].To.ShouldBe("user@example.com");
        store.Cursor!.LastEventId.ShouldBe(envelope.Id);
        store.Saves.ShouldBe(1);
    }

    [Fact]
    public async Task Replaying_the_same_event_is_idempotent()
    {
        var accountId = Guid.CreateVersion7(Now);
        var client = new FakeEventClient();
        client.Accounts[accountId] = new AccountSnapshot(accountId, "user@example.com", "张伟", "zhang.wei");
        var envelope = new EventEnvelope(
            Guid.CreateVersion7(Now),
            EventTemplateMapper.AccountCreated,
            accountId,
            null,
            "{}",
            Now);
        client.Events.Add(envelope);

        var store = new FakeStore();
        var consumer = Create(client, store);
        var first = await consumer.ConsumeOnceAsync(CancellationToken.None);
        var second = await consumer.ConsumeOnceAsync(CancellationToken.None);

        first.ShouldBe(1);
        second.ShouldBe(0);
        store.Messages.Count.ShouldBe(1);
    }

    [Fact]
    public async Task An_unknown_event_type_is_skipped_but_the_cursor_still_moves()
    {
        var client = new FakeEventClient();
        var envelope = new EventEnvelope(
            Guid.CreateVersion7(Now),
            "account.deleted-by-aliens",
            null,
            null,
            "{}",
            Now);
        client.Events.Add(envelope);

        var store = new FakeStore();
        var created = await Create(client, store).ConsumeOnceAsync(CancellationToken.None);

        created.ShouldBe(0);
        store.Messages.ShouldBeEmpty();
        store.Cursor!.LastEventId.ShouldBe(envelope.Id);
    }

    [Fact]
    public async Task An_account_without_an_email_is_skipped_without_stopping_the_batch()
    {
        var withoutEmail = Guid.CreateVersion7(Now);
        var withEmail = Guid.CreateVersion7(Now);
        var client = new FakeEventClient();
        client.Accounts[withoutEmail] = new AccountSnapshot(withoutEmail, null, "无邮箱", "no.mail");
        client.Accounts[withEmail] = new AccountSnapshot(withEmail, "second@example.com", "李四", "li.si");

        client.Events.Add(new EventEnvelope(
            Guid.CreateVersion7(Now), EventTemplateMapper.AccountCreated, withoutEmail, null, "{}", Now));
        client.Events.Add(new EventEnvelope(
            Guid.CreateVersion7(Now), EventTemplateMapper.AccountCreated, withEmail, null, "{}", Now.AddSeconds(1)));

        var store = new FakeStore();
        var created = await Create(client, store).ConsumeOnceAsync(CancellationToken.None);

        created.ShouldBe(1);
        store.Messages.Count.ShouldBe(1);
        store.Messages[0].To.ShouldBe("second@example.com");
    }

    [Fact]
    public async Task A_transport_failure_does_not_move_the_cursor()
    {
        var client = new FakeEventClient { Failure = new HttpRequestException("identity is down") };
        var store = new FakeStore();

        await Should.ThrowAsync<HttpRequestException>(
            () => Create(client, store).ConsumeOnceAsync(CancellationToken.None));

        store.Cursor.ShouldBeNull();
        store.Messages.ShouldBeEmpty();
        store.Saves.ShouldBe(0);
    }

    [Fact]
    public async Task The_lookback_overlap_is_subtracted_from_the_stored_cursor()
    {
        var client = new FakeEventClient();
        var store = new FakeStore();
        store.Cursor = new EventCursor(Now.AddMinutes(-10), Guid.CreateVersion7(Now));

        await Create(client, store, lookbackSeconds: 3).ConsumeOnceAsync(CancellationToken.None);

        client.LastAfter.ShouldBe(Now.AddMinutes(-10).AddSeconds(-3));
    }

    [Fact]
    public async Task A_real_name_change_uses_the_real_name_template()
    {
        var accountId = Guid.CreateVersion7(Now);
        var client = new FakeEventClient();
        client.Accounts[accountId] = new AccountSnapshot(accountId, "user@example.com", "张伟", "zhang.wei");
        client.Events.Add(new EventEnvelope(
            Guid.CreateVersion7(Now),
            EventTemplateMapper.RealnameChanged,
            accountId,
            Guid.CreateVersion7(Now),
            "{\"state\":\"Rejected\"}",
            Now));

        var store = new FakeStore();
        await Create(client, store).ConsumeOnceAsync(CancellationToken.None);

        store.Messages.Count.ShouldBe(1);
        store.Messages[0].Template.ShouldBe("account.realname-changed");
        store.Messages[0].Body.ShouldContain("Rejected");
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class FakeEventClient : IIdentityEventClient
    {
        public List<EventEnvelope> Events { get; } = [];

        public Dictionary<Guid, AccountSnapshot> Accounts { get; } = [];

        public Exception? Failure { get; set; }

        public DateTimeOffset? LastAfter { get; private set; }

        public Task<IReadOnlyList<EventEnvelope>> ListEventsAsync(
            DateTimeOffset? after,
            int limit,
            CancellationToken cancellationToken)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            LastAfter = after;
            IReadOnlyList<EventEnvelope> batch = Events.Take(limit).ToList();
            return Task.FromResult(batch);
        }

        public Task<AccountSnapshot?> GetAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult(Accounts.TryGetValue(accountId, out var account) ? account : null);
    }

    private sealed class FakeStore : INotificationStore
    {
        public List<NotificationMessage> Messages { get; } = [];

        public EventCursor? Cursor { get; set; }

        public int Saves { get; private set; }

        public Task<EventCursor?> GetEventCursorAsync(string consumer, CancellationToken cancellationToken) =>
            Task.FromResult(Cursor);

        public Task<bool> HasMessageForEventAsync(Guid sourceEventId, CancellationToken cancellationToken) =>
            Task.FromResult(Messages.Exists(message => message.SourceEventId == sourceEventId));

        public void AddMessage(NotificationMessage message) => Messages.Add(message);

        public void SetEventCursor(string consumer, EventCursor cursor, DateTimeOffset now) => Cursor = cursor;

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saves += 1;
            return Task.CompletedTask;
        }
    }
}
