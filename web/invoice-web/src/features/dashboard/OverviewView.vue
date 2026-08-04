<script setup lang="ts">
import { computed } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { RouterLink } from 'vue-router'
import ProblemState from '../../components/ProblemState.vue'
import { sessionQueryOptions } from '../../session'
import { useWorkspaceStore } from '../../stores/workspace'
import { dashboardQuery } from './dashboard-queries'

const session = useQuery(sessionQueryOptions())
const workspace = useWorkspaceStore()
const businessId = computed(() => workspace.selectedBusinessId ?? '')
const activeBusiness = computed(() => session.data.value?.memberships.find(
  membership => membership.businessId === businessId.value,
))
const query = useQuery(computed(() => dashboardQuery(businessId.value)))

const money = (value: number | string, currency: string) =>
  new Intl.NumberFormat('en-ZA', { style: 'currency', currency }).format(Number(value))
const activityLabel = (action: string) => ({
  DraftCreated: 'Draft created',
  DraftUpdated: 'Draft updated',
  InvoiceIssued: 'Invoice issued',
  InvoicePaymentRecorded: 'Payment recorded',
  InvoiceVoided: 'Invoice voided',
  InvoiceDueDateChanged: 'Due date changed',
  InvoiceBecameOverdue: 'Marked overdue',
}[action] ?? action)
</script>

<template>
  <section aria-labelledby="overview-title" :aria-busy="query.isFetching.value">
    <div class="page-heading">
      <div>
        <p class="eyebrow">Workspace overview</p>
        <h1 id="overview-title">{{ activeBusiness?.businessName ?? 'Your business' }}</h1>
        <p class="lede">A tenant-scoped view of invoices requiring attention and recent ledger activity.</p>
      </div>
      <RouterLink class="button-link" :to="{ name: 'invoices' }">Review invoices</RouterLink>
    </div>

    <p v-if="query.isPending.value" class="loading-state" role="status">Loading dashboard…</p>
    <ProblemState v-else-if="query.isError.value" :error="query.error.value" @retry="query.refetch()" />
    <template v-else-if="query.data.value">
      <section aria-labelledby="summary-title">
        <h2 id="summary-title" class="section-title">Invoice summary</h2>
        <dl class="metric-grid">
          <div><dt>Total invoices</dt><dd>{{ query.data.value.counts.total }}</dd></div>
          <div><dt>Open</dt><dd>{{ query.data.value.counts.open }}</dd></div>
          <div class="attention"><dt>Overdue</dt><dd>{{ query.data.value.counts.overdue }}</dd></div>
          <div><dt>Paid</dt><dd>{{ query.data.value.counts.paid }}</dd></div>
        </dl>
      </section>

      <section aria-labelledby="receivables-title">
        <h2 id="receivables-title" class="section-title">Receivables by currency</h2>
        <div v-if="query.data.value.currencyTotals.length" class="currency-grid">
          <article v-for="total in query.data.value.currencyTotals" :key="total.currency">
            <h3>{{ total.currency }}</h3>
            <p><span>Outstanding</span><strong>{{ money(total.outstandingAmount, total.currency) }}</strong></p>
            <p><span>Overdue</span><strong>{{ money(total.overdueAmount, total.currency) }}</strong></p>
          </article>
        </div>
        <p v-else class="empty-inline">No issued receivables yet.</p>
      </section>

      <div class="dashboard-columns">
        <section aria-labelledby="overdue-title">
          <div class="section-heading"><h2 id="overdue-title">Overdue invoices</h2><RouterLink :to="{ name: 'invoices', query: { status: 'Overdue' } }">View all</RouterLink></div>
          <ul v-if="query.data.value.overdueInvoices.length" class="dashboard-list">
            <li v-for="invoice in query.data.value.overdueInvoices" :key="invoice.id">
              <RouterLink :to="{ name: 'invoice-detail', params: { invoiceId: invoice.id } }">
                <span><strong>{{ invoice.number }}</strong><small>{{ invoice.customerName }} · due {{ invoice.dueDate }}</small></span>
                <strong>{{ money(invoice.outstandingAmount, invoice.currency) }}</strong>
              </RouterLink>
            </li>
          </ul>
          <p v-else class="empty-inline">Nothing is overdue.</p>
        </section>

        <section aria-labelledby="activity-title">
          <h2 id="activity-title">Recent activity</h2>
          <ol v-if="query.data.value.recentActivity.length" class="activity-list">
            <li v-for="entry in query.data.value.recentActivity" :key="entry.id">
              <RouterLink :to="{ name: 'invoice-detail', params: { invoiceId: entry.invoiceId } }">
                <strong>{{ activityLabel(entry.action) }}</strong>
                <span>{{ entry.invoiceNumber ?? 'Draft' }} · {{ entry.customerName }}</span>
                <time :datetime="entry.occurredAtUtc">{{ new Date(entry.occurredAtUtc).toLocaleString() }}</time>
              </RouterLink>
            </li>
          </ol>
          <p v-else class="empty-inline">No invoice activity yet.</p>
        </section>
      </div>
    </template>
  </section>
</template>
