import { queryOptions } from '@tanstack/vue-query'
import { loadInvoice, loadInvoiceAudit, loadInvoices } from '../../api/invoice-api'

export const invoiceKeys = {
  all: (businessId: string) => ['invoices', businessId] as const,
  list: (businessId: string, options: InvoiceListOptions) =>
    ['invoices', businessId, 'list', options] as const,
  detail: (businessId: string, invoiceId: string) => ['invoices', businessId, 'detail', invoiceId] as const,
  audit: (businessId: string, invoiceId: string, page?: number) =>
    ['invoices', businessId, 'audit', invoiceId, page] as const,
}

export interface InvoiceListOptions {
  search: string
  status: string
  sort: string
  direction: string
  page: number
  pageSize: number
}

export const invoiceListQuery = (businessId: string, options: InvoiceListOptions) => queryOptions({
  queryKey: invoiceKeys.list(businessId, options),
  queryFn: ({ signal }) => loadInvoices(businessId, options, signal),
  enabled: Boolean(businessId),
  placeholderData: (previous) => previous,
})

export const invoiceDetailQuery = (businessId: string, invoiceId: string) => queryOptions({
  queryKey: invoiceKeys.detail(businessId, invoiceId),
  queryFn: ({ signal }) => loadInvoice(businessId, invoiceId, signal),
})

export const invoiceAuditQuery = (businessId: string, invoiceId: string, page: number) => queryOptions({
  queryKey: invoiceKeys.audit(businessId, invoiceId, page),
  queryFn: ({ signal }) => loadInvoiceAudit(businessId, invoiceId, page, 20, signal),
  placeholderData: (previous) => previous,
})
