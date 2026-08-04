<script setup lang="ts">
import { ref } from 'vue'
import { useMutation } from '@tanstack/vue-query'
import { readDeliveryProbe, startDeliveryProbe } from '../../api/invoice-api'
import ProblemState from '../../components/ProblemState.vue'

const message = ref('This retained diagnostic proves API, database, outbox, Dapr, and worker delivery.')
const mutation = useMutation({
  mutationFn: async () => {
    const accepted = await startDeliveryProbe()
    for (let attempt = 0; attempt < 30; attempt += 1) {
      const status = await readDeliveryProbe(accepted.probeId)
      if (status.status === 'processed') return status
      await new Promise(resolve => window.setTimeout(resolve, 500))
    }
    throw new Error('The automation receipt did not arrive within 15 seconds.')
  },
  onSuccess: status => {
    message.value = `Processed by the automation worker at ${status.processedAtUtc ?? 'an unknown time'}.`
  },
})
</script>

<template>
  <section aria-labelledby="delivery-title">
    <p class="eyebrow">Operational diagnostic</p>
    <h1 id="delivery-title">Delivery proof</h1>
    <p>{{ message }}</p>
    <ProblemState v-if="mutation.isError.value" compact :error="mutation.error.value" @retry="mutation.mutate()" />
    <button type="button" :disabled="mutation.isPending.value" @click="mutation.mutate()">
      {{ mutation.isPending.value ? 'Waiting for receipt…' : 'Run delivery probe' }}
    </button>
    <p v-if="mutation.isSuccess.value" class="success-state" role="status">Delivery completed successfully.</p>
  </section>
</template>
