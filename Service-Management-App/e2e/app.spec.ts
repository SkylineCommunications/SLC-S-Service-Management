import { expect, test } from '@playwright/test';
import { APP_URL, SAMPLE_ORDERS, mockDataMinerApi, setSessionCookie } from './fixtures';
import type { GqiMock } from './fixtures';

const count = (mock: GqiMock, method: string) => mock.calls.filter(call => call.method === method).length;

test.describe('Service Management workspace', () => {
    let mock: GqiMock;
    test.beforeEach(async ({ context, page }) => { await setSessionCookie(context); mock = await mockDataMinerApi(page); await page.goto(APP_URL); });
    test('renders Orders and switches between the four workspace pages', { tag: ['@A1', '@A2'] }, async ({ page }) => {
        await expect(page.getByRole('heading', { name: 'Orders' })).toBeVisible();
        await expect(page.locator('.order-row').filter({ hasText: 'Network refresh' })).toBeVisible();
        for (const name of ['Services', 'Catalog', 'Settings']) { await page.getByRole('link', { name }).click(); await expect(page.getByRole('heading', { name, exact: true })).toBeVisible(); }
    });
    test('lists service orders from the Get_ServiceOrders GQI data source', { tag: ['@G1', '@G2'] }, async ({ page }) => {
        const rows = page.locator('.order-row');
        await expect(rows).toHaveCount(3);
        const network = rows.filter({ hasText: 'Network refresh' });
        for (const text of ['SO-1042', 'Skyline Broadcast', 'High priority', 'In Progress']) await expect(network).toContainText(text);
        const studio = rows.filter({ hasText: 'Studio expansion' });
        for (const text of ['7A2D3B81', 'Acme Media', 'Medium priority', 'New']) await expect(studio).toContainText(text);
        await expect(rows.filter({ hasText: 'Backup link' }).locator('.state-badge')).toHaveText('Completed');
        await expect(page.getByTestId('open-orders')).toHaveText('2');
        await expect(page.getByText('3 orders in total')).toBeVisible();
        await expect(page.getByText('8% this month')).toHaveCount(0);
        expect(count(mock, 'OpenQuerySession')).toBe(1);
        expect(count(mock, 'GetNextQuerySessionPage')).toBe(2);
        await expect.poll(() => count(mock, 'CloseQuerySession')).toBe(1);
    });
    test('refresh re-runs the GQI query', { tag: '@G3' }, async ({ page }) => {
        await expect(page.locator('.order-row')).toHaveCount(3);
        await page.getByRole('button', { name: 'Refresh', exact: true }).click();
        await expect.poll(() => count(mock, 'OpenQuerySession')).toBe(2);
        await expect.poll(() => count(mock, 'CloseQuerySession')).toBe(2);
        await expect(page.locator('.order-row')).toHaveCount(3);
        await expect(page.getByRole('button', { name: 'Refresh', exact: true })).toBeEnabled();
    });
    test('opens order details with its service order items and closes again', { tag: ['@D1', '@D2'] }, async ({ page }) => {
        await page.locator('.order-row').filter({ hasText: 'Network refresh' }).click();
        const panel = page.getByRole('dialog', { name: 'Network refresh' });
        await expect(panel).toBeVisible();
        expect(new URL(page.url()).hash).toBe(`#orders/${SAMPLE_ORDERS[0]['Order ID']}`);
        for (const text of ['SO-1042', 'Skyline Broadcast', 'Replace core switches', SAMPLE_ORDERS[0]['Order ID']]) await expect(panel).toContainText(text);
        await expect(panel.getByTestId('order-item-count')).toHaveText('2');
        const items = panel.locator('.order-item');
        await expect(items).toHaveCount(2);
        for (const text of ['Core switch A', 'In Progress', 'Add', 'Managed switching', 'Core network', '9/1/2026 8:00:00 AM']) await expect(items.first()).toContainText(text);
        await expect(items.nth(1)).not.toContainText('No Start Time');
        const itemQuery = mock.calls.find(call => call.method === 'OpenQuerySession' && call.body.query.Child.Options[0].Value.length);
        expect(itemQuery?.body.query.Child.Options[0].Value[0]).toMatchObject({ ID: 'DOM ID', Value: { Value: SAMPLE_ORDERS[0]['Order ID'] } });
        await page.keyboard.press('Escape');
        await expect(panel).toHaveCount(0);
        expect(new URL(page.url()).hash).toBe('#orders');
        await page.locator('.order-row').filter({ hasText: 'Studio expansion' }).click();
        await expect(page.getByRole('dialog', { name: 'Studio expansion' })).toContainText('This order has no items.');
        await page.getByRole('button', { name: 'Close order details' }).click();
        await expect(page.getByRole('dialog')).toHaveCount(0);
    });
    test('order details can be deep linked and show an item error with retry', { tag: ['@D3', '@D4'] }, async ({ page }) => {
        mock.itemsFail = true;
        await page.goto(`${APP_URL}#orders/${SAMPLE_ORDERS[0]['Order ID']}`);
        const panel = page.getByRole('dialog', { name: 'Network refresh' });
        await expect(panel.getByRole('alert')).toContainText('Order items could not be loaded');
        mock.itemsFail = false;
        await panel.getByRole('button', { name: 'Retry' }).click();
        await expect(panel.locator('.order-item')).toHaveCount(2);
    });
    test('offers theme modes and persists an explicit dark theme', { tag: ['@T1', '@T2'] }, async ({ page }) => {
        await page.getByRole('button', { name: 'Choose theme' }).click(); await expect(page.getByRole('menuitem', { name: 'System' })).toBeVisible(); await page.getByRole('menuitem', { name: 'Dark' }).click(); await expect.poll(() => page.locator('html').getAttribute('data-theme')).toBe('dark'); await page.reload(); await expect.poll(() => page.locator('html').getAttribute('data-theme')).toBe('dark');
    });
    test('system theme follows OS preference and mobile layout stays within the viewport', { tag: ['@T3', '@R1'] }, async ({ page }) => {
        await page.emulateMedia({ colorScheme: 'dark' }); await page.getByRole('button', { name: 'Choose theme' }).click(); await page.getByRole('menuitem', { name: 'System' }).click(); await expect.poll(() => page.locator('html').getAttribute('data-theme')).toBe('dark'); await page.emulateMedia({ colorScheme: 'light' }); await expect.poll(() => page.locator('html').getAttribute('data-theme')).toBe('light'); await page.setViewportSize({ width: 390, height: 844 }); await expect(page.getByRole('heading', { name: 'Orders' })).toBeVisible(); await expect(page.locator('.order-row')).toHaveCount(3); expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBeTruthy();
    });
});

test.describe('Orders data states', () => {
    test.beforeEach(async ({ context }) => { await setSessionCookie(context); });
    test('shows an error with retry when the GQI query fails', { tag: '@G4' }, async ({ page }) => {
        const mock = await mockDataMinerApi(page, 'error');
        await page.goto(APP_URL);
        const alert = page.getByRole('alert');
        await expect(alert).toContainText('Service orders could not be loaded');
        await expect(alert).toContainText('DOM manager is unavailable');
        await expect(page.getByTestId('open-orders')).toHaveText('–');
        mock.mode = 'ok';
        await alert.getByRole('button', { name: 'Retry' }).click();
        await expect(page.locator('.order-row')).toHaveCount(3);
        await expect(page.getByRole('alert')).toHaveCount(0);
        await expect(page.getByTestId('open-orders')).toHaveText('2');
    });
    test('shows an empty state when there are no service orders', { tag: '@G5' }, async ({ page }) => {
        await mockDataMinerApi(page, 'empty');
        await page.goto(APP_URL);
        await expect(page.getByText('No service orders yet.')).toBeVisible();
        await expect(page.getByTestId('open-orders')).toHaveText('0');
    });
    test('redirects to DataMiner authentication when the GQI call reports an expired connection', { tag: '@G6' }, async ({ page }) => {
        await mockDataMinerApi(page, 'expired');
        await page.goto(APP_URL);
        await expect.poll(() => new URL(page.url()).pathname).toBe('/auth/');
        expect(new URL(page.url()).searchParams.get('url')).toContain(APP_URL);
    });
});
