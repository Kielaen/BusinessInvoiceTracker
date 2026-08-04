import { createApp, nextTick, type Component } from 'vue'
import { createPinia } from 'pinia'
import { VueQueryPlugin } from '@tanstack/vue-query'
import { createMemoryHistory, createRouter } from 'vue-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import axe from 'axe-core'
import { client } from './generated/client.gen'
import { queryClient } from './query-client'
import { sessionKey } from './session'
import { useWorkspaceStore } from './stores/workspace'
import OverviewView from './features/dashboard/OverviewView.vue'
import InvoiceListView from './features/invoices/InvoiceListView.vue'

const businessId = '11111111-1111-4111-8111-111111111111'
const invoiceId = '60000000-0000-4000-8000-000000000001'
const version = '60000000-0000-4000-8000-000000000002'
let host: HTMLElement | undefined

const invoice = {
  id: invoiceId,
  customerName: 'Northwind Studio',
  number: 'INV-00000042',
  status: 'Overdue',
  currency: 'ZAR',
  issueDate: '2026-07-01',
  dueDate: '2026-07-31',
  totalAmount: 115,
  outstandingAmount: 115,
  version,
  updatedAtUtc: '2026-08-03T10:00:00Z',
}

const dashboard = {
  counts: { total: 4, draft: 1, open: 2, overdue: 1, paid: 1, voided: 0 },
  currencyTotals: [{ currency: 'ZAR', outstandingAmount: 230, overdueAmount: 115 }],
  overdueInvoices: [invoice],
  recentActivity: [{
    id: '70000000-0000-4000-8000-000000000001',
    invoiceId,
    invoiceNumber: invoice.number,
    customerName: invoice.customerName,
    action: 'InvoiceBecameOverdue',
    actorUserId: null,
    occurredAtUtc: '2026-08-03T10:00:00Z',
  }],
  overdueLimit: 5,
  activityLimit: 10,
}

async function render(component: Component, path: string, fetchStub: typeof fetch) {
  host = document.createElement('main')
  document.body.append(host)
  const pinia = createPinia()
  useWorkspaceStore(pinia).selectBusiness(businessId)
  queryClient.setQueryData(sessionKey, {
    userId: '10000000-0000-4000-8000-000000000001',
    email: 'owner@acme.test',
    memberships: [{ businessId, businessName: 'Acme Services', role: 'Owner' }],
  })
  client.setConfig({ baseUrl: 'http://localhost', fetch: fetchStub })
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'overview', component: OverviewView },
      { path: '/invoices', name: 'invoices', component: InvoiceListView },
      { path: '/invoices/new', name: 'invoice-create', component: { template: '<p>New</p>' } },
      { path: '/invoices/:invoiceId', name: 'invoice-detail', component: { template: '<p>Detail</p>' } },
    ],
  })
  await router.push(path)
  await router.isReady()
  createApp(component)
    .use(pinia)
    .use(VueQueryPlugin, { queryClient })
    .use(router)
    .mount(host)
  await nextTick()
  return host
}

afterEach(() => {
  host?.remove()
  host = undefined
  queryClient.clear()
  client.setConfig({ baseUrl: '', fetch: globalThis.fetch })
})

describe('Phase 9 reviewer experience', () => {
  it('renders a bounded currency-aware dashboard with accessible structure', async () => {
    const fetchStub = vi.fn(async () => new Response(JSON.stringify(dashboard), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    })) as typeof fetch
    const element = await render(OverviewView, '/', fetchStub)
    await vi.waitFor(() => expect(element.textContent).toContain('R 230,00'))

    expect(element.textContent).toContain('Invoice summary')
    expect(element.textContent).toContain('Marked overdue')
    expect(element.querySelectorAll('.metric-grid div')).toHaveLength(4)
    expect(element.querySelectorAll('.dashboard-list li')).toHaveLength(1)
    expect(element.querySelectorAll('.activity-list li')).toHaveLength(1)
    const results = await axe.run(document.body, {
      rules: { 'color-contrast': { enabled: false } },
    })
    expect(results.violations).toEqual([])
  })

  it('provides labelled functional filters, route status, paging, and status announcements', async () => {
    const requested: string[] = []
    const fetchStub = vi.fn(async (input: RequestInfo | URL) => {
      const url = input instanceof Request ? input.url : input.toString()
      requested.push(url)
      return new Response(JSON.stringify({
        items: [invoice], page: url.includes('page=2') ? 2 : 1,
        pageSize: 10, totalCount: 11, totalPages: 2,
      }), { status: 200, headers: { 'Content-Type': 'application/json' } })
    }) as typeof fetch
    const element = await render(InvoiceListView, '/invoices?status=Overdue', fetchStub)
    await vi.waitFor(() => expect(element.textContent).toContain('11 invoices found'))

    expect((element.querySelector('#invoice-status') as HTMLSelectElement).value).toBe('Overdue')
    expect(requested.some(url => url.includes('status=Overdue'))).toBe(true)
    const labelled = ['invoice-search', 'invoice-status', 'invoice-sort', 'invoice-direction']
    expect(labelled.every(id => element.querySelector(`label[for="${id}"]`))).toBe(true)
    expect(element.querySelector('[role="status"][aria-live="polite"]')).not.toBeNull()

    const next = [...element.querySelectorAll('button')]
      .find(button => button.textContent?.trim() === 'Next')!
    next.click()
    await vi.waitFor(() => expect(requested.some(url => url.includes('page=2'))).toBe(true))
    const results = await axe.run(document.body, {
      rules: { 'color-contrast': { enabled: false } },
    })
    expect(results.violations).toEqual([])
  })
})
