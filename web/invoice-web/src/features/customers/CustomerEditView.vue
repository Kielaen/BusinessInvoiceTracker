<script setup lang="ts">
import { computed } from 'vue'
import { useMutation, useQuery } from '@tanstack/vue-query'
import { useRoute, useRouter } from 'vue-router'
import { updateCustomer } from '../../api/invoice-api'
import ProblemState from '../../components/ProblemState.vue'
import { queryClient } from '../../query-client'
import { useWorkspaceStore } from '../../stores/workspace'
import CustomerForm from './CustomerForm.vue'
import { customerDetailOptions, customerKeys } from './customer-queries'
import { customerToForm, type CustomerFormValue } from './customer-form'

const route = useRoute()
const router = useRouter()
const workspace = useWorkspaceStore()
const businessId = computed(() => workspace.selectedBusinessId ?? '')
const customerId = computed(() => String(route.params.customerId ?? ''))
const query = useQuery(customerDetailOptions(businessId, customerId))
const mutation = useMutation({
  mutationFn: (value: CustomerFormValue) => updateCustomer(
    businessId.value,
    customerId.value,
    {
      version: query.data.value!.version,
      name: value.name.trim(),
      email: value.email.trim(),
      phone: value.phone.trim() || null,
      billingAddressLine1: value.billingAddressLine1.trim(),
      billingAddressLine2: value.billingAddressLine2.trim() || null,
      billingCity: value.billingCity.trim(),
      billingPostalCode: value.billingPostalCode.trim(),
      billingCountryCode: value.billingCountryCode.trim().toUpperCase(),
    },
  ),
  onSuccess: async customer => {
    queryClient.setQueryData(
      customerKeys.detail(businessId.value, customerId.value),
      customer,
    )
    await queryClient.invalidateQueries({ queryKey: customerKeys.all(businessId.value) })
    await router.replace({ name: 'customer-detail', params: { customerId: customer.id } })
  },
})

async function recover() {
  mutation.reset()
  await query.refetch()
}
</script>

<template>
  <section aria-labelledby="edit-customer-title">
    <p class="eyebrow">Customer directory</p>
    <h1 id="edit-customer-title">Edit customer</h1>
    <p v-if="query.isPending.value" class="loading-state" role="status">Loading customer…</p>
    <ProblemState v-else-if="query.isError.value" :error="query.error.value" @retry="query.refetch()" />
    <template v-else-if="query.data.value">
      <ProblemState v-if="mutation.isError.value" compact :error="mutation.error.value" @retry="recover" />
      <CustomerForm
        :initial="customerToForm(query.data.value)"
        submit-label="Save changes"
        :pending="mutation.isPending.value"
        @submit="mutation.mutate"
        @cancel="router.push({ name: 'customer-detail', params: { customerId } })"
      />
    </template>
  </section>
</template>
