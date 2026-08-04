<script setup lang="ts">
import { computed } from 'vue'
import { useMutation, useQuery } from '@tanstack/vue-query'
import { useRouter } from 'vue-router'
import { createDraftInvoice } from '../../api/invoice-api'
import ProblemState from '../../components/ProblemState.vue'
import { queryClient } from '../../query-client'
import { useWorkspaceStore } from '../../stores/workspace'
import { customerListOptions } from '../customers/customer-queries'
import InvoiceForm from './InvoiceForm.vue'
import { emptyInvoiceForm, invoiceWriteBody, type InvoiceFormValue } from './invoice-form'
import { invoiceKeys } from './invoice-queries'

const router = useRouter()
const workspace = useWorkspaceStore()
const businessId = computed(() => workspace.selectedBusinessId ?? '')
const customers = useQuery(customerListOptions(businessId, computed(() => ''), computed(() => 1), 100))
const requestId = crypto.randomUUID()
const initial = emptyInvoiceForm()
const mutation = useMutation({
  mutationFn: (value: InvoiceFormValue) => createDraftInvoice(businessId.value, { requestId, ...invoiceWriteBody(value) }),
  onSuccess: async invoice => {
    await queryClient.invalidateQueries({ queryKey: invoiceKeys.all(businessId.value) })
    await router.replace({ name: 'invoice-detail', params: { invoiceId: invoice.id } })
  },
})
</script>

<template>
  <section aria-labelledby="create-invoice-title">
    <p class="eyebrow">Billing workspace</p><h1 id="create-invoice-title">Create draft invoice</h1>
    <p class="lede">Line totals, tax, and invoice totals are calculated by the server.</p>
    <p v-if="customers.isPending.value" role="status">Loading customers…</p>
    <ProblemState v-else-if="customers.isError.value" :error="customers.error.value" @retry="customers.refetch()" />
    <template v-else>
      <ProblemState v-if="mutation.isError.value" compact :error="mutation.error.value" @retry="mutation.reset()" />
      <InvoiceForm :initial="initial" :customers="customers.data.value?.items ?? []" submit-label="Create draft" :pending="mutation.isPending.value" @submit="mutation.mutate" @cancel="router.push({ name: 'invoices' })" />
    </template>
  </section>
</template>

