<script setup lang="ts">
import { computed } from 'vue'
import { normalizeFailure } from '../api/failure'

const props = defineProps<{
  error: unknown
  compact?: boolean
}>()
const emit = defineEmits<{ retry: [] }>()

const failure = computed(() => normalizeFailure(props.error))
const presentation = computed(() => {
  if (failure.value.kind === 'malformed') {
    return {
      eyebrow: 'Contract error',
      title: 'The response could not be safely displayed',
      message: 'The service returned data in an unexpected format. No untrusted values were rendered.',
      action: 'Try again',
    }
  }
  switch (failure.value.status) {
    case 400: return { eyebrow: 'Check your details', title: 'Some information needs attention', message: failure.value.message, action: 'Review and try again' }
    case 401: return { eyebrow: 'Session required', title: 'Please sign in', message: 'Your session is missing or has expired.', action: 'Sign in again' }
    case 403: return { eyebrow: 'Access denied', title: 'You do not have access', message: 'Ask a business owner if you need this permission.', action: 'Return and retry' }
    case 404: return { eyebrow: 'Not found', title: 'That record is not available', message: 'It may have moved, been removed, or belong to another workspace.', action: 'Return to overview' }
    case 409: return { eyebrow: 'Update conflict', title: 'This record changed', message: 'Reload the latest version before applying your changes again.', action: 'Reload latest' }
    default: return { eyebrow: 'Service problem', title: failure.value.kind === 'network' ? 'The service is unavailable' : 'Something went wrong', message: failure.value.message, action: 'Try again' }
  }
})
</script>

<template>
  <section class="problem-state" :class="{ compact }" role="alert" aria-labelledby="problem-title">
    <p class="eyebrow">{{ presentation.eyebrow }}</p>
    <h2 id="problem-title">{{ presentation.title }}</h2>
    <p>{{ presentation.message }}</p>
    <ul v-if="failure.fieldErrors" class="field-errors">
      <template v-for="(messages, field) in failure.fieldErrors" :key="field">
        <li v-for="message in messages" :key="message"><strong>{{ field }}:</strong> {{ message }}</li>
      </template>
    </ul>
    <button type="button" class="secondary" @click="emit('retry')">{{ presentation.action }}</button>
  </section>
</template>
