<script setup lang="ts">
import { RouterView } from 'vue-router'
import RootErrorBoundary from './components/RootErrorBoundary.vue'
</script>

<template>
  <RootErrorBoundary>
    <RouterView />
  </RootErrorBoundary>
</template>
