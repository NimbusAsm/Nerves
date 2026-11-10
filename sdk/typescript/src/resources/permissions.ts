import type { RequestSpec, Transport } from '../http.js';
import type { PermissionInfo } from '../types.js';

/** Builds `GET /api/v1/permissions`. */
export class PermissionsListBuilder {
  constructor(private readonly transport: Transport) {}

  send(): Promise<PermissionInfo[]> {
    return this.transport.request<PermissionInfo[]>({ method: 'GET', path: 'permissions' });
  }
}

/** Builds `POST /api/v1/permissions`. */
export class PermissionsCreateBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly code: string | undefined = undefined,
    private readonly description: string | null | undefined = undefined,
    private readonly idempotencyKey: string | undefined = undefined,
  ) {}

  withCode(code: string): PermissionsCreateBuilder {
    return new PermissionsCreateBuilder(this.transport, code, this.description, this.idempotencyKey);
  }

  withDescription(description: string | null): PermissionsCreateBuilder {
    return new PermissionsCreateBuilder(this.transport, this.code, description, this.idempotencyKey);
  }

  withIdempotencyKey(idempotencyKey: string): PermissionsCreateBuilder {
    return new PermissionsCreateBuilder(this.transport, this.code, this.description, idempotencyKey);
  }

  send(): Promise<PermissionInfo> {
    if (this.code === undefined || this.code.trim().length === 0) {
      throw new Error('withCode() is required before send().');
    }

    const body: Record<string, unknown> = { code: this.code };
    if (this.description !== undefined) {
      body.description = this.description;
    }

    return this.transport.request<PermissionInfo>({
      method: 'POST',
      path: 'permissions',
      body,
      idempotencyKey: this.idempotencyKey ?? crypto.randomUUID(),
    });
  }
}

/** Entry point for permission codes. */
export class PermissionsResource {
  constructor(private readonly transport: Transport) {}

  list(): PermissionsListBuilder {
    return new PermissionsListBuilder(this.transport);
  }

  create(): PermissionsCreateBuilder {
    return new PermissionsCreateBuilder(this.transport);
  }
}
