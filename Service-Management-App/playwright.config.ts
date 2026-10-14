import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
    testDir: './e2e',
    fullyParallel: true,
    forbidOnly: !!process.env.CI,
    preserveOutput: 'never',
    reporter: 'list',
    use: { baseURL: 'http://localhost:4173', serviceWorkers: 'block', trace: 'off' },
    projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
    webServer: { command: 'npm run preview -- --host localhost --port 4173 --strictPort', url: 'http://localhost:4173/public/Service-Management-App/index.html', reuseExistingServer: false },
});