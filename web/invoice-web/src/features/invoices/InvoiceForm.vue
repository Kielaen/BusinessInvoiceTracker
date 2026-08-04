<script setup lang="ts">
import { nextTick, reactive, ref } from 'vue'
import type { CustomerResponse } from '../../generated/types.gen'
import type { InvoiceFormValue } from './invoice-form'
import { validateInvoice } from './invoice-form'

const props = defineProps<{
  initial: InvoiceFormValue
  customers: CustomerResponse[]
  submitLabel: string
  pending: boolean
}>()
const emit = defineEmits<{ submit: [value: InvoiceFormValue]; cancel: [] }>()
const copyForm = (value: InvoiceFormValue): InvoiceFormValue => ({
  ...value,
  lines: value.lines.map(line => ({ ...line })),
})
const form = reactive<InvoiceFormValue>(copyForm(props.initial))
const errors = ref<string[]>([])
const summary = ref<HTMLElement>()

const addLine = () => form.lines.push({ description: '', quantity: '1', unitPrice: '0.00', taxRate: '15' })
const removeLine = (index: number) => { if (form.lines.length > 1) form.lines.splice(index, 1) }
const submit = async () => {
  errors.value = validateInvoice(form)
  if (errors.value.length) {
    await nextTick()
    summary.value?.focus()
    return
  }
  // Vue wraps reactive state in a Proxy, which the browser's structuredClone
  // cannot clone. Copy the small form shape explicitly before emitting it.
  emit('submit', copyForm(form))
}
</script>

<template>
  <form class="customer-form" novalidate @submit.prevent="submit">
    <section v-if="errors.length" ref="summary" class="validation-summary" role="alert" tabindex="-1" aria-labelledby="invoice-validation-title">
      <h2 id="invoice-validation-title">Check the invoice details</h2>
      <ul><li v-for="error in errors" :key="error">{{ error }}</li></ul>
    </section>

    <fieldset>
      <legend>Draft details</legend>
      <label for="invoice-customer">Customer</label>
      <select id="invoice-customer" v-model="form.customerId" required>
        <option value="">Select a customer</option>
        <option v-for="customer in customers" :key="customer.id" :value="customer.id">{{ customer.name }}</option>
      </select>
      <label for="invoice-currency">Currency</label>
      <input id="invoice-currency" v-model="form.currency" readonly>
      <label for="invoice-issue-date">Issue date</label>
      <input id="invoice-issue-date" v-model="form.issueDate" type="date" required>
      <label for="invoice-due-date">Due date</label>
      <input id="invoice-due-date" v-model="form.dueDate" type="date" required>
    </fieldset>

    <fieldset>
      <legend>Invoice lines</legend>
      <article v-for="(line, index) in form.lines" :key="index" class="invoice-line-editor">
        <h3>Line {{ index + 1 }}</h3>
        <label :for="`line-description-${index}`">Description</label>
        <input :id="`line-description-${index}`" v-model="line.description" maxlength="500" required>
        <label :for="`line-quantity-${index}`">Quantity</label>
        <input :id="`line-quantity-${index}`" v-model="line.quantity" inputmode="decimal" required>
        <label :for="`line-price-${index}`">Unit price</label>
        <input :id="`line-price-${index}`" v-model="line.unitPrice" inputmode="decimal" required>
        <label :for="`line-tax-${index}`">Tax rate (%)</label>
        <input :id="`line-tax-${index}`" v-model="line.taxRate" inputmode="decimal" required>
        <button type="button" class="text-button" :disabled="form.lines.length === 1" @click="removeLine(index)">Remove line {{ index + 1 }}</button>
      </article>
      <button type="button" class="secondary-button" @click="addLine">Add line</button>
      <p class="field-hint">Authoritative totals are calculated by the server when you save.</p>
    </fieldset>

    <div class="form-actions">
      <button type="submit" :disabled="pending">{{ pending ? 'Saving…' : submitLabel }}</button>
      <button type="button" class="secondary-button" :disabled="pending" @click="emit('cancel')">Cancel</button>
    </div>
  </form>
</template>
