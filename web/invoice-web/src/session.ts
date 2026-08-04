import { queryOptions } from '@tanstack/vue-query'
import { loadCurrentUser } from './api/invoice-api'

export const sessionKey = ['session'] as const

export function sessionQueryOptions() {
  return queryOptions({
    queryKey: sessionKey,
    queryFn: loadCurrentUser,
  })
}
