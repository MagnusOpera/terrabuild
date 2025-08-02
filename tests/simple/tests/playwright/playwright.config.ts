import { defineConfig } from '@playwright/test'

export default defineConfig({
    reporter: [['junit', { outputFile: 'test-results/junit.xml' }]],
    workers: 1,
    projects: [
        {
            name: 'chromium',
            use: {
                browserName: 'chromium',
                launchOptions: {
                    args: ['--no-sandbox', '--disable-dev-shm-usage', '--disable-gpu']
                }
            }
        },
        {
            name: 'webkit',
            use: {
                browserName: 'webkit'
            }
        }
    ]
})
