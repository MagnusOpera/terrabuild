import { defineConfig } from '@playwright/test'

export default defineConfig({
    reporter: [['junit', { outputFile: 'test-results/junit.xml' }]],
    workers: 1,
    projects: [
        {
            name: 'local',
            use: {
                browserName: 'chromium',
                launchOptions: {
                    args: ['--no-sandbox', '--disable-dev-shm-usage', '--disable-gpu']
                }
            }
        },
        {
            name: 'ci',
            use: {
                browserName: 'webkit'
            }
        }
    ]
})
