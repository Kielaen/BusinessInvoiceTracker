<script setup lang="ts">
import { nextTick, reactive, ref } from 'vue'
import type { CustomerFormValue } from './customer-form'
import { validateCustomerForm } from './customer-form'

const props = defineProps<{
  initial: CustomerFormValue
  submitLabel: string
  pending: boolean
}>()
const emit = defineEmits<{
  submit: [value: CustomerFormValue]
  cancel: []
}>()

const value = reactive<CustomerFormValue>({ ...props.initial })
const errors = ref<Record<string, string>>({})
const summary = ref<HTMLElement>()

async function submit() {
  errors.value = validateCustomerForm(value)
  if (Object.keys(errors.value).length > 0) {
    await nextTick()
    summary.value?.focus()
    return
  }
  emit('submit', { ...value })
}
</script>

<template>
  <form class="customer-form" novalidate @submit.prevent="submit">
    <section
      v-if="Object.keys(errors).length"
      ref="summary"
      class="validation-summary"
      tabindex="-1"
      role="alert"
      aria-labelledby="validation-title"
    >
      <h2 id="validation-title">Check the customer details</h2>
      <ul>
        <li v-for="(message, field) in errors" :key="field">
          <a :href="`#customer-${field}`">{{ message }}</a>
        </li>
      </ul>
    </section>

    <fieldset>
      <legend>Contact details</legend>
      <div class="field">
        <label for="customer-name">Customer name</label>
        <input id="customer-name" v-model="value.name" maxlength="160" required :aria-invalid="Boolean(errors.name)" :aria-describedby="errors.name ? 'customer-name-error' : undefined" />
        <p v-if="errors.name" id="customer-name-error" class="field-error">{{ errors.name }}</p>
      </div>
      <div class="field-grid">
        <div class="field">
          <label for="customer-email">Email address</label>
          <input id="customer-email" v-model="value.email" type="email" maxlength="254" autocomplete="email" required :aria-invalid="Boolean(errors.email)" :aria-describedby="errors.email ? 'customer-email-error' : undefined" />
          <p v-if="errors.email" id="customer-email-error" class="field-error">{{ errors.email }}</p>
        </div>
        <div class="field">
          <label for="customer-phone">Phone <span class="optional">Optional</span></label>
          <input id="customer-phone" v-model="value.phone" type="tel" maxlength="32" autocomplete="tel" :aria-invalid="Boolean(errors.phone)" :aria-describedby="errors.phone ? 'customer-phone-error' : undefined" />
          <p v-if="errors.phone" id="customer-phone-error" class="field-error">{{ errors.phone }}</p>
        </div>
      </div>
    </fieldset>

    <fieldset>
      <legend>Billing address</legend>
      <div class="field">
        <label for="customer-billingAddressLine1">Address line 1</label>
        <input id="customer-billingAddressLine1" v-model="value.billingAddressLine1" maxlength="200" autocomplete="address-line1" required :aria-invalid="Boolean(errors.billingAddressLine1)" />
        <p v-if="errors.billingAddressLine1" class="field-error">{{ errors.billingAddressLine1 }}</p>
      </div>
      <div class="field">
        <label for="customer-billingAddressLine2">Address line 2 <span class="optional">Optional</span></label>
        <input id="customer-billingAddressLine2" v-model="value.billingAddressLine2" maxlength="200" autocomplete="address-line2" :aria-invalid="Boolean(errors.billingAddressLine2)" />
        <p v-if="errors.billingAddressLine2" class="field-error">{{ errors.billingAddressLine2 }}</p>
      </div>
      <div class="field-grid three">
        <div class="field">
          <label for="customer-billingCity">City</label>
          <input id="customer-billingCity" v-model="value.billingCity" maxlength="120" autocomplete="address-level2" required :aria-invalid="Boolean(errors.billingCity)" />
          <p v-if="errors.billingCity" class="field-error">{{ errors.billingCity }}</p>
        </div>
        <div class="field">
          <label for="customer-billingPostalCode">Postal code</label>
          <input id="customer-billingPostalCode" v-model="value.billingPostalCode" maxlength="32" autocomplete="postal-code" required :aria-invalid="Boolean(errors.billingPostalCode)" />
          <p v-if="errors.billingPostalCode" class="field-error">{{ errors.billingPostalCode }}</p>
        </div>
        <div class="field">
          <label for="customer-billingCountryCode">Country code</label>
          <input id="customer-billingCountryCode" v-model="value.billingCountryCode" maxlength="2" autocomplete="country" required aria-describedby="country-hint" :aria-invalid="Boolean(errors.billingCountryCode)" />
          <p id="country-hint" class="hint">Two letters, for example ZA.</p>
          <p v-if="errors.billingCountryCode" class="field-error">{{ errors.billingCountryCode }}</p>
        </div>
      </div>
    </fieldset>

    <div class="form-actions">
      <button type="submit" :disabled="pending">{{ pending ? 'Saving…' : submitLabel }}</button>
      <button type="button" class="secondary" :disabled="pending" @click="emit('cancel')">Cancel</button>
    </div>
  </form>
</template>
