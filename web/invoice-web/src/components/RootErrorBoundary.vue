<script setup lang="ts">
import { ref, onErrorCaptured } from 'vue'
import ProblemState from './ProblemState.vue'

const error = ref<unknown>()
onErrorCaptured((captured) => {
  error.value = captured
  return false
})
function recover() {
  error.value = undefined
  window.location.assign('/')
}
</script>

<template>
  <main v-if="error" class="centered-page">
    <ProblemState :error="error" @retry="recover" />
  </main>
  <slot v-else />
</template>
