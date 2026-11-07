/** RFC 9457 `application/problem+json` body returned by the management API. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

/**
 * The single error type raised by the SDK for non-success responses.
 * It always carries the HTTP status, the problem type, and the trace id.
 */
export class NervesApiError extends Error {
  readonly status: number;
  readonly type: string | undefined;
  readonly traceId: string | undefined;
  readonly problem: ProblemDetails;
  readonly rawBody: string | undefined;

  constructor(status: number, problem: ProblemDetails, rawBody?: string) {
    const detail = problem.detail ?? problem.title ?? `HTTP ${status}`;
    const trace = problem.traceId ? ` (traceId: ${problem.traceId})` : '';
    super(`Nerves API request failed with status ${status}: ${detail}${trace}`);
    this.name = 'NervesApiError';
    this.status = status;
    this.type = problem.type;
    this.traceId = problem.traceId;
    this.problem = problem;
    this.rawBody = rawBody;
  }

  get title(): string | undefined {
    return this.problem.title;
  }

  get detail(): string | undefined {
    return this.problem.detail;
  }

  get errors(): Record<string, string[]> | undefined {
    return this.problem.errors;
  }
}
