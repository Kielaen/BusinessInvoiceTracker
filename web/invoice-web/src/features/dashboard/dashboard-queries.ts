import { queryOptions } from '@tanstack/vue-query'
import { loadDashboard } from '../../api/invoice-api'

export const dashboardKey = (businessId: string) =>
  ['dashboard', businessId] as const

export const dashboardQuery = (businessId: string) => queryOptions({
  queryKey: dashboardKey(businessId),
  queryFn: ({ signal }) => loadDashboard(businessId, signal),
  enabled: Boolean(businessId),
})
