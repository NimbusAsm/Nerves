import { Transport, type NervesClientOptions } from './http.js';
import { AccountsResource } from './resources/accounts.js';
import { LegalEntitiesResource } from './resources/legalEntities.js';
import { RolesResource } from './resources/roles.js';
import { PermissionsResource } from './resources/permissions.js';
import { GroupsResource } from './resources/groups.js';
import { DecisionsResource } from './resources/decisions.js';
import { SessionsResource } from './resources/sessions.js';
import { AuditResource } from './resources/audit.js';
import { ComplianceResource } from './resources/compliance.js';
import { SubjectsResource } from './resources/subjects.js';
import { NotificationsResource } from './resources/notifications.js';
import { EventsResource } from './resources/events.js';

/**
 * Entry point of the chainable client. Every operation starts here and is
 * expressed as `entry → verb → modifier → terminal`:
 *
 * ```ts
 * const account = await nerves.accounts
 *   .create()
 *   .forEntity(entityId)
 *   .withUsername('alice')
 *   .send();
 * ```
 */
export class NervesClient {
  readonly accounts: AccountsResource;
  readonly legalEntities: LegalEntitiesResource;
  readonly roles: RolesResource;
  readonly permissions: PermissionsResource;
  readonly groups: GroupsResource;
  readonly decisions: DecisionsResource;
  readonly sessions: SessionsResource;
  readonly audit: AuditResource;
  readonly compliance: ComplianceResource;
  readonly subjects: SubjectsResource;
  readonly notifications: NotificationsResource;
  readonly events: EventsResource;

  constructor(options: NervesClientOptions) {
    const transport = new Transport(options);
    this.accounts = new AccountsResource(transport);
    this.legalEntities = new LegalEntitiesResource(transport);
    this.roles = new RolesResource(transport);
    this.permissions = new PermissionsResource(transport);
    this.groups = new GroupsResource(transport);
    this.decisions = new DecisionsResource(transport);
    this.sessions = new SessionsResource(transport);
    this.audit = new AuditResource(transport);
    this.compliance = new ComplianceResource(transport);
    this.subjects = new SubjectsResource(transport);
    this.notifications = new NotificationsResource(transport);
    this.events = new EventsResource(transport);
  }
}

/** Convenience factory, matching `new NervesClient(...)`. */
export function createClient(options: NervesClientOptions): NervesClient {
  return new NervesClient(options);
}