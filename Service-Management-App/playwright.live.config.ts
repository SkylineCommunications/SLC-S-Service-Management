import { defineConfig, devices } from '@playwright/test';

const dmaUrl = process.env.DMA_URL?.replace(/\/+$/, '');
if (!dmaUrl) throw new Error('Set DMA_URL to the DataMiner Agent base URL, e.g. https://dma.example.com');

// Runs against the deployed app on a real DataMiner Agent; no local web server and no mocks.
export default defineConfig({
    testDir: './e2e-live',
    fullyParallel: false,
    workers: 1,
    forbidOnly: !!process.env.CI,
    retries: process.env.CI ? 1 : 0,
    timeout: 60_000,
    expect: { timeout: 20_000 },
    reporter: [['list'], ['html', { open: 'never' }]],
    use: {
        baseURL: dmaUrl,
        ignoreHTTPSErrors: process.env.DMA_IGNORE_HTTPS_ERRORS === 'true',
        serviceWorkers: 'block',
        trace: 'retain-on-failure',
        screenshot: 'only-on-failure',
    },
    projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
