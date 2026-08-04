import { createApp, defineComponent, h, nextTick } from 'vue'
import { afterEach, describe, expect, it } from 'vitest'
import FeatureErrorBoundary from './components/FeatureErrorBoundary.vue'
import RootErrorBoundary from './components/RootErrorBoundary.vue'

let host: HTMLDivElement | undefined
const ThrowingChild = defineComponent({
  setup() { throw new Error('deliberate render failure') },
  render: () => h('p'),
})

afterEach(() => {
  host?.remove()
  host = undefined
})

async function renderBoundary(boundary: typeof FeatureErrorBoundary | typeof RootErrorBoundary) {
  host = document.createElement('div')
  document.body.append(host)
  createApp({ render: () => h(boundary, null, { default: () => h(ThrowingChild) }) }).mount(host)
  await nextTick()
  return host
}

describe('error containment', () => {
  it('contains a feature failure with a recovery state', async () => {
    const element = await renderBoundary(FeatureErrorBoundary)
    expect(element.textContent).toContain('Something went wrong')
    expect(element.textContent).toContain('Try again')
  })

  it('contains an application-root failure', async () => {
    const element = await renderBoundary(RootErrorBoundary)
    expect(element.textContent).toContain('Something went wrong')
    expect(element.querySelector('main')).not.toBeNull()
  })
})
