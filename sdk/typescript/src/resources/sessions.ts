import type { RequestSpec, Transport } from '../http.js';
import type { SessionInfo } from '../types.js';

/** Builds `GET /api/v1/accounts/{accountId}/sessions`. */
export class SessionsListBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly accountId: string,
  ) {}

  send(): Promise<SessionInfo[]> {
    return this.transport.request<SessionInfo[]>({
      method: 'GET',
      path: `accounts/${this.accountId}/sessions`,
    });
  }
}

/** Builds `DELETE /api/v1/sessions/{sessionId}`. */
export class SessionsRevokeBuilder {
  constructor(
    private readonly transport: Transport,
    private readonly sessionId: string,
  ) {}

  send(): Promise<void> {
    return this.transport.requestVoid({ method: 'DELETE', path: `sessions/${this.sessionId}` });
  }
}

/** Entry point for session inspection and revocation. */
export class SessionsResource {
  constructor(private readonly transport: Transport) {}

  list(accountId: string): SessionsListBuilder {
    return new SessionsListBuilder(this.transport, accountId);
  }

  revoke(sessionId: string): SessionsRevokeBuilder {
    return new SessionsRevokeBuilder(this.transport, sessionId);
  }
}
