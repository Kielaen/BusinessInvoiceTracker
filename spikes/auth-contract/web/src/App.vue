<script setup lang="ts">
import { ref } from 'vue';

const status = ref('Signed out');

async function getToken(): Promise<string> {
  const response = await fetch('/api/antiforgery', { credentials: 'same-origin' });
  if (!response.ok) throw new Error(`Token request failed: ${response.status}`);
  const body = (await response.json()) as { token: string };
  return body.token;
}

async function signIn(): Promise<void> {
  const token = await getToken();
  const response = await fetch('/api/login', {
    method: 'POST',
    credentials: 'same-origin',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-TOKEN': token,
    },
    body: JSON.stringify({ username: 'owner', password: 'phase0-only' }),
  });
  status.value = response.ok ? 'Signed in through nginx' : `Sign-in failed: ${response.status}`;
}

async function proveMutation(): Promise<void> {
  const token = await getToken();
  const response = await fetch('/api/v1/mutation-proof', {
    method: 'POST',
    credentials: 'same-origin',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-TOKEN': token,
    },
    body: JSON.stringify({ value: 'same-origin' }),
  });
  status.value = response.ok ? 'Mutation accepted with CSRF token' : `Mutation failed: ${response.status}`;
}
</script>

<template>
  <main>
    <h1>Phase 0 cookie and CSRF spike</h1>
    <p role="status">{{ status }}</p>
    <button type="button" @click="signIn">Sign in</button>
    <button type="button" @click="proveMutation">Prove mutation</button>
  </main>
</template>
