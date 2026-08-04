<script setup lang="ts">
import { computed, watchEffect } from 'vue'
import { RouterLink, RouterView, useRouter } from 'vue-router'
import { useMutation, useQuery } from '@tanstack/vue-query'
import { signOut } from '../api/invoice-api'
import FeatureErrorBoundary from '../components/FeatureErrorBoundary.vue'
import ProblemState from '../components/ProblemState.vue'
import { queryClient } from '../query-client'
import { sessionKey, sessionQueryOptions } from '../session'
import { useWorkspaceStore } from '../stores/workspace'

const router = useRouter()
const workspace = useWorkspaceStore()
const session = useQuery(sessionQueryOptions())
const memberships = computed(() => session.data.value?.memberships ?? [])

watchEffect(() => {
  if (!workspace.selectedBusinessId && memberships.value[0]) {
    workspace.selectBusiness(memberships.value[0].businessId)
  }
})

const logoutMutation = useMutation({
  mutationFn: signOut,
  onSuccess: async () => {
    queryClient.setQueryData(sessionKey, null)
    workspace.selectBusiness(null)
    await router.push({ name: 'login' })
  },
})
</script>

<template>
  <a class="skip-link" href="#main-content">Skip to main content</a>
  <div class="app-frame">
    <header class="app-header">
      <RouterLink class="brand" :to="{ name: 'overview' }" @keydown.enter.prevent="router.push({ name: 'overview' })">Invoice Tracker</RouterLink>
      <div class="header-tools">
        <label v-if="memberships.length" for="business-switcher" class="sr-only">Active business</label>
        <select
          v-if="memberships.length"
          id="business-switcher"
          :value="workspace.selectedBusinessId ?? ''"
          @change="workspace.selectBusiness(($event.target as HTMLSelectElement).value)"
        >
          <option v-for="membership in memberships" :key="membership.businessId" :value="membership.businessId">
            {{ membership.businessName }}
          </option>
        </select>
        <span class="session-email">{{ session.data.value?.email }}</span>
        <button type="button" class="text-button" :disabled="logoutMutation.isPending.value" @click="logoutMutation.mutate()">
          {{ logoutMutation.isPending.value ? 'Signing out…' : 'Sign out' }}
        </button>
      </div>
    </header>
    <nav class="primary-nav" aria-label="Primary navigation">
      <RouterLink :to="{ name: 'overview' }" @keydown.enter.prevent="router.push({ name: 'overview' })">Overview</RouterLink>
      <RouterLink :to="{ name: 'customers' }" @keydown.enter.prevent="router.push({ name: 'customers' })">Customers</RouterLink>
      <RouterLink :to="{ name: 'invoices' }" @keydown.enter.prevent="router.push({ name: 'invoices' })">Invoices</RouterLink>
      <RouterLink :to="{ name: 'delivery' }" @keydown.enter.prevent="router.push({ name: 'delivery' })">Delivery proof</RouterLink>
    </nav>
    <main id="main-content" tabindex="-1">
      <ProblemState v-if="logoutMutation.isError.value" compact :error="logoutMutation.error.value" @retry="logoutMutation.mutate()" />
      <FeatureErrorBoundary>
        <RouterView />
      </FeatureErrorBoundary>
    </main>
  </div>
</template>
