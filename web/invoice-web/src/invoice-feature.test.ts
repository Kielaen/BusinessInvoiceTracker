import { createApp, nextTick } from 'vue'
import { afterEach, describe, expect, it, vi } from 'vitest'
import axe from 'axe-core'
import { loadInvoiceAudit } from './api/invoice-api'
import InvoiceForm from './features/invoices/InvoiceForm.vue'
import { emptyInvoiceForm, invoiceWriteBody, validateInvoice } from './features/invoices/invoice-form'
import { client } from './generated/client.gen'
import {
  zInvoiceCommandRequest,
  zInvoicePaymentRequest,
  zInvoiceResponse,
} from './generated/zod.gen'

let host: HTMLElement | undefined
afterEach(() => {
  host?.remove()
  host = undefined
  client.setConfig({ baseUrl: '', fetch: globalThis.fetch })
})

const customer = {
  id: '50000000-0000-4000-8000-000000000001',
  name: 'Acme Customer', email: 'customer@test.local', phone: null,
  billingAddressLine1: '1 Test Road', billingAddressLine2: null,
  billingCity: 'Johannesburg', billingPostalCode: '2000',
  billingCountryCode: 'ZA', isActive: true,
  version: '50000000-0000-4000-8000-000000000002',
  createdAtUtc: '2026-07-31T10:00:00Z', updatedAtUtc: '2026-07-31T10:00:00Z',
}

describe('draft invoice feature', () => {
  it('never sends client-calculated totals', () => {
    const value = emptyInvoiceForm()
    value.customerId = customer.id
    value.lines[0]!.description = 'Service'
    const body = invoiceWriteBody(value)
    expect(body).not.toHaveProperty('totalAmount')
    expect(body.lines[0]).toEqual({ description: 'Service', quantity: 1, unitPrice: 0, taxRate: 15 })
  })

  it('accepts a complete server-calculated response contract', () => {
    expect(zInvoiceResponse.safeParse({
      id: '60000000-0000-4000-8000-000000000001', customerId: customer.id,
      customerName: customer.name, number: null, status: 'Draft', currency: 'ZAR',
      issueDate: '2026-07-31', dueDate: '2026-08-31',
      lines: [{ id: '60000000-0000-4000-8000-000000000002', position: 1, description: 'Service', quantity: 2, unitPrice: 100, taxRate: 15, netAmount: 200, taxAmount: 30, totalAmount: 230 }],
      subtotalAmount: 200, taxAmount: 30, totalAmount: 230, paidAmount: 0,
      outstandingAmount: 230, version: '60000000-0000-4000-8000-000000000003',
      createdAtUtc: '2026-07-31T10:00:00Z', updatedAtUtc: '2026-07-31T10:00:00Z',
    }).success).toBe(true)
  })

  it('validates stable lifecycle command identities and payment shape', () => {
    const requestId = '70000000-0000-4000-8000-000000000001'
    const version = '70000000-0000-4000-8000-000000000002'
    expect(zInvoiceCommandRequest.safeParse({ requestId, version }).success).toBe(true)
    expect(zInvoicePaymentRequest.safeParse({
      requestId,
      version,
      idempotencyKey: 'bank-reference-1001',
      amount: 100,
      receivedAtUtc: '2026-07-31T12:00:00Z',
    }).success).toBe(true)
    expect(zInvoicePaymentRequest.safeParse({ requestId, version, amount: 'bad' }).success).toBe(false)
  })

  it('loads and validates one bounded audit page', async () => {
    const requested: string[] = []
    const fetchStub = vi.fn(async (input: RequestInfo | URL) => {
      const url = input instanceof Request ? input.url : input.toString()
      requested.push(url)
      return new Response(JSON.stringify({
        items: [{
          id: '70000000-0000-4000-8000-000000000010',
          action: 'InvoiceIssued',
          actorUserId: '10000000-0000-4000-8000-000000000001',
          occurredAtUtc: '2026-08-04T10:00:00Z',
          details: '{}',
        }],
        page: 2,
        pageSize: 20,
        totalCount: 21,
        totalPages: 2,
      }), { status: 200, headers: { 'Content-Type': 'application/json' } })
    }) as typeof fetch
    client.setConfig({ baseUrl: 'http://localhost', fetch: fetchStub })

    const page = await loadInvoiceAudit(
      '11111111-1111-4111-8111-111111111111',
      '60000000-0000-4000-8000-000000000001',
      2,
      20,
    )

    expect(page.items[0]?.action).toBe('InvoiceIssued')
    expect(requested[0]).toContain('page=2')
    expect(requested[0]).toContain('pageSize=20')
  })

  it('focuses an accessible validation summary', async () => {
    expect(validateInvoice(emptyInvoiceForm())).toContain('Select a customer.')
    host = document.createElement('main')
    document.body.append(host)
    createApp(InvoiceForm, { initial: emptyInvoiceForm(), customers: [customer], submitLabel: 'Create draft', pending: false }).mount(host)
    host.querySelector<HTMLButtonElement>('button[type="submit"]')?.click()
    await nextTick()
    expect(document.activeElement?.getAttribute('aria-labelledby')).toBe('invoice-validation-title')
    const results = await axe.run(document.body, { rules: { 'color-contrast': { enabled: false } } })
    expect(results.violations).toEqual([])
  })

  it('emits a detached value when valid reactive form state is submitted', async () => {
    const initial = emptyInvoiceForm()
    initial.customerId = customer.id
    initial.lines[0]!.description = 'Browser design work'
    initial.lines[0]!.unitPrice = '125.50'
    const submitted = vi.fn()
    host = document.createElement('main')
    document.body.append(host)
    createApp(InvoiceForm, {
      initial,
      customers: [customer],
      submitLabel: 'Create draft',
      pending: false,
      onSubmit: submitted,
    }).mount(host)

    host.querySelector<HTMLButtonElement>('button[type="submit"]')?.click()
    await nextTick()

    expect(submitted).toHaveBeenCalledOnce()
    expect(submitted.mock.calls[0]![0]).toEqual(initial)
    expect(submitted.mock.calls[0]![0]).not.toBe(initial)
  })
})
