<script setup lang="ts">
import { computed } from 'vue'
import { useMutation, useQuery } from '@tanstack/vue-query'
import { useRoute, useRouter } from 'vue-router'
import { updateDraftInvoice } from '../../api/invoice-api'
import ProblemState from '../../components/ProblemState.vue'
import { queryClient } from '../../query-client'
import { useWorkspaceStore } from '../../stores/workspace'
import { customerListOptions } from '../customers/customer-queries'
import InvoiceForm from './InvoiceForm.vue'
import { invoiceToForm, invoiceWriteBody, type InvoiceFormValue } from './invoice-form'
import { invoiceDetailQuery, invoiceKeys } from './invoice-queries'

const route = useRoute()
const router = useRouter()
const workspace = useWorkspaceStore()
const businessId = computed(() => workspace.selectedBusinessId ?? '')
const invoiceId = computed(() => String(route.params.invoiceId ?? ''))
const query = useQuery(computed(() => invoiceDetailQuery(businessId.value, invoiceId.value)))
const customers = useQuery(customerListOptions(businessId, computed(() => ''), computed(() => 1), 100))
const mutation = useMutation({
  mutationFn: (value: InvoiceFormValue) => updateDraftInvoice(businessId.value, invoiceId.value, { version: query.data.value!.version, ...invoiceWriteBody(value) }),
  onSuccess: async invoice => {
    queryClient.setQueryData(invoiceKeys.detail(businessId.value, invoiceId.value), invoice)
    await queryClient.invalidateQueries({ queryKey: invoiceKeys.all(businessId.value) })
    await queryClient.invalidateQueries({ queryKey: invoiceKeys.audit(businessId.value, invoiceId.value) })
    await router.replace({ name: 'invoice-detail', params: { invoiceId: invoice.id } })
  },
})
const recover = async () => { mutation.reset(); await query.refetch() }
</script>

<template>
  <section aria-labelledby="edit-invoice-title"><p class="eyebrow">Billing workspace</p><h1 id="edit-invoice-title">Edit draft invoice</h1>
    <p v-if="query.isPending.value || customers.isPending.value" role="status">Loading draft…</p>
    <ProblemState v-else-if="query.isError.value" :error="query.error.value" @retry="query.refetch()" />
    <ProblemState v-else-if="customers.isError.value" :error="customers.error.value" @retry="customers.refetch()" />
    <template v-else-if="query.data.value"><ProblemState v-if="mutation.isError.value" compact :error="mutation.error.value" @retry="recover" />
      <InvoiceForm :initial="invoiceToForm(query.data.value)" :customers="customers.data.value?.items ?? []" submit-label="Save draft" :pending="mutation.isPending.value" @submit="mutation.mutate" @cancel="router.push({ name: 'invoice-detail', params: { invoiceId } })" />
    </template>
  </section>
</template>

