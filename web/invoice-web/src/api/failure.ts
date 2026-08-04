import { z } from 'zod'
import { zHttpValidationProblemDetails, zProblemDetails } from '../generated/zod.gen'

export type FailureKind = 'http' | 'network' | 'malformed' | 'unexpected'

export class ApiFailure extends Error {
  constructor(
    public readonly kind: FailureKind,
    message: string,
    public readonly status?: number,
    public readonly fieldErrors?: Record<string, string[]>,
  ) {
    super(message)
    this.name = 'ApiFailure'
  }
}

export function malformedPayload(error: unknown): ApiFailure {
  const detail = error instanceof z.ZodError
    ? `The server response failed ${error.issues.length} contract check(s).`
    : 'The server response did not match the generated contract.'
  return new ApiFailure('malformed', detail)
}

export function responseFailure(
  status: number,
  payload: unknown,
): ApiFailure {
  const validation = zHttpValidationProblemDetails.safeParse(payload)
  if (validation.success && validation.data.errors) {
    return new ApiFailure(
      'http',
      validation.data.detail ?? validation.data.title ?? 'The request is invalid.',
      status,
      validation.data.errors,
    )
  }

  const problem = zProblemDetails.safeParse(payload)
  return new ApiFailure(
    'http',
    problem.success
      ? problem.data.detail ?? problem.data.title ?? `Request failed with status ${status}.`
      : `Request failed with status ${status}.`,
    status,
  )
}

export function normalizeFailure(error: unknown): ApiFailure {
  if (error instanceof ApiFailure) return error
  if (error instanceof TypeError) {
    return new ApiFailure('network', 'The service could not be reached. Check your connection and try again.')
  }
  return new ApiFailure('unexpected', 'An unexpected error occurred. Try again or return to the overview.')
}
