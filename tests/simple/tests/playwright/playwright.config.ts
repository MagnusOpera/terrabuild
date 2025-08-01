import { defineConfig } from '@playwright/test'

export default defineConfig({
  reporter: [['junit', { outputFile: 'test-results/junit.xml' }]],
  projects: [
    {
        name: 'chromium',
        use: { browserName: 'chromium' }
    }
  ]
})
