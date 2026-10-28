import { expect, test } from '@playwright/test';
import { APP_ROOT_URL, connect, disconnect, useSession, type DataMinerSession } from './session';

// Read-only checks: navigates the deployed app without creating, changing or deleting data.
test.describe('Service Management on DataMiner', () => {
    let session: DataMinerSession | undefined;

    test.beforeAll(async () => { session = await connect(); });
    test.afterAll(async () => { await disconnect(session); });

    test('redirects to DataMiner authentication without a session', async ({ page }) => {
        await page.goto(`${APP_ROOT_URL}#orders`);
        await expect.poll(() => new URL(page.url()).pathname).toBe('/auth/');
    });

    test.describe('signed in', () => {
        test.beforeEach(async ({ context }) => { await useSession(context, session!); });

        test('opens the Orders page', async ({ page }) => {
            await page.goto(`${APP_ROOT_URL}#orders`);
            await expect(page.getByRole('heading', { name: 'Orders', exact: true })).toBeVisible();
            expect(new URL(page.url()).pathname).toBe(APP_ROOT_URL);
        });

        test('navigates between the workspace pages', async ({ page }) => {
            await page.goto(`${APP_ROOT_URL}#orders`);
            const navigation = page.getByRole('navigation', { name: 'Primary navigation' });
            for (const name of ['Services', 'Catalog', 'Settings']) {
                await navigation.getByRole('link', { name }).click();
                await expect(page.getByRole('heading', { name, exact: true })).toBeVisible();
            }
        });

        test('loads the overview from live data', async ({ page }) => {
            const overviewLoaded = page.waitForResponse(response =>
                response.url().includes('/API/v1/Json.asmx/ExecuteAutomationScriptWithOutput') && response.ok());
            await page.goto(`${APP_ROOT_URL}#overview`);
            await expect(page.getByRole('heading', { name: 'Service Management', exact: true })).toBeVisible();
            await overviewLoaded;
            await expect(page.getByRole('alert')).toHaveCount(0);
        });
    });
});
