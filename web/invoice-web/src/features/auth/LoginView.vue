<script setup lang="ts">
import { nextTick, ref } from 'vue'
import { useMutation } from '@tanstack/vue-query'
import { useRoute, useRouter } from 'vue-router'
import { signIn } from '../../api/invoice-api'
import ProblemState from '../../components/ProblemState.vue'
import { queryClient } from '../../query-client'
import { sessionKey } from '../../session'

const email = ref('owner@acme.test')
const password = ref('Phase2-local-only!')
const errorRegion = ref<HTMLElement>()
const route = useRoute()
const router = useRouter()

const mutation = useMutation({
  mutationFn: signIn,
  onSuccess: async (user) => {
    queryClient.setQueryData(sessionKey, user)
    const target = typeof route.query.returnTo === 'string' ? route.query.returnTo : '/'
    await router.replace(target)
  },
  onError: async () => {
    await nextTick()
    errorRegion.value?.focus()
  },
})

function submit() {
  mutation.mutate({ email: email.value.trim(), password: password.value })
}
</script>

<template>
  <main class="centered-page">
    <section class="auth-card" aria-labelledby="login-title">
      <p class="eyebrow">Secure workspace</p>
      <h1 id="login-title">Sign in to Invoice Tracker</h1>
      <p class="lede">Manage invoices and payments for the businesses you are authorised to access.</p>
      <div v-if="mutation.isError.value" ref="errorRegion" tabindex="-1">
        <ProblemState compact :error="mutation.error.value" @retry="submit" />
      </div>
      <form novalidate @submit.prevent="submit">
        <div class="field">
          <label for="email">Email address</label>
          <input id="email" v-model="email" type="email" autocomplete="username" required />
        </div>
        <div class="field">
          <label for="password">Password</label>
          <input
            id="password"
            v-model="password"
            type="password"
            autocomplete="current-password"
            required
            @keydown.enter.prevent="submit"
          />
        </div>
        <button type="submit" :disabled="mutation.isPending.value">
          {{ mutation.isPending.value ? 'Signing in…' : 'Sign in' }}
        </button>
      </form>
      <p class="review-note">Local review account details are documented in the repository runbook.</p>
    </section>
  </main>
</template>
