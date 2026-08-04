<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { RouterLink, useRoute } from 'vue-router'
import ProblemState from '../../components/ProblemState.vue'
import { useDebouncedRef } from '../../composables/use-debounced-ref'
import { sessionQueryOptions } from '../../session'
import { useWorkspaceStore } from '../../stores/workspace'
import { invoiceListQuery } from './invoice-queries'

const pageSize = 10
const statuses = ['Draft', 'Issued', 'PartiallyPaid', 'Paid', 'Overdue', 'Voided']
const route = useRoute()
const page = ref(1)
const searchInput = ref('')
const search = useDebouncedRef(searchInput, 300)
const status = ref(statuses.includes(String(route.query.status)) ? String(route.query.status) : '')
const sort = ref('updated')
const direction = ref('desc')
const workspace = useWorkspaceStore()
const businessId = computed(() => workspace.selectedBusinessId ?? '')
const session = useQuery(sessionQueryOptions())
const options = computed(() => ({
  search: search.value,
  status: status.value,
  sort: sort.value,
  direction: direction.value,
  page: page.value,
  pageSize,
}))
const query = useQuery(computed(() => invoiceListQuery(businessId.value, options.value)))
const role = computed(() => session.data.value?.memberships.find(
  item => item.businessId === businessId.value,
)?.role)
const canManage = computed(() => role.value === 'Owner' || role.value === 'Accountant')
const totalPages = computed(() => Number(query.data.value?.totalPages ?? 0))
const hasFilters = computed(() => Boolean(searchInput.value || status.value ||
  sort.value !== 'updated' || direction.value !== 'desc'))

watch([businessId, search, status, sort, direction], () => { page.value = 1 })
watch(() => route.query.status, value => {
  status.value = statuses.includes(String(value)) ? String(value) : ''
})

function clearFilters() {
  searchInput.value = ''
  status.value = ''
  sort.value = 'updated'
  direction.value = 'desc'
  page.value = 1
}

const money = (value: number | string, currency: string) =>
  new Intl.NumberFormat('en-ZA', { style: 'currency', currency }).format(Number(value))
</script>

<template>
  <section aria-labelledby="invoices-title" :aria-busy="query.isFetching.value">
    <div class="page-heading">
      <div>
        <p class="eyebrow">Billing workspace</p>
        <h1 id="invoices-title">Invoices</h1>
        <p class="lede">Search and review server-calculated invoices without loading the full ledger.</p>
      </div>
      <RouterLink v-if="canManage" class="button-link" :to="{ name: 'invoice-create' }">Create draft</RouterLink>
    </div>

    <form class="invoice-filters" role="search" @submit.prevent>
      <div class="field filter-search">
        <label for="invoice-search">Search invoices</label>
        <input id="invoice-search" v-model="searchInput" type="search" maxlength="100" autocomplete="off" placeholder="Number or customer" />
      </div>
      <div class="field">
        <label for="invoice-status">Status</label>
        <select id="invoice-status" v-model="status">
          <option value="">All statuses</option>
          <option v-for="value in statuses" :key="value" :value="value">{{ value }}</option>
        </select>
      </div>
      <div class="field">
        <label for="invoice-sort">Sort by</label>
        <select id="invoice-sort" v-model="sort">
          <option value="updated">Last updated</option>
          <option value="due">Due date</option>
          <option value="amount">Total amount</option>
          <option value="customer">Customer</option>
          <option value="number">Invoice number</option>
          <option value="status">Status</option>
        </select>
      </div>
      <div class="field">
        <label for="invoice-direction">Direction</label>
        <select id="invoice-direction" v-model="direction">
          <option value="desc">Descending</option>
          <option value="asc">Ascending</option>
        </select>
      </div>
      <button v-if="hasFilters" type="button" class="secondary" @click="clearFilters">Clear filters</button>
    </form>

    <p v-if="query.isPending.value" class="loading-state" role="status">Loading invoices…</p>
    <ProblemState v-else-if="query.isError.value" :error="query.error.value" @retry="query.refetch()" />
    <template v-else>
      <p class="result-count" role="status" aria-live="polite">
        {{ query.data.value?.totalCount ?? 0 }} invoices found<span v-if="query.isFetching.value"> · updating results…</span>
      </p>
      <div v-if="!query.data.value?.items.length" class="empty-state">
        <h2>{{ hasFilters ? 'No matching invoices' : 'No invoices yet' }}</h2>
        <p>{{ hasFilters ? 'Try a different search or clear the filters.' : 'Create the first draft when customer details are ready.' }}</p>
        <button v-if="hasFilters" type="button" class="secondary" @click="clearFilters">Clear filters</button>
      </div>
      <template v-else>
        <ul class="invoice-list">
          <li v-for="invoice in query.data.value.items" :key="invoice.id">
            <RouterLink :to="{ name: 'invoice-detail', params: { invoiceId: invoice.id } }">
              <span><strong>{{ invoice.number ?? 'Draft' }}</strong><small>{{ invoice.customerName }}</small></span>
              <span><small>Status</small><strong>{{ invoice.status }}</strong></span>
              <span><small>Due</small><strong>{{ invoice.dueDate }}</strong></span>
              <span class="amount"><small>Outstanding</small><strong>{{ money(invoice.outstandingAmount, invoice.currency) }}</strong></span>
            </RouterLink>
          </li>
        </ul>
        <nav class="pagination" aria-label="Invoice pages">
          <button type="button" class="secondary" :disabled="page <= 1 || query.isFetching.value" @click="page--">Previous</button>
          <span>Page {{ page }} of {{ totalPages }}</span>
          <button type="button" class="secondary" :disabled="page >= totalPages || query.isFetching.value" @click="page++">Next</button>
        </nav>
      </template>
    </template>
  </section>
</template>
