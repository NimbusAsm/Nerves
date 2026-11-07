import { Transport, type NervesClientOptions } from './http.js';
import { AccountsResource } from './resources/accounts.js';
import { LegalEntitiesResource } from './resources/legalEntities.js';
import { RolesResource } from './resources/roles.js';

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

  constructor(options: NervesClientOptions) {
    const transport = new Transport(options);
    this.accounts = new AccountsResource(transport);
    this.legalEntities = new LegalEntitiesResource(transport);
    this.roles = new RolesResource(transport);
  }
}

/** Convenience factory, matching `new NervesClient(...)`. */
export function createClient(options: NervesClientOptions): NervesClient {
  return new NervesClient(options);
}
