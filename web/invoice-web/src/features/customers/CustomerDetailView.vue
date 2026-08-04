<script setup lang="ts">
import { computed } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { RouterLink, useRoute } from 'vue-router'
import ProblemState from '../../components/ProblemState.vue'
import { sessionQueryOptions } from '../../session'
import { useWorkspaceStore } from '../../stores/workspace'
import { customerDetailOptions } from './customer-queries'

const route = useRoute()
const workspace = useWorkspaceStore()
const businessId = computed(() => workspace.selectedBusinessId ?? '')
const customerId = computed(() => String(route.params.customerId ?? ''))
const query = useQuery(customerDetailOptions(businessId, customerId))
const session = useQuery(sessionQueryOptions())
const activeRole = computed(() => session.data.value?.memberships.find(
  membership => membership.businessId === businessId.value,
)?.role)
const canManage = computed(() => activeRole.value === 'Owner' || activeRole.value === 'Accountant')
</script>

<template>
  <p><RouterLink :to="{ name: 'customers' }">← Back to customers</RouterLink></p>
  <p v-if="query.isPending.value" class="loading-state" role="status">Loading customer…</p>
  <ProblemState v-else-if="query.isError.value" :error="query.error.value" @retry="query.refetch()" />
  <section v-else-if="query.data.value" aria-labelledby="customer-title">
    <div class="page-heading">
      <div>
        <p class="eyebrow">Customer detail</p>
        <h1 id="customer-title">{{ query.data.value.name }}</h1>
      </div>
      <RouterLink v-if="canManage" class="button-link" :to="{ name: 'customer-edit', params: { customerId } }">Edit customer</RouterLink>
    </div>
    <dl class="detail-grid">
      <div><dt>Email</dt><dd><a :href="`mailto:${query.data.value.email}`">{{ query.data.value.email }}</a></dd></div>
      <div><dt>Phone</dt><dd>{{ query.data.value.phone ?? 'Not provided' }}</dd></div>
      <div class="wide"><dt>Billing address</dt><dd>
        {{ query.data.value.billingAddressLine1 }}<br />
        <template v-if="query.data.value.billingAddressLine2">{{ query.data.value.billingAddressLine2 }}<br /></template>
        {{ query.data.value.billingCity }}, {{ query.data.value.billingPostalCode }}<br />
        {{ query.data.value.billingCountryCode }}
      </dd></div>
      <div><dt>Status</dt><dd>{{ query.data.value.isActive ? 'Active' : 'Inactive' }}</dd></div>
    </dl>
  </section>
</template>
