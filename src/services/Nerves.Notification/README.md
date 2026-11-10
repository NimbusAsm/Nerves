# Nerves.Notification

Queues templated notifications and records every delivery attempt.

## Channels

| Channel | Transport | Configuration |
| --- | --- | --- |
| `email` | SMTP, or a log-only sender in development | `Smtp:*` |
| `sms` | HTTP webhook or a Twilio-compatible API | `Sms:*` |

Both transports are **opt-in and disabled by default**: `Smtp:Disabled=true` and
`Sms:Disabled=true` wire the no-op senders, so a fresh checkout needs no mail
server and no gateway.

```jsonc
{
  "Sms": {
    "Disabled": false,
    "Provider": "webhook",          // webhook | twilio
    "WebhookUrl": "https://sms.internal.example/send",
    "WebhookToken": "<bearer token, optional>",
    "TimeoutSeconds": 30
  }
}
```

For the `twilio` provider set `AccountSid`, `AuthToken` and `From`;
`TwilioBaseUrl` defaults to `https://api.twilio.com` and exists so a compatible
gateway (or a test stub) can be used instead.

## API

All routes require the `nerves:admin` permission.

| Method | Route | Purpose |
| --- | --- | --- |
| `POST` | `/api/v1/notifications/email` | Queue templated email (kept for compatibility) |
| `POST` | `/api/v1/notifications/send` | Queue email or SMS: `{ "to", "template", "channel", "values" }` |
| `GET` | `/api/v1/notifications` | Delivery log; filters: `status`, `channel`, `to`, `cursor` |
| `GET` | `/api/v1/notifications/{id}` | One delivery-log row |

The response includes `channel` and, for messages queued by the event consumer,
`sourceEventId`.

## Identity event consumer (B10)

The consumer polls `GET /api/v1/events` on the identity service and turns domain
events into templated emails:

| Event | Template |
| --- | --- |
| `account.created` | `account.welcome` |
| `realname.changed` | `account.realname-changed` |

Other event types are skipped and counted in the log; they still advance the
cursor.

```jsonc
{
  "Events": {
    "Consumer": {
      "Enabled": true,
      "IdentityBaseUrl": "http://127.0.0.1:5102",
      "BearerToken": "<admin token>",     // or a ClientCredentials block below
      "ClientCredentials": {
        "TokenEndpoint": "http://127.0.0.1:5101/connect/token",
        "ClientId": "nerves-notification",
        "ClientSecret": "<secret>",
        "Scope": "nerves.api"
      },
      "PollSeconds": 30,
      "BatchSize": 50,
      "LookbackSeconds": 2,
      "Issuer": "https://nerves.example.com/"
    }
  }
}
```

Delivery guarantees:

* the cursor is stored in `notification.event_consumer_state`, so a restart
  resumes where the previous process stopped;
* `notification_message.SourceEventId` is **unique**, so replaying an event
  (lookback overlap or a crash between insert and commit) cannot queue the same
  notification twice;
* a transport error leaves the cursor untouched and the same batch is retried on
  the next poll.
