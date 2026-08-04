<script setup lang="ts">
import { computed } from 'vue'
import { useMutation } from '@tanstack/vue-query'
import { useRouter } from 'vue-router'
import { createCustomer } from '../../api/invoice-api'
import ProblemState from '../../components/ProblemState.vue'
import { queryClient } from '../../query-client'
import { useWorkspaceStore } from '../../stores/workspace'
import CustomerForm from './CustomerForm.vue'
import { customerKeys } from './customer-queries'
import { emptyCustomerForm, type CustomerFormValue } from './customer-form'

const router = useRouter()
const workspace = useWorkspaceStore()
const businessId = computed(() => workspace.selectedBusinessId ?? '')
const requestId = crypto.randomUUID()
const mutation = useMutation({
  mutationFn: (value: CustomerFormValue) => createCustomer(businessId.value, {
    requestId,
    name: value.name.trim(),
    email: value.email.trim(),
    phone: value.phone.trim() || null,
    billingAddressLine1: value.billingAddressLine1.trim(),
    billingAddressLine2: value.billingAddressLine2.trim() || null,
    billingCity: value.billingCity.trim(),
    billingPostalCode: value.billingPostalCode.trim(),
    billingCountryCode: value.billingCountryCode.trim().toUpperCase(),
  }),
  onSuccess: async customer => {
    await queryClient.invalidateQueries({ queryKey: customerKeys.all(businessId.value) })
    await router.replace({ name: 'customer-detail', params: { customerId: customer.id } })
  },
})
</script>

<template>
  <section aria-labelledby="create-customer-title">
    <p class="eyebrow">Customer directory</p>
    <h1 id="create-customer-title">Add customer</h1>
    <p class="lede">Contact and billing information will be used when preparing invoices.</p>
    <ProblemState v-if="mutation.isError.value" compact :error="mutation.error.value" @retry="mutation.reset()" />
    <CustomerForm
      :initial="emptyCustomerForm()"
      submit-label="Add customer"
      :pending="mutation.isPending.value"
      @submit="mutation.mutate"
      @cancel="router.push({ name: 'customers' })"
    />
  </section>
</template>
