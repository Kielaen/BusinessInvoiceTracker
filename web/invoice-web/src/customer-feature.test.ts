import { createApp, effectScope, nextTick, ref } from 'vue'
import { afterEach, describe, expect, it, vi } from 'vitest'
import axe from 'axe-core'
import CustomerForm from './features/customers/CustomerForm.vue'
import { emptyCustomerForm, validateCustomerForm } from './features/customers/customer-form'
import { useDebouncedRef } from './composables/use-debounced-ref'
import { loadCustomers } from './api/invoice-api'
import { client } from './generated/client.gen'

let host: HTMLElement | undefined

afterEach(() => {
  host?.remove()
  host = undefined
  vi.useRealTimers()
  client.setConfig({ baseUrl: '', fetch: globalThis.fetch })
})

describe('customer feature foundation', () => {
  it('debounces search and cancels the obsolete timer', async () => {
    vi.useFakeTimers()
    const source = ref('')
    const scope = effectScope()
    const debounced = scope.run(() => useDebouncedRef(source, 300))!

    source.value = 'first'
    await nextTick()
    await vi.advanceTimersByTimeAsync(200)
    source.value = 'second'
    await nextTick()
    await vi.advanceTimersByTimeAsync(299)
    expect(debounced.value).toBe('')
    await vi.advanceTimersByTimeAsync(1)
    expect(debounced.value).toBe('second')
    scope.stop()
  })

  it('propagates the query abort signal to the generated HTTP client', async () => {
    const aborted = vi.fn()
    const fetchStub = vi.fn((input: RequestInfo | URL, init?: RequestInit) =>
      new Promise<Response>((_resolve, reject) => {
        const signal = input instanceof Request ? input.signal : init?.signal
        signal?.addEventListener('abort', () => {
          aborted()
          reject(new DOMException('The request was cancelled.', 'AbortError'))
        })
      }))
    client.setConfig({ baseUrl: 'http://localhost', fetch: fetchStub })
    const controller = new AbortController()
    const request = loadCustomers(
      '11111111-1111-4111-8111-111111111111',
      'obsolete',
      1,
      10,
      controller.signal,
    )
    await vi.waitFor(() => expect(fetchStub).toHaveBeenCalledOnce())
    controller.abort()

    await expect(request).rejects.toMatchObject({ name: 'AbortError' })
    expect(aborted).toHaveBeenCalledOnce()
  })

  it('validates every required billing boundary', () => {
    const errors = validateCustomerForm(emptyCustomerForm())
    expect(Object.keys(errors)).toEqual([
      'name',
      'email',
      'billingAddressLine1',
      'billingCity',
      'billingPostalCode',
    ])
  })

  it('focuses a visible validation summary and has no automated violations', async () => {
    host = document.createElement('main')
    document.body.append(host)
    createApp(CustomerForm, {
      initial: emptyCustomerForm(),
      submitLabel: 'Add customer',
      pending: false,
    }).mount(host)
    host.querySelector<HTMLButtonElement>('button[type="submit"]')?.click()
    await nextTick()

    expect(document.activeElement?.getAttribute('aria-labelledby')).toBe('validation-title')
    expect(host.textContent).toContain('Check the customer details')
    const results = await axe.run(document.body, {
      rules: { 'color-contrast': { enabled: false } },
    })
    expect(results.violations).toEqual([])
  })
})
