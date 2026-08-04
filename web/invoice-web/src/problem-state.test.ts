import { createApp, nextTick } from 'vue'
import { afterEach, describe, expect, it } from 'vitest'
import axe from 'axe-core'
import ProblemState from './components/ProblemState.vue'
import { ApiFailure, malformedPayload } from './api/failure'

let host: HTMLDivElement | undefined
function render(error: unknown) {
  host = document.createElement('div')
  document.body.append(host)
  createApp(ProblemState, { error }).mount(host)
  return host
}

afterEach(() => {
  host?.remove()
  host = undefined
})

describe('problem recovery states', () => {
  it.each([
    [400, 'Some information needs attention'],
    [401, 'Please sign in'],
    [403, 'You do not have access'],
    [404, 'That record is not available'],
    [409, 'This record changed'],
    [500, 'Something went wrong'],
  ])('renders status %i safely', async (status, title) => {
    const element = render(new ApiFailure('http', 'safe detail', status))
    await nextTick()
    expect(element.textContent).toContain(title)
    expect(element.querySelector('[role="alert"]')).not.toBeNull()
  })

  it('visibly rejects a malformed successful payload', async () => {
    const element = render(malformedPayload(new Error('schema mismatch')))
    await nextTick()
    expect(element.textContent).toContain('could not be safely displayed')
    expect(element.textContent).toContain('No untrusted values were rendered')
  })

  it('has no automated accessibility violations in the recovery state', async () => {
    render(new ApiFailure('http', 'Sign in again.', 401))
    await nextTick()
    const results = await axe.run(document.body, { rules: { 'color-contrast': { enabled: false } } })
    expect(results.violations).toEqual([])
  })
})
