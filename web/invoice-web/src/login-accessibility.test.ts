import { createApp, nextTick } from 'vue'
import { VueQueryPlugin } from '@tanstack/vue-query'
import { createMemoryHistory, createRouter } from 'vue-router'
import { afterEach, describe, expect, it } from 'vitest'
import axe from 'axe-core'
import LoginView from './features/auth/LoginView.vue'
import { queryClient } from './query-client'

let host: HTMLDivElement | undefined

async function renderLogin() {
  host = document.createElement('div')
  document.body.append(host)
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/login', name: 'login', component: LoginView },
      { path: '/', name: 'overview', component: { template: '<p>Overview</p>' } },
    ],
  })
  await router.push('/login')
  await router.isReady()
  createApp(LoginView).use(VueQueryPlugin, { queryClient }).use(router).mount(host)
  await nextTick()
  return host
}

afterEach(() => {
  host?.remove()
  host = undefined
  queryClient.clear()
})

describe('authentication accessibility', () => {
  it('exposes labelled controls in semantic keyboard order', async () => {
    const element = await renderLogin()
    const controls = [...element.querySelectorAll<HTMLElement>('input, button')]
    expect(controls.map(control => control.id || control.textContent?.trim())).toEqual([
      'email', 'password', 'Sign in',
    ])
    expect(element.querySelector('form')).not.toBeNull()
    expect(element.querySelector('button[type="submit"]')).not.toBeNull()
    expect(element.querySelector('[tabindex]:not([tabindex="-1"])')).toBeNull()
  })

  it('has no automated accessibility violations', async () => {
    await renderLogin()
    const results = await axe.run(document.body, { rules: { 'color-contrast': { enabled: false } } })
    expect(results.violations).toEqual([])
  })
})
