import { expect, test } from '@playwright/test';
import { APP_URL } from './fixtures';
test('redirects a missing session to DataMiner authentication', { tag: '@A3' }, async ({ page }) => { await page.goto(APP_URL); await expect.poll(() => new URL(page.url()).pathname).toBe('/auth/'); expect(new URL(page.url()).searchParams.get('url')).toContain('/public/Service-Management-App/index.html'); });