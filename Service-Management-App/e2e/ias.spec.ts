import { expect, test } from '@playwright/test';
import type { Page } from '@playwright/test';
import { APP_URL, CREATE_JOB_SCRIPT_ID, SUBSCRIPT_DEST_VARS, SUBSCRIPT_ID, mockDataMinerApi, setSessionCookie } from './fixtures';
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

    test('creates a workflow job without an item script and preserves its link when editing', async ({ page }) => {
        mock.serviceItems!.push({ Label: 'Production workflow', 'Service Item ID': '3', 'Service Item Type': 'Workflow', 'Definition Reference': 'Default', 'Implementation Reference': '', 'Service Item Script': '' });
        const drawer = await openServiceDrawer(page);
        await expect(drawer.getByRole('button', { name: 'Launch script for Production workflow' })).toHaveCount(0);
        await drawer.getByRole('button', { name: 'Create Job for Production workflow' }).click();
        const link = drawer.getByRole('link', { name: 'View Job Test service | Production workflow' });
        await expect(link).toBeVisible();
        await expect(drawer.getByRole('button', { name: 'Create Job for Production workflow' })).toHaveCount(0);
        await expect(drawer.getByRole('row').filter({ hasText: 'Production workflow' })).toContainText('Draft');
        const execute = mock.calls.filter(call => call.method === 'ExecuteAutomationScript').pop()!;
        expect(execute.body.script.Name).toBe('SLC_SM_Create Job For Service Item');
        expect(execute.body.script.Parameters.map((parameter: any) => [parameter.ParameterId, parameter.Name, parameter.Value])).toEqual([[10, 'DOM ID', '["service-1"]'], [11, 'Service Item Label', '["Production workflow"]']]);
        expect(mock.ias.messages.some(message => message.Method === 'LaunchAutomationScript' && message.Parameters.scriptID === CREATE_JOB_SCRIPT_ID)).toBe(true);
        const href = new URL((await link.getAttribute('href'))!);
        expect(href.searchParams.get('object manager instances')).toBe('(slc)workflow/11111111-1111-4111-8111-111111111111');
        await drawer.getByRole('button', { name: 'Edit Production workflow', exact: true }).click();
        const editor = page.locator('form').filter({ has: page.getByRole('heading', { name: 'Edit service item' }) });
        await editor.getByRole('textbox', { name: 'Label', exact: true }).fill('Updated workflow');
        await editor.getByRole('button', { name: 'Save', exact: true }).click();
        await expect(editor).toBeHidden();
        await expect(drawer.getByRole('link', { name: 'View Job Test service | Production workflow' })).toHaveAttribute('href', href.href);
        await expect(drawer.getByRole('button', { name: 'Create Job for Updated workflow' })).toHaveCount(0);
    });

    test('disables job creation without a workflow and surfaces creation failures without linking a job', async ({ page }) => {
        mock.serviceItems!.push(
            { Label: 'Missing workflow', 'Service Item ID': '3', 'Service Item Type': 'Workflow', 'Definition Reference': '', 'Implementation Reference': '' },
            { Label: 'Production workflow', 'Service Item ID': '4', 'Service Item Type': 'Workflow', 'Definition Reference': 'Default', 'Implementation Reference': '' },
        );
        const drawer = await openServiceDrawer(page);
        await expect(drawer.getByRole('button', { name: 'Create Job for Missing workflow' })).toBeDisabled();
        mock.ias.executeError = 'MediaOps is unavailable.';
        await drawer.getByRole('button', { name: 'Create Job for Production workflow' }).click();
        const failure = page.getByRole('dialog', { name: 'SLC_SM_Create Job For Service Item' });
        await expect(failure.getByRole('alert')).toContainText('MediaOps is unavailable.');
        await failure.getByRole('button', { name: 'Close', exact: true }).click();
        await expect(drawer.getByRole('button', { name: 'Create Job for Production workflow' })).toBeEnabled();
        await expect(drawer.getByRole('link', { name: /View Job/ })).toHaveCount(0);
        await expect(page.getByRole('status').filter({ hasText: 'completed' })).toHaveCount(0);
    });

    test('launches the item script per service item and refreshes the item after the booking is created', { tag: '@A9' }, async ({ page }) => {
        await page.clock.install();
        const drawer = await openServiceDrawer(page);
        await expect(drawer.getByRole('button', { name: 'Launch script for Monitoring link' })).toBeDisabled();
        await expect(drawer.getByRole('button', { name: 'Create SRM Booking for Encoder booking' })).toHaveAttribute('title', 'Launch SRM_Booking Wizard');
        await drawer.getByRole('button', { name: 'Create SRM Booking for Encoder booking' }).click();

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
        await expect(drawer.getByRole('row').filter({ hasText: 'Encoder booking' })).toContainText('Confirmed');
        await expect(drawer.getByRole('row').filter({ hasText: 'Encoder booking' })).not.toContainText('Not Implemented');
        await expect(drawer.getByRole('button', { name: 'Create SRM Booking for Encoder booking' })).toHaveCount(0);
        await expect(drawer.getByRole('link', { name: 'View Booking Test service' })).toHaveAttribute('href', /\/mock\/srm-booking\?id=reservation-1$/);
        expect(mock.ias.continues.pop()).toMatchObject({ _onchange: SUBSCRIPT_DEST_VARS.create, [SUBSCRIPT_DEST_VARS.create]: SUBSCRIPT_DEST_VARS.create, [SUBSCRIPT_DEST_VARS.cancel]: '' });
        Object.assign(mock.serviceItems![0], { 'Implementation State': 'Ongoing', 'Monitoring Service State': 'Critical' });
        await page.clock.fastForward(5000);
        await expect(drawer.getByRole('row').filter({ hasText: 'Encoder booking' })).toContainText('Ongoing');
        await expect(drawer.getByRole('row').filter({ hasText: 'Encoder booking' }).locator('.monitoring-severity-icon')).toHaveCSS('color', 'rgb(240, 20, 30)');
    });

    test('shows the script error dialog and closes it without a success notice', { tag: '@A10' }, async ({ page }) => {
        mock.ias.subscriptError = "No service item with label 'Encoder booking' exists under service 'Test service'.";
        const drawer = await openServiceDrawer(page);
        await drawer.getByRole('button', { name: 'Create SRM Booking for Encoder booking' }).click();
        const errorDialog = page.getByRole('dialog', { name: 'Error' });
        await expect(errorDialog).toContainText("No service item with label 'Encoder booking' exists under service 'Test service'.");
        await errorDialog.getByRole('button', { name: '+' }).click();
        await expect(errorDialog.getByRole('textbox')).toHaveValue(/System\.InvalidOperationException/);
        await errorDialog.getByRole('button', { name: 'Close', exact: true }).click();
        await expect(errorDialog).toBeHidden();
        await expect(page.getByRole('status').filter({ hasText: 'completed' })).toHaveCount(0);
        await expect(drawer).toBeVisible();
        await expect(drawer.getByRole('button', { name: 'Create SRM Booking for Encoder booking' })).toBeEnabled();
    });
});
