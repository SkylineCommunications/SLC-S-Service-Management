import { expect, test } from '@playwright/test';
import { createServer } from 'vite';
import type { ViteDevServer } from 'vite';

test.describe('Local booking preview', () => {
    let server: ViteDevServer;
    let url: string;
    test.beforeAll(async () => {
        server = await createServer({ mode: 'mock', server: { host: '127.0.0.1', port: 0 } });
        await server.listen();
        url = server.resolvedUrls!.local[0];
    });
    test.afterAll(async () => { await server?.close(); });

    test('creates and views workflow jobs in the local implementation demo', async ({ page }) => {
        await page.goto(`${url}#services`);
        await page.locator('.service-row').filter({ hasText: 'Implementation demo' }).getByRole('button', { name: 'Open details of Implementation demo' }).click();
        const dialog = page.getByRole('dialog', { name: 'Implementation demo' });
        await expect(dialog.getByRole('link', { name: 'View Job Existing production job' })).toBeVisible();
        await dialog.getByRole('button', { name: 'Create Job for New workflow job' }).click();
        const link = dialog.getByRole('link', { name: 'View Job Implementation demo | New workflow job' });
        await expect(link).toBeVisible();
        await expect(link).toHaveCSS('height', '32px');
        await expect(link).toHaveCSS('border-style', 'none');
        await expect(link).toHaveAttribute('title', 'Implementation demo | New workflow job');
        expect((await link.boundingBox())!.width).toBeLessThanOrEqual(200);
        expect(await link.locator('.implementation-reference-name').evaluate(element => element.scrollWidth > element.clientWidth)).toBe(true);
        await expect(dialog.getByRole('button', { name: 'Create Job for New workflow job' })).toHaveCount(0);
        await expect(dialog.getByRole('row').filter({ hasText: 'New workflow job' })).toContainText('Draft');
        const popupPromise = page.waitForEvent('popup');
        await link.click();
        const popup = await popupPromise;
        await expect(popup.getByRole('heading', { name: 'Mock workflow job' })).toBeVisible();
        await expect(popup.getByText('Implementation demo | New workflow job', { exact: true })).toBeVisible();
        await popup.close();
    });

    test('opens an off-page linked service internally and explains how to link an unlinked service', async ({ page }) => {
        await page.clock.install();
        await page.goto(`${url}#services`);
        await page.locator('.service-row').filter({ hasText: 'Implementation demo' }).getByRole('button', { name: 'Open details of Implementation demo' }).click();
        const dialog = page.getByRole('dialog', { name: 'Implementation demo' });
        await dialog.getByRole('button', { name: 'No Linked Service for Unlinked service' }).click();
        const notice = page.getByRole('status').filter({ hasText: 'Linked Service' });
        await expect(notice).toContainText('Please edit the service item and link another service.');
        await page.clock.fastForward(5000);
        await expect(notice).toBeHidden();
        await expect(page.locator('.service-row').getByRole('button', { name: 'Open details of Managed switching 1', exact: true })).toHaveCount(0);
        await dialog.getByRole('row').filter({ hasText: 'Linked service' }).getByRole('link', { name: 'Managed switching 1' }).click();
        await expect(page.getByRole('dialog', { name: 'Managed switching 1' })).toBeVisible();
        await expect(page.getByRole('dialog', { name: 'Implementation demo' })).toHaveCount(0);
    });

    test('opens the MediaOps job of a workflow item and offers no MediaOps setting', async ({ page }) => {
        await page.goto(`${url}#settings`);
        await expect(page.getByRole('heading', { name: 'Theme' })).toBeVisible();
        await expect(page.getByRole('textbox', { name: 'MediaOps job URL' })).toHaveCount(0);
        await page.goto(`${url}#services`);
        await page.locator('.service-row').filter({ hasText: 'Implementation demo' }).getByRole('button', { name: 'Open details of Implementation demo' }).click();
        const link = page.getByRole('link', { name: 'View Job Existing production job' });
        await expect(link).toHaveAttribute('href', /\/app\/ab26de0d-b0d7-456c-9c7b-155008a5cbc9\/Job%20Panel\?object%20manager%20instances=\(slc\)workflow%2F[a-f0-9-]+$/);
        await expect(link).toHaveAttribute('target', '_blank');
    });

    test('keeps short and long booking menus beside their three-dot buttons', async ({ page }) => {
        await page.goto(`${url}#services`);
        await page.locator('.service-row').filter({ hasText: 'SRM booking demo' }).getByRole('button', { name: 'Open details of SRM booking demo' }).click();
        const dialog = page.getByRole('dialog', { name: 'SRM booking demo' });
        for (const label of ['Canceled booking', 'Ongoing booking']) {
            const trigger = dialog.getByRole('button', { name: `Booking actions for ${label}` });
            await trigger.click();
            const menu = page.getByRole('menu', { name: `Booking actions for ${label}` });
            await expect(menu).toBeVisible();
            const buttonBox = (await trigger.boundingBox())!;
            const menuBox = (await menu.boundingBox())!;
            const gap = Math.min(
                Math.abs(menuBox.y - (buttonBox.y + buttonBox.height)),
                Math.abs(menuBox.y + menuBox.height - buttonBox.y),
            );
            expect(gap).toBeCloseTo(4, 0);
            expect(menuBox.x + menuBox.width).toBeCloseTo(buttonBox.x + buttonBox.width, 0);
            expect(menuBox.y).toBeGreaterThanOrEqual(12);
            expect(menuBox.y + menuBox.height).toBeLessThanOrEqual(page.viewportSize()!.height - 12);
            await trigger.click();
            await expect(menu).toBeHidden();
        }
    });

    test('shows a five-second context-menu notification for unsupported booking states', async ({ page }) => {
        await page.clock.install();
        await page.goto(`${url}#services`);
        await page.locator('.service-row').filter({ hasText: 'SRM booking demo' }).getByRole('button', { name: 'Open details of SRM booking demo' }).click();
        const dialog = page.getByRole('dialog', { name: 'SRM booking demo' });
        await dialog.getByRole('button', { name: 'Booking actions for Invoiced booking' }).click();
        const notice = page.getByRole('status').filter({ hasText: 'Context Menu' });
        await expect(notice).toContainText('No context menu options are available for the current state of the service item.');
        await expect(page.getByRole('menu')).toHaveCount(0);
        await page.clock.fastForward(5000);
        await expect(notice).toBeHidden();
        await dialog.locator('.service-items-table tbody tr').filter({ hasText: 'Draft booking' }).click({ button: 'right' });
        await expect(notice).toBeVisible();
        await expect(page.getByRole('menu')).toHaveCount(0);
    });

    test('previews booking states and creates a mock booking using the existing IAS UI', async ({ page }) => {
        await page.goto(`${url}#services`);
        await page.locator('.service-row').filter({ hasText: 'SRM booking demo' }).getByRole('button', { name: 'Open details of SRM booking demo' }).click();
        const dialog = page.getByRole('dialog', { name: 'SRM booking demo' });
        await expect(dialog.locator('.service-items-table tbody tr')).toHaveCount(13);
        await expect(dialog.getByRole('link', { name: 'View Booking Encoder 1', exact: true })).toBeVisible();
        await expect(dialog.getByRole('link', { name: 'Open monitoring service 123/100 in Monitoring (Normal)' })).toHaveAttribute('href', /\/monitoring\/service\/123\/100$/);
        await dialog.getByRole('button', { name: 'Create SRM Booking for New encoder booking' }).click();
        const wizard = page.getByRole('dialog', { name: 'Mock SRM Booking Wizard' });
        await wizard.getByRole('textbox', { name: 'Booking name' }).fill('Local encoder');
        await wizard.getByRole('button', { name: 'Create Booking', exact: true }).click();
        await expect(wizard).toBeHidden();
        await expect(dialog.getByRole('button', { name: 'Create SRM Booking for New encoder booking' })).toHaveCount(0);
        const link = dialog.getByRole('link', { name: 'View Booking Local encoder' });
        const popupPromise = page.waitForEvent('popup');
        await link.click();
        const popup = await popupPromise;
        await expect(popup.getByRole('heading', { name: 'Mock SRM booking' })).toBeVisible();
        await expect(popup.getByText('Local encoder', { exact: true })).toBeVisible();
        await popup.close();
        await dialog.getByRole('button', { name: 'Booking actions for New encoder booking' }).click();
        await page.getByRole('menuitem', { name: 'Start Booking' }).click();
        await expect(dialog.locator('.service-items-table tbody tr').filter({ hasText: 'New encoder booking' })).toContainText('Ongoing');
    });
});
