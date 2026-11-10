import type { QueryParam, Transport } from '../http.js';
import type { NotificationMessage, Page } from '../types.js';

/** Builds `POST /api/v1/notifications/email`. */
export class NotificationsSendBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly recipient: string | undefined = undefined,
    private readonly template: string | undefined = undefined,
    private readonly values: Readonly<Record<string, string>> = {},
    private readonly idempotencyKey: string | undefined = undefined,
  ) {}

  to(to: string): NotificationsSendBuilder {
    return new NotificationsSendBuilder(this.transport, to, this.template, this.values, this.idempotencyKey);
  }

  withTemplate(template: string): NotificationsSendBuilder {
    return new NotificationsSendBuilder(this.transport, this.recipient, template, this.values, this.idempotencyKey);
  }

  withValue(key: string, value: string): NotificationsSendBuilder {
    return new NotificationsSendBuilder(
      this.transport,
      this.recipient,
      this.template,
      { ...this.values, [key]: value },
      this.idempotencyKey,
    );
  }

  withValues(values: Readonly<Record<string, string>>): NotificationsSendBuilder {
    return new NotificationsSendBuilder(this.transport, this.recipient, this.template, { ...values }, this.idempotencyKey);
  }

  withIdempotencyKey(idempotencyKey: string): NotificationsSendBuilder {
    return new NotificationsSendBuilder(this.transport, this.recipient, this.template, this.values, idempotencyKey);
  }

  send(): Promise<NotificationMessage> {
    if (this.recipient === undefined || this.recipient.trim().length === 0) {
      throw new Error('to() is required before send().');
    }
    if (this.template === undefined || this.template.trim().length === 0) {
      throw new Error('withTemplate() is required before send().');
    }

    const body: Record<string, unknown> = { to: this.recipient, template: this.template };
    if (Object.keys(this.values).length > 0) {
      body.values = this.values;
    }

    return this.transport.request<NotificationMessage>({
      method: 'POST',
      path: 'notifications/email',
      body,
      idempotencyKey: this.idempotencyKey ?? crypto.randomUUID(),
    });
  }
}

/** Builds \`GET /api/v1/notifications\` with cursor pagination. */
export class NotificationsListBuilder implements AsyncIterable<NotificationMessage> {
  constructor(
    private readonly transport: Transport,
    private readonly deliveryStatus: string | undefined = undefined,
    private readonly recipient: string | undefined = undefined,
    private readonly pageSize: number | undefined = undefined,
    private readonly cursorToken: string | undefined = undefined,
  ) {}

  withStatus(status: string): NotificationsListBuilder {
    return new NotificationsListBuilder(this.transport, status, this.recipient, this.pageSize, this.cursorToken);
  }

  to(to: string): NotificationsListBuilder {
    return new NotificationsListBuilder(this.transport, this.deliveryStatus, to, this.pageSize, this.cursorToken);
  }

  limit(pageSize: number): NotificationsListBuilder {
    if (!Number.isInteger(pageSize) || pageSize <= 0) {
      throw new RangeError('page size must be a positive integer');
    }
    return new NotificationsListBuilder(this.transport, this.deliveryStatus, this.recipient, pageSize, this.cursorToken);
  }

  cursor(cursor: string): NotificationsListBuilder {
    if (cursor.length === 0) {
      throw new RangeError('cursor must not be empty');
    }
    return new NotificationsListBuilder(this.transport, this.deliveryStatus, this.recipient, this.pageSize, cursor);
  }

  send(): Promise<Page<NotificationMessage>> {
    return this.transport.requestPage<NotificationMessage>({
      method: 'GET',
      path: 'notifications',
      query: this.params(),
    });
  }

  async *stream(): AsyncGenerator<NotificationMessage, void, undefined> {
    let cursor = this.cursorToken;
    const seen = new Set<string>();

    for (;;) {
      const page = await new NotificationsListBuilder(
        this.transport,
        this.deliveryStatus,
        this.recipient,
        this.pageSize,
        cursor,
      ).send();
      yield* page.items;

      const next = page.nextCursor;
      if (next === null || next.length === 0 || seen.has(next)) {
        return;
      }
      seen.add(next);
      cursor = next;
    }
  }

  [Symbol.asyncIterator](): AsyncIterator<NotificationMessage> {
    return this.stream();
  }

  private params(): QueryParam[] {
    return [
      ['status', this.deliveryStatus],
      ['to', this.recipient],
      ['limit', this.pageSize === undefined ? undefined : String(this.pageSize)],
      ['cursor', this.cursorToken],
    ];
  }
}

/** Builds `GET /api/v1/notifications/{id}`. */
export class NotificationsGetBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly id: string,
  ) {}

  send(): Promise<NotificationMessage> {
    return this.transport.request<NotificationMessage>({ method: 'GET', path: `notifications/${this.id}` });
  }
}

/** Entry point for the notification delivery log. */
export class NotificationsResource {
  constructor(private readonly transport: Transport) {}

  sendEmail(): NotificationsSendBuilder {
    return new NotificationsSendBuilder(this.transport);
  }

  list(): NotificationsListBuilder {
    return new NotificationsListBuilder(this.transport);
  }

  get(id: string): NotificationsGetBuilder {
    return new NotificationsGetBuilder(this.transport, id);
  }
}