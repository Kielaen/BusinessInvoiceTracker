import { QueryClient } from '@tanstack/vue-query'
import { ApiFailure } from './api/failure'

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: false,
      retry: (failureCount, error) => {
        if (failureCount >= 1) return false
        return error instanceof ApiFailure &&
          (error.kind === 'network' || (error.status ?? 0) >= 500)
      },
    },
    mutations: {
      retry: false,
    },
  },
})
