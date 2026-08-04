<script setup lang="ts">
import { ref, onErrorCaptured } from 'vue'
import ProblemState from './ProblemState.vue'

const error = ref<unknown>()
onErrorCaptured((captured) => {
  error.value = captured
  return false
})
function recover() { error.value = undefined }
</script>

<template>
  <ProblemState v-if="error" :error="error" @retry="recover" />
  <slot v-else />
</template>
