<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { RouterLink } from 'vue-router'
import ProblemState from '../../components/ProblemState.vue'
import { useDebouncedRef } from '../../composables/use-debounced-ref'
import { sessionQueryOptions } from '../../session'
import { useWorkspaceStore } from '../../stores/workspace'
import { customerListOptions } from './customer-queries'

const pageSize = 10
const page = ref(1)
const searchInput = ref('')
const search = useDebouncedRef(searchInput)
const workspace = useWorkspaceStore()
const businessId = computed(() => workspace.selectedBusinessId ?? '')
const session = useQuery(sessionQueryOptions())
const query = useQuery(customerListOptions(businessId, search, page, pageSize))
const activeRole = computed(() => session.data.value?.memberships.find(
  membership => membership.businessId === businessId.value,
)?.role)
const canManage = computed(() => activeRole.value === 'Owner' || activeRole.value === 'Accountant')
const totalPages = computed(() => Number(query.data.value?.totalPages ?? 0))

watch(search, () => { page.value = 1 })
watch(businessId, () => {
  page.value = 1
  searchInput.value = ''
})
</script>

<template>
  <section aria-labelledby="customers-title">
    <div class="page-heading">
      <div>
        <p class="eyebrow">Customer directory</p>
        <h1 id="customers-title">Customers</h1>
        <p class="lede">Search and manage the billing contacts for this business.</p>
      </div>
      <RouterLink v-if="canManage" class="button-link" :to="{ name: 'customer-create' }">Add customer</RouterLink>
    </div>

    <div class="search-field">
      <label for="customer-search">Search customers</label>
      <input id="customer-search" v-model="searchInput" type="search" maxlength="100" placeholder="Name or email" autocomplete="off" />
      <span v-if="query.isFetching.value && !query.isPending.value" class="hint" role="status">Updating results…</span>
    </div>

    <p v-if="query.isPending.value" class="loading-state" role="status">Loading customers…</p>
    <ProblemState v-else-if="query.isError.value" :error="query.error.value" @retry="query.refetch()" />
    <div v-else-if="!query.data.value?.items.length" class="empty-state">
      <h2>{{ search ? 'No matching customers' : 'No customers yet' }}</h2>
      <p>{{ search ? 'Try a different name or email.' : 'Add the first customer when you are ready to create an invoice.' }}</p>
    </div>
    <template v-else>
      <p class="result-count" aria-live="polite">
        {{ query.data.value.totalCount }} customers · page {{ query.data.value.page }} of {{ query.data.value.totalPages }}
      </p>
      <ul class="customer-list">
        <li v-for="customer in query.data.value.items" :key="customer.id">
          <RouterLink :to="{ name: 'customer-detail', params: { customerId: customer.id } }">
            <strong>{{ customer.name }}</strong>
            <span>{{ customer.email }}</span>
            <span>{{ customer.billingCity }}, {{ customer.billingCountryCode }}</span>
          </RouterLink>
        </li>
      </ul>
      <nav class="pagination" aria-label="Customer pages">
        <button type="button" class="secondary" :disabled="page <= 1 || query.isFetching.value" @click="page -= 1">Previous</button>
        <span>Page {{ page }} of {{ totalPages }}</span>
        <button type="button" class="secondary" :disabled="page >= totalPages || query.isFetching.value" @click="page += 1">Next</button>
      </nav>
    </template>
  </section>
</template>
