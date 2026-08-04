<script setup lang="ts">
import { RouterLink } from 'vue-router'
</script>

<template>
  <main class="centered-page">
    <section class="problem-state" aria-labelledby="not-found-title">
      <p class="eyebrow">Not found</p>
      <h1 id="not-found-title">This page does not exist</h1>
      <p>Check the address or return to your workspace.</p>
      <RouterLink class="button-link" to="/">Return to overview</RouterLink>
    </section>
  </main>
</template>
