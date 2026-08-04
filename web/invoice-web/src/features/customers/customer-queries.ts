import { computed, type Ref } from 'vue'
import { queryOptions } from '@tanstack/vue-query'
import { loadCustomer, loadCustomers } from '../../api/invoice-api'

export const customerKeys = {
  all: (businessId: string) => ['customers', businessId] as const,
  list: (businessId: string, search: string, page: number, pageSize: number) =>
    ['customers', businessId, 'list', search, page, pageSize] as const,
  detail: (businessId: string, customerId: string) =>
    ['customers', businessId, 'detail', customerId] as const,
}

export function customerListOptions(
  businessId: Ref<string>,
  search: Ref<string>,
  page: Ref<number>,
  pageSize: number,
) {
  return queryOptions({
    queryKey: computed(() => customerKeys.list(
      businessId.value,
      search.value,
      page.value,
      pageSize,
    )),
    queryFn: ({ signal }) => loadCustomers(
      businessId.value,
      search.value,
      page.value,
      pageSize,
      signal,
    ),
    enabled: computed(() => Boolean(businessId.value)),
    placeholderData: previous => previous,
  })
}

export function customerDetailOptions(
  businessId: Ref<string>,
  customerId: Ref<string>,
) {
  return queryOptions({
    queryKey: computed(() => customerKeys.detail(businessId.value, customerId.value)),
    queryFn: ({ signal }) => loadCustomer(businessId.value, customerId.value, signal),
    enabled: computed(() => Boolean(businessId.value && customerId.value)),
  })
}
