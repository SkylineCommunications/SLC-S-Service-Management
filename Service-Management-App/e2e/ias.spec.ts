import { expect, test } from '@playwright/test';
import type { Page } from '@playwright/test';
import { APP_URL, SUBSCRIPT_DEST_VARS, SUBSCRIPT_ID, mockDataMinerApi, setSessionCookie } from './fixtures';
import type { GqiMock } from './fixtures';

test.describe('Service item interactive subscript', () => {
    let mock: GqiMock;
    test.beforeEach(async ({ context, page }) => { await setSessionCookie(context); mock = await mockDataMinerApi(page); await page.goto(APP_URL); });

    async function openServiceDrawer(page: Page) {
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: /Services/ }).click();
        await page.locator('.service-row').filter({ hasText: 'Test service' }).getByRole('button', { name: 'Open details of Test service' }).click();
        const drawer = page.getByRole('dialog', { name: 'Test service' });
        await expect(drawer.getByRole('row').filter({ hasText: 'Encoder booking' })).toContainText('Not Implemented');
        return drawer;
    }

    test('launches the item script per service item and refreshes the item after the booking is created', { tag: '@A9' }, async ({ page }) => {
        const drawer = await openServiceDrawer(page);
        await expect(drawer.getByRole('button', { name: 'Launch script for Monitoring link' })).toBeDisabled();
        await expect(drawer.getByRole('button', { name: 'Launch script for Encoder booking' })).toHaveAttribute('title', 'Launch SRM_Booking Wizard');
        await drawer.getByRole('button', { name: 'Launch script for Encoder booking' }).click();

        const wizard = page.getByRole('dialog', { name: 'Encoding Booking Wizard' });
        await expect(wizard.getByText('Service item · Encoder booking')).toBeVisible();
        await expect(wizard.getByRole('textbox', { name: 'Booking name' })).toHaveValue('Test service');
        const execute = mock.calls.filter(call => call.method === 'ExecuteAutomationScript').pop()!;
        expect(execute.body.script).toMatchObject({ Name: 'Launch Interactive Subscript', Folder: 'Service Management', Settings: { RequireInteractive: true, HasFindInteractiveClient: false } });
        expect(execute.body.script.Parameters.map((parameter: any) => [parameter.ParameterId, parameter.Name, parameter.Value])).toEqual([[3, 'DOM ID', '["service-1"]'], [4, 'Item Label', '["Encoder booking"]']]);
        expect(mock.ias.messages.find(message => message.Method === 'LaunchAutomationScript')?.Parameters.scriptID).toBe(SUBSCRIPT_ID);

        await wizard.getByRole('button', { name: 'Create Booking' }).click();
        await expect(wizard).toBeHidden();
        await expect(page.getByRole('status').filter({ hasText: 'Script for "Encoder booking" completed.' })).toBeVisible();
        await expect(drawer.getByRole('row').filter({ hasText: 'Encoder booking' })).toContainText('Implemented');
        await expect(drawer.getByRole('row').filter({ hasText: 'Encoder booking' })).not.toContainText('Not Implemented');
        expect(mock.ias.continues.pop()).toMatchObject({ _onchange: SUBSCRIPT_DEST_VARS.create, [SUBSCRIPT_DEST_VARS.create]: SUBSCRIPT_DEST_VARS.create, [SUBSCRIPT_DEST_VARS.cancel]: '' });
    });

    test('shows the script error dialog and closes it without a success notice', { tag: '@A10' }, async ({ page }) => {
        mock.ias.subscriptError = "No service item with label 'Encoder booking' exists under service 'Test service'.";
        const drawer = await openServiceDrawer(page);
        await drawer.getByRole('button', { name: 'Launch script for Encoder booking' }).click();
        const errorDialog = page.getByRole('dialog', { name: 'Error' });
        await expect(errorDialog).toContainText("No service item with label 'Encoder booking' exists under service 'Test service'.");
        await errorDialog.getByRole('button', { name: '+' }).click();
        await expect(errorDialog.getByRole('textbox')).toHaveValue(/System\.InvalidOperationException/);
        await errorDialog.getByRole('button', { name: 'Close', exact: true }).click();
        await expect(errorDialog).toBeHidden();
        await expect(page.getByRole('status').filter({ hasText: 'completed' })).toHaveCount(0);
        await expect(drawer).toBeVisible();
        await expect(drawer.getByRole('button', { name: 'Launch script for Encoder booking' })).toBeEnabled();
    });
});
