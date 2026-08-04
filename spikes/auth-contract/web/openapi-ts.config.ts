import { defineConfig } from '@hey-api/openapi-ts';

export default defineConfig({
  input: '../contract/AuthContract.Api.json',
  output: {
    path: 'src/generated',
    clean: true,
  },
  plugins: [
    '@hey-api/client-fetch',
    '@hey-api/typescript',
    '@hey-api/sdk',
    'zod',
  ],
});
