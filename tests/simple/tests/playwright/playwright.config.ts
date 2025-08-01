import { defineConfig } from '@playwright/test'

export default defineConfig({
  reporter: [
    ['junit', { outputFile: 'test-results/junit.xml' }]
  ],
  outputDir: 'test-results',
  timeout: 30_000
})
