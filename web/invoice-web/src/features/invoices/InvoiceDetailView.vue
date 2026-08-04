<script setup lang="ts">
import { computed, ref } from 'vue'
import { useMutation, useQuery } from '@tanstack/vue-query'
import { RouterLink, useRoute } from 'vue-router'
import { issueInvoice, recordInvoicePayment, voidInvoice } from '../../api/invoice-api'
import ProblemState from '../../components/ProblemState.vue'
import { queryClient } from '../../query-client'
import { sessionQueryOptions } from '../../session'
import { useWorkspaceStore } from '../../stores/workspace'
import { invoiceAuditQuery, invoiceDetailQuery, invoiceKeys } from './invoice-queries'

const route = useRoute()
const workspace = useWorkspaceStore()
const businessId = computed(() => workspace.selectedBusinessId ?? '')
const invoiceId = computed(() => String(route.params.invoiceId ?? ''))
const query = useQuery(computed(() => invoiceDetailQuery(businessId.value, invoiceId.value)))
const audit = useQuery(computed(() => invoiceAuditQuery(businessId.value, invoiceId.value)))
const session = useQuery(sessionQueryOptions())
const role = computed(() => session.data.value?.memberships.find(item => item.businessId === businessId.value)?.role)
const canManage = computed(() => role.value === 'Owner' || role.value === 'Accountant')
const canEdit = computed(() => canManage.value && query.data.value?.status === 'Draft')
const canIssue = computed(() => canEdit.value)
const canPay = computed(() => canManage.value && ['Issued', 'PartiallyPaid', 'Overdue'].includes(query.data.value?.status ?? ''))
const canVoid = computed(() => canManage.value && Number(query.data.value?.paidAmount ?? 0) === 0 && ['Draft', 'Issued', 'Overdue'].includes(query.data.value?.status ?? ''))
const issueRequestId = crypto.randomUUID()
const voidRequestId = crypto.randomUUID()
const paymentRequestId = ref(crypto.randomUUID())
const paymentKey = ref(`payment-${paymentRequestId.value}`)
const paymentAmount = ref('')
const refresh = async () => {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: invoiceKeys.all(businessId.value) }),
    queryClient.invalidateQueries({ queryKey: invoiceKeys.audit(businessId.value, invoiceId.value) }),
  ])
}
const issueMutation = useMutation({
  mutationFn: () => issueInvoice(businessId.value, invoiceId.value, {
    requestId: issueRequestId, version: query.data.value!.version,
  }),
  onSuccess: refresh,
})
const paymentMutation = useMutation({
  mutationFn: () => recordInvoicePayment(businessId.value, invoiceId.value, {
    requestId: paymentRequestId.value,
    version: query.data.value!.version,
    idempotencyKey: paymentKey.value,
    amount: Number(paymentAmount.value),
    receivedAtUtc: new Date().toISOString(),
  }),
  onSuccess: async () => {
    paymentAmount.value = ''
    paymentRequestId.value = crypto.randomUUID()
    paymentKey.value = `payment-${paymentRequestId.value}`
    await refresh()
  },
})
const voidMutation = useMutation({
  mutationFn: () => voidInvoice(businessId.value, invoiceId.value, {
    requestId: voidRequestId, version: query.data.value!.version,
  }),
  onSuccess: refresh,
})
const actionError = computed(() => issueMutation.error.value ?? paymentMutation.error.value ?? voidMutation.error.value)
const resetActions = () => {
  issueMutation.reset()
  paymentMutation.reset()
  voidMutation.reset()
}
const money = (value: number | string) => new Intl.NumberFormat('en-ZA', {
  style: 'currency', currency: query.data.value?.currency ?? 'ZAR',
}).format(Number(value))
</script>

<template>
  <p><RouterLink :to="{ name: 'invoices' }">← Back to invoices</RouterLink></p>
  <p v-if="query.isPending.value" role="status">Loading invoice…</p>
  <ProblemState v-else-if="query.isError.value" :error="query.error.value" @retry="query.refetch()" />
  <article v-else-if="query.data.value" aria-labelledby="invoice-title">
    <div class="page-heading">
      <div><p class="eyebrow">Invoice preview</p><h1 id="invoice-title">{{ query.data.value.number ?? 'Draft invoice' }}</h1><p>{{ query.data.value.customerName }} · {{ query.data.value.status }}</p></div>
      <div class="form-actions">
        <RouterLink v-if="canEdit" class="button-link" :to="{ name: 'invoice-edit', params: { invoiceId } }">Edit draft</RouterLink>
        <button v-if="canIssue" type="button" :disabled="issueMutation.isPending.value" @click="issueMutation.mutate()">{{ issueMutation.isPending.value ? 'Issuing…' : 'Issue invoice' }}</button>
        <button v-if="canVoid" type="button" class="secondary-button" :disabled="voidMutation.isPending.value" @click="voidMutation.mutate()">{{ voidMutation.isPending.value ? 'Voiding…' : 'Void invoice' }}</button>
      </div>
    </div>
    <ProblemState v-if="actionError" compact :error="actionError" @retry="resetActions" />
    <dl class="detail-grid">
      <div><dt>Issue date</dt><dd>{{ query.data.value.issueDate }}</dd></div>
      <div><dt>Due date</dt><dd>{{ query.data.value.dueDate }}</dd></div>
      <div><dt>Paid</dt><dd>{{ money(query.data.value.paidAmount) }}</dd></div>
      <div><dt>Outstanding</dt><dd>{{ money(query.data.value.outstandingAmount) }}</dd></div>
    </dl>
    <div class="invoice-table-wrap"><table><caption>Server-calculated invoice lines</caption><thead><tr><th>Description</th><th>Quantity</th><th>Unit price</th><th>Tax</th><th>Total</th></tr></thead>
      <tbody><tr v-for="line in query.data.value.lines" :key="line.id"><td>{{ line.description }}</td><td>{{ line.quantity }}</td><td>{{ money(line.unitPrice) }}</td><td>{{ money(line.taxAmount) }}</td><td>{{ money(line.totalAmount) }}</td></tr></tbody>
      <tfoot><tr><th colspan="4">Subtotal</th><td>{{ money(query.data.value.subtotalAmount) }}</td></tr><tr><th colspan="4">Tax</th><td>{{ money(query.data.value.taxAmount) }}</td></tr><tr><th colspan="4">Total</th><td>{{ money(query.data.value.totalAmount) }}</td></tr></tfoot>
    </table></div>
    <form v-if="canPay" class="customer-form" @submit.prevent="paymentMutation.mutate()">
      <fieldset><legend>Record payment</legend><label for="payment-amount">Amount</label><input id="payment-amount" v-model="paymentAmount" inputmode="decimal" required><label for="payment-key">Idempotency key</label><input id="payment-key" v-model="paymentKey" maxlength="100" required><p class="field-hint">Keep the same key when retrying the same payment.</p></fieldset>
      <button type="submit" :disabled="paymentMutation.isPending.value || Number(paymentAmount) <= 0">{{ paymentMutation.isPending.value ? 'Recording…' : 'Record payment' }}</button>
    </form>
    <section aria-labelledby="audit-title"><h2 id="audit-title">Audit history</h2><p v-if="audit.isPending.value" role="status">Loading audit history…</p><ProblemState v-else-if="audit.isError.value" compact :error="audit.error.value" @retry="audit.refetch()" /><ol v-else class="audit-list"><li v-for="entry in audit.data.value" :key="entry.id"><strong>{{ entry.action }}</strong> <time :datetime="entry.occurredAtUtc">{{ new Date(entry.occurredAtUtc).toLocaleString() }}</time></li></ol></section>
  </article>
</template>
