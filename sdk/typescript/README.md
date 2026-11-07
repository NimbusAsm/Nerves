# @nerves/sdk (TypeScript)

Chainable TypeScript client for the Nerves management API. No runtime
dependencies; uses the global `fetch` and is ESM-first.

```bash
pnpm add @nerves/sdk
```

```ts
import { NervesClient } from '@nerves/sdk';

const nerves = new NervesClient({
  baseUrl: 'https://accounts.example.com',
  token: process.env.NERVES_TOKEN,
});

const account = await nerves.accounts
  .create()
  .forEntity(legalEntityId)
  .inRealm('default')
  .withUsername('alice')
  .withEmail('alice@example.com')
  .withRoles('member')
  .send();

const page = await nerves.accounts
  .list()
  .forEntity(legalEntityId)
  .withStatus('active')
  .filter('username', 'alice')
  .sort('-createdAt')
  .limit(50)
  .send();

for await (const item of nerves.accounts.list().forEntity(legalEntityId)) {
  console.log(item.username);
}
```

Every modifier returns a new builder (frozen state), so a partially built request
can be reused. Fetch can be injected for tracing, a proxy, or tests:

```ts
const nerves = new NervesClient({
  baseUrl: 'https://accounts.example.com',
  token,
  fetch: myInstrumentedFetch,
  timeoutMs: 30_000,
  maxRetries: 2,
});
```

Non-success responses throw `NervesApiError` with `status`, `type`, `traceId`,
and the parsed `problem`. Creates send a fresh `Idempotency-Key`; updates and
deletes accept `.ifMatch(etag)`.

## Development

```bash
pnpm install
pnpm build
pnpm typecheck
pnpm test
```

Tests use Vitest and a queued `fetch` double.
