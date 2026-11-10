import type { Transport } from '../http.js';
import type { IdentityEvent } from '../types.js';

/** Builds `GET /api/v1/events`; the endpoint returns a plain array ordered by `occurredAt`. */
export class EventsListBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly accountId: string | undefined = undefined,
    private readonly after: string | undefined = undefined,
    private readonly eventLimit: number | undefined = undefined,
  ) {}

  forAccount(accountId: string): EventsListBuilder {
    return new EventsListBuilder(this.transport, accountId, this.after, this.eventLimit);
  }

  since(after: Date | string): EventsListBuilder {
    return new EventsListBuilder(
      this.transport,
      this.accountId,
      typeof after === 'string' ? after : after.toISOString(),
      this.eventLimit,
    );
  }

  limit(limit: number): EventsListBuilder {
    return new EventsListBuilder(this.transport, this.accountId, this.after, limit);
  }

  send(): Promise<IdentityEvent[]> {
    return this.transport.request<IdentityEvent[]>({
      method: 'GET',
      path: 'events',
      query: [
        ['accountId', this.accountId],
        ['after', this.after],
        ['limit', this.eventLimit === undefined ? undefined : String(this.eventLimit)],
      ],
    });
  }
}

/** Entry point for the identity domain event stream. */
export class EventsResource {
  constructor(private readonly transport: Transport) {}

  list(): EventsListBuilder {
    return new EventsListBuilder(this.transport);
  }
}
