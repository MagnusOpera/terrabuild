import { defineConfig } from '@playwright/test'

export default defineConfig({
    reporter: [['junit', { outputFile: 'test-results/junit.xml' }]],
    workers: 1,
    projects: [
        {
            name: 'chromium',
            use: { browserName: 'chromium' }
        }
    ]
})
