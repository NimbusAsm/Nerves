# Nerves SDKs

Three hand-written, chainable clients for the Nerves management API (`/api/v1`).
They deliberately share one shape:

```text
entry -> verb -> modifier -> terminal
nerves.<resource>.<verb>().<modifier>(...).send()
```

| Package | Directory | Runtime | Package name |
| --- | --- | --- | --- |
| .NET client | `dotnet/Nerves.Client` | .NET 10 | `Nerves.Client` (NuGet) |
| TypeScript client | `typescript` | Node 20+ / browsers | `@nerves/sdk` (npm) |
| Python client | `python` | Python 3.10+ | `nerves-sdk` (PyPI) |

```csharp
// .NET
var account = await nerves.Accounts.Create().ForEntity(entityId)
    .WithUsername("alice").WithRoles("member").SendAsync(ct);
```

```ts
// TypeScript
const account = await nerves.accounts.create().forEntity(entityId)
  .withUsername('alice').withRoles('member').send();
```

```python
# Python
with nerves.client(base_url, token) as c:
    account = (c.accounts.create().for_entity(entity_id)
        .with_username("alice").with_roles("member").send())
```

Every builder is immutable: each modifier returns a new builder, so a partially
configured request can be stored, forked, and reused concurrently.

## Method mapping

The same concept is spelled in the casing the language expects, nothing else changes.

| Concept | .NET | TypeScript | Python |
| --- | --- | --- | --- |
| Entry point | `new NervesClient(options)` | `new NervesClient(options)` / `createClient(options)` | `nerves.client(base_url, token)` / `NervesClient(...)` |
| Accounts resource | `nerves.Accounts` | `nerves.accounts` | `nerves.accounts` |
| Legal entities resource | `nerves.LegalEntities` | `nerves.legalEntities` | `nerves.legal_entities` |
| Roles resource | `nerves.Roles` | `nerves.roles` | `nerves.roles` |
| Create | `.Create()` | `.create()` | `.create()` |
| Get one | `.Get(id)` | `.get(id)` | `.get(id)` |
| List | `.List()` | `.list()` | `.list()` |
| Update (PATCH) | `.Update(id)` | `.update(id)` | `.update(id)` |
| Delete | `.Delete(id)` | `.delete(id)` | `.delete(id)` |
| Scope to a legal entity | `.ForEntity(id)` | `.forEntity(id)` | `.for_entity(id)` |
| Scope to a realm | `.InRealm(name)` | `.inRealm(name)` | `.in_realm(name)` |
| Username field | `.WithUsername(value)` | `.withUsername(value)` | `.with_username(value)` |
| Email field | `.WithEmail(value)` | `.withEmail(value)` | `.with_email(value)` |
| Phone field | `.WithPhoneNumber(value)` | `.withPhoneNumber(value)` | `.with_phone_number(value)` |
| Display-name field | `.WithDisplayName(value)` | `.withDisplayName(value)` | `.with_display_name(value)` |
| Account type | `.WithType(AccountType.Service)` | `.withType('service')` | `.with_type(AccountType.SERVICE)` |
| Roles field | `.WithRoles("a", "b")` | `.withRoles('a', 'b')` | `.with_roles("a", "b")` |
| Primary flag | `.WithPrimary()` | `.withPrimary()` | `.with_primary()` |
| Document (entity) | `.WithDocument(type, number, country)` | `.withDocument(type, number, country)` | `.with_document(type, number, country)` |
| Permissions (role) | `.WithPermissions("a", "b")` | `.withPermissions('a', 'b')` | `.with_permissions("a", "b")` |
| Generic filter | `.Filter("status", "active")` | `.filter('status', 'active')` | `.filter("status", "active")` |
| Typed status filter | `.WithStatus(AccountStatus.Active)` | `.withStatus('active')` | `.with_status(AccountStatus.ACTIVE)` |
| Sort | `.Sort("-createdAt")` | `.sort('-createdAt')` | `.sort("-createdAt")` |
| Page size | `.Limit(50)` | `.limit(50)` | `.limit(50)` |
| Continue cursor | `.Cursor(cursor)` | `.cursor(cursor)` | `.cursor(cursor)` |
| Optimistic concurrency | `.IfMatch(etag)` | `.ifMatch(etag)` | `.if_match(etag)` |
| Custom idempotency key | `.WithIdempotencyKey(key)` | `.withIdempotencyKey(key)` | `.with_idempotency_key(key)` |
| Terminal | `.SendAsync(ct)` | `.send()` | `.send()` |
| Auto-paging iterator | `.StreamAsync(ct)` | `.stream()` (also `for await ... of builder`) | `for item in builder` |
| Page type | `NervesPage<T>` | `Page<T>` | `Page[T]` |
| Page fields | `.Items`, `.NextCursor`, `.HasMore` | `.items`, `.nextCursor`, `.hasMore` | `.items`, `.next_cursor`, `.has_more` |
| Error type | `NervesApiException` | `NervesApiError` | `NervesApiException` |
| Error fields | `.Status`, `.Type`, `.TraceId` | `.status`, `.type`, `.traceId` | `.status`, `.type`, `.trace_id` |
| Problem document | `NervesProblemDetails` | `ProblemDetails` | `ProblemDetails` |

## Wire contract

All three clients target the same JSON contract, which follows PLAN section 9.2.

| Topic | Rule |
| --- | --- |
| Base path | `{baseUrl}/api/v1` |
| Content type | `application/json`; errors are RFC 9457 `application/problem+json` |
| Authentication | `Authorization: Bearer <token>` when a token is supplied |
| Enum values | lower camel case strings: `personal`, `service`, `managed`; `active`, `locked`, `disabled`; `naturalPerson`, `legalPerson`, `government`, `other`; `pending`, `verified`, `rejected`, `expired`; `residentIdentityCard`, `passport`, `unifiedSocialCreditCode`, `other` |
| Timestamps | ISO-8601 with offset; the .NET client uses `DateTimeOffset`, TypeScript uses ISO strings, Python uses `datetime` |
| List request | `?limit=&cursor=&filter[field]=value&sort=-createdAt`; `limit` and `cursor` are opaque/validated |
| List response | `{"items": [...], "nextCursor": "opaque" | null}` |
| Create | `POST /resources` with a fresh `Idempotency-Key` unless overridden |
| Update | `PATCH /resources/{id}`; only explicitly set fields are sent |
| Delete | `DELETE /resources/{id}`; success may be `204 No Content` |
| Concurrency | `If-Match` is sent when `IfMatch(...)` / `ifMatch(...)` / `if_match(...)` is called |
| Retries | 429, 5xx, and connection failures only; exponential backoff plus jitter; the idempotency key is reused across attempts |
| Timeouts | .NET: `HttpClient.Timeout` (30 s by default when the SDK owns the client). TypeScript: 30 s per attempt. Python: 5 s connect / 30 s read. All configurable. |

Pagination is real cursor pagination. The clients never synthesize offsets and
never silently walk a list; `send()` returns exactly one page, while the stream
iterators follow `nextCursor` until it is null (guarding against a server that
repeats a cursor forever).

## What is covered

Each resource exposes create, get, list, update, and delete:

| Resource | HTTP collection | Notes |
| --- | --- | --- |
| Accounts | `/api/v1/accounts` | `forEntity` / `inRealm`; service accounts may omit the entity |
| Legal entities | `/api/v1/legal-entities` | document number is write-only; responses expose `realName.documentType` and `country` only |
| Roles | `/api/v1/roles` | permission codes as strings |

## Build and test

```bash
# .NET (from the repository root)
export PATH="$HOME/.dotnet:$PATH"
dotnet build sdk/dotnet/Nerves.Client/Nerves.Client.csproj -warnaserror
dotnet test  sdk/dotnet/Nerves.Client.Tests/Nerves.Client.Tests.csproj

# TypeScript
cd sdk/typescript
pnpm install
pnpm build
pnpm typecheck
pnpm test

# Python
cd sdk/python
python -m venv .venv
.venv/bin/pip install -e '.[dev]'
.venv/bin/pytest
```

The .NET projects are not referenced from `Nerves.slnx`; the solution owner mounts
them. All three SDKs are independent of the service implementation and only need
the management API described above.
