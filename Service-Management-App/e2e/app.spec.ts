import { expect, test } from '@playwright/test';
import { APP_URL, mockDataMinerApi, setSessionCookie } from './fixtures';

test.describe('Service Management workspace', () => {
    test.beforeEach(async ({ context, page }) => { await setSessionCookie(context); await mockDataMinerApi(page); await page.goto(APP_URL); });
    test('renders Orders and switches between the four workspace pages', { tag: ['@A1', '@A2'] }, async ({ page }) => {
        await expect(page.getByRole('heading', { name: 'Orders' })).toBeVisible();
        await expect(page.getByText('SO-1042  ·  Network refresh')).toBeVisible();
        for (const name of ['Services', 'Catalog', 'Settings']) { await page.getByRole('link', { name }).click(); await expect(page.getByRole('heading', { name, exact: true })).toBeVisible(); }
    });
    test('offers theme modes and persists an explicit dark theme', { tag: ['@T1', '@T2'] }, async ({ page }) => {
        await page.getByRole('button', { name: 'Choose theme' }).click(); await expect(page.getByRole('menuitem', { name: 'System' })).toBeVisible(); await page.getByRole('menuitem', { name: 'Dark' }).click(); await expect.poll(() => page.locator('html').getAttribute('data-theme')).toBe('dark'); await page.reload(); await expect.poll(() => page.locator('html').getAttribute('data-theme')).toBe('dark');
    });
    test('system theme follows OS preference and mobile layout stays within the viewport', { tag: ['@T3', '@R1'] }, async ({ page }) => {
        await page.emulateMedia({ colorScheme: 'dark' }); await page.getByRole('button', { name: 'Choose theme' }).click(); await page.getByRole('menuitem', { name: 'System' }).click(); await expect.poll(() => page.locator('html').getAttribute('data-theme')).toBe('dark'); await page.emulateMedia({ colorScheme: 'light' }); await expect.poll(() => page.locator('html').getAttribute('data-theme')).toBe('light'); await page.setViewportSize({ width: 390, height: 844 }); await expect(page.getByRole('heading', { name: 'Orders' })).toBeVisible(); expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBeTruthy();
    });
});