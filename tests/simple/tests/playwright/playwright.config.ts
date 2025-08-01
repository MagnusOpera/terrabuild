import { defineConfig } from '@playwright/test'

export default defineConfig({
  use: {
    headless: true,
    launchOptions: {
      args: ['--no-sandbox']
    }
  },
  workers: 1,
  reporter: [['junit', { outputFile: 'test-results/junit.xml' }]],
  projects: [
    {
      name: 'chromium',
      use: { browserName: 'chromium' }
    }
  ]
})
