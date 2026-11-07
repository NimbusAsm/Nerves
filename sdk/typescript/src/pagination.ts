import type { QueryParam } from './http.js';

/**
 * Immutable list state: filters, sort, page size, cursor. Every modifier returns
 * a new instance, so a builder can be forked safely.
 */
export class ListQuery {
  static readonly empty = new ListQuery();

  constructor(
    readonly filters: ReadonlyArray<readonly [string, string]> = [],
    readonly sort: string | undefined = undefined,
    readonly limit: number | undefined = undefined,
    readonly cursor: string | undefined = undefined,
  ) {}

  filter(field: string, value: string): ListQuery {
    if (field.length === 0) {
      throw new TypeError('filter field must not be empty');
    }
    return new ListQuery([...this.filters, [field, value]], this.sort, this.limit, this.cursor);
  }

  withSort(expression: string): ListQuery {
    return new ListQuery(this.filters, expression, this.limit, this.cursor);
  }

  withLimit(pageSize: number): ListQuery {
    if (!Number.isInteger(pageSize) || pageSize <= 0) {
      throw new RangeError('page size must be a positive integer');
    }
    return new ListQuery(this.filters, this.sort, pageSize, this.cursor);
  }

  withCursor(cursor: string): ListQuery {
    if (cursor.length === 0) {
      throw new RangeError('cursor must not be empty');
    }
    return new ListQuery(this.filters, this.sort, this.limit, cursor);
  }

  params(): QueryParam[] {
    const params: QueryParam[] = this.filters.map(([field, value]) => [`filter[${field}]`, value]);
    if (this.sort !== undefined) {
      params.push(['sort', this.sort]);
    }
    if (this.limit !== undefined) {
      params.push(['limit', String(this.limit)]);
    }
    if (this.cursor !== undefined) {
      params.push(['cursor', this.cursor]);
    }
    return params;
  }
}