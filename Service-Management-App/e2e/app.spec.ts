import { expect, test } from '@playwright/test';
import { APP_ROOT_URL, APP_URL, SAMPLE_ORDERS, SAMPLE_SERVICE, mockDataMinerApi, setSessionCookie } from './fixtures';
import type { GqiMock } from './fixtures';

const count = (mock: GqiMock, method: string) => mock.calls.filter(call => call.method === method).length;

test.describe('Service Management workspace', () => {
    let mock: GqiMock;
    test.beforeEach(async ({ context, page }) => { await setSessionCookie(context); mock = await mockDataMinerApi(page); await page.goto(APP_URL); });
    test('renders Orders and switches between the four workspace pages', { tag: ['@A1', '@A2'] }, async ({ page }) => {
        await expect(page.getByRole('heading', { name: 'Orders', exact: true })).toBeVisible();
        await expect(page.locator('.order-row').filter({ hasText: 'Network refresh' })).toBeVisible();
        for (const name of ['Services', 'Catalog', 'Settings']) { await page.getByRole('link', { name }).click(); await expect(page.getByRole('heading', { name, exact: true })).toBeVisible(); }
    });
    test('does not expose the removed order IAS launcher', { tag: '@A4' }, async ({ page }) => {
        const orderRow = page.locator('.order-row').filter({ hasText: 'Network refresh' });
        await orderRow.getByRole('button', { name: 'Open details of Network refresh' }).click();
        await expect(page.getByRole('dialog').getByRole('button', { name: 'Launch test IAS', exact: true })).toHaveCount(0);
    });
    test('loads organizations into the service order form dropdown', { tag: '@W10' }, async ({ page }) => {
        await page.getByRole('button', { name: 'New order', exact: true }).click();
        const form = page.getByRole('form', { name: 'New service order' });
        const organization = form.getByRole('combobox', { name: 'Organization' });
        await expect(organization).toBeVisible();
        await expect(organization).toHaveValue('');
        await expect(organization.locator('option')).toHaveText(['No organization', 'Acme Media', 'Skyline Broadcast']);
        await form.getByRole('textbox', { name: 'Label' }).fill('Network refresh');
        await expect(form.getByText('Name already exists!', { exact: true })).toBeVisible();
        await expect(form.getByRole('button', { name: 'Create order', exact: true })).toBeDisabled();
        await form.getByRole('textbox', { name: 'Label' }).fill('New validation order');
        await expect(form.getByRole('button', { name: 'Create order', exact: true })).toBeEnabled();
        expect(mock.calls.some(call => {
            if (call.method !== 'OpenQuerySession') return false;
            const descriptor = JSON.parse(call.body.query.Options[0].Value.Value);
            return descriptor.TypeFullName === 'SLC_SM_GQIDS_Get_Service_Organizations.GetOrganizations';
        })).toBeTruthy();
    });
    test('uses fetched lists for service category, organization, and linked service fields', { tag: '@S3' }, async ({ page }) => {
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: /Services/ }).click();
        await page.getByRole('button', { name: 'Add service', exact: true }).click();
        const form = page.getByRole('form', { name: 'New service' });
        await expect(form.getByRole('combobox', { name: 'Category' }).locator('option')).toContainText(['Connectivity', 'Production']);
        await expect(form.getByRole('combobox', { name: 'Service specification' }).locator('option')).toContainText(['Managed switching']);
        await expect(form.getByRole('combobox', { name: 'Organization' }).locator('option')).toContainText(['Acme Media', 'Skyline Broadcast']);
        await form.getByRole('checkbox', { name: 'Link service' }).check();
        await expect(form.getByRole('combobox', { name: 'Linked service' }).locator('option')).not.toHaveCount(1);
    });
    test('disables service inventory creation until its validation rules pass', { tag: '@S5' }, async ({ page }) => {
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: /Services/ }).click();
        await page.getByRole('button', { name: 'Add service', exact: true }).click();
        const form = page.getByRole('form', { name: 'New service' });
        const save = form.getByRole('button', { name: 'Create', exact: true });

        await form.getByRole('textbox', { name: 'Name' }).fill('Test service');
        await expect(form.getByText('Name already exists!', { exact: true })).toBeVisible();
        await expect(save).toBeDisabled();

        await form.getByRole('textbox', { name: 'Service ID' }).fill('SVC-TEST-001');
        await expect(form.getByText('Service ID already exists!', { exact: true })).toBeVisible();
        await form.getByRole('textbox', { name: 'Service ID' }).fill('SVC-NEW-001');
        await form.getByRole('textbox', { name: 'Name' }).fill('New service');
        await expect(save).toBeEnabled();

        await form.getByLabel('Start time').fill('2020-01-01T08:00');
        await expect(form.getByText("Please make a selection which doesn't lie in the past.", { exact: true })).toBeVisible();
        await expect(save).toBeDisabled();
        await form.getByLabel('Start time').fill('2099-01-01T08:00');
        await form.getByRole('textbox', { name: /End time/ }).fill('2098-01-01T08:00');
        await expect(form.getByText('End Time must come after the start time.', { exact: true })).toBeVisible();
        await expect(save).toBeDisabled();
        await form.getByRole('textbox', { name: /End time/ }).fill('2100-01-01T08:00');

        await form.getByRole('checkbox', { name: 'Link service' }).check();
        await expect(form.getByText('Selection is mandatory!', { exact: true })).toBeVisible();
        await expect(save).toBeDisabled();
        await form.getByRole('combobox', { name: 'Linked service' }).selectOption({ label: 'Monitoring service 1' });
        await expect(save).toBeEnabled();
    });
    test('disables service item creation for duplicate labels', { tag: '@S5' }, async ({ page }) => {
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: /Services/ }).click();
        await page.locator('.service-row').filter({ hasText: 'Test service' }).getByRole('button', { name: 'Open details of Test service' }).click();
        const details = page.getByRole('dialog', { name: 'Test service' });
        await details.getByRole('button', { name: 'Add item', exact: true }).click();
        const form = page.getByRole('form', { name: 'Add service item' });
        await form.getByRole('textbox', { name: 'Label' }).fill('Encoder booking');
        await expect(form.getByText('Label already exists!', { exact: true })).toBeVisible();
        await expect(form.getByRole('button', { name: 'Add item', exact: true })).toBeDisabled();
        await form.getByRole('textbox', { name: 'Label' }).fill('');
        await expect(form.getByRole('button', { name: 'Add item', exact: true })).toBeEnabled();
    });
    test('loads the selected service organization only when editing', { tag: '@S3' }, async ({ page }) => {
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: /Services/ }).click();
        await page.locator('.service-row').filter({ hasText: 'Test service' }).getByRole('button', { name: 'Open details of Test service' }).click();
        const details = page.getByRole('dialog');
        await details.getByRole('button', { name: 'Edit', exact: true }).click();
        const form = page.getByRole('form', { name: 'Edit service' });
        await expect(form.getByRole('combobox', { name: 'Organization' })).toHaveValue('Skyline Broadcast');
        const targetedServiceCall = mock.calls.find(call => {
            if (call.method !== 'OpenQuerySession') return false;
            const descriptor = JSON.parse(call.body.query.Options[0].Value.Value);
            return descriptor.ScriptName === 'SLC_SM_GQIDS_Get Services'
                && call.body.query.Child.Options[0].Value.some((option: any) => option.ID === 'DOM ID' && option.Value.Value === 'service-1');
        });
        expect(targetedServiceCall).toBeTruthy();
    });
    test('manages service categories from the Catalog subpage', { tag: '@S3' }, async ({ page }) => {
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: /Settings/ }).click();
        await page.getByRole('link', { name: 'Manage categories', exact: true }).click();
        await expect(page.getByRole('heading', { name: 'Service Categories', exact: true })).toBeVisible();
        await page.getByRole('button', { name: 'New category', exact: true }).click();
        const form = page.getByRole('form', { name: 'New service category' });
        await form.getByRole('textbox', { name: 'Name' }).fill('Broadcast');
        await form.getByRole('textbox', { name: 'Type' }).fill('Channel');
        await form.getByRole('combobox', { name: 'Document Hub logo' }).selectOption('/Public/DocumentHub/connectivity.svg');
        await form.getByRole('button', { name: 'Create', exact: true }).click();
        await expect(page.getByText('Broadcast', { exact: true })).toBeVisible();
        expect(mock.calls.some(call => call.method === 'ExecuteAutomationScriptWithOutput' && call.body.script?.Name === 'SLC_SM_AS_ManageServiceCategory')).toBeTruthy();
    });
    test('disables service category creation until name and type are valid', { tag: '@C4' }, async ({ page }) => {
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: /Settings/ }).click();
        await page.getByRole('link', { name: 'Manage categories', exact: true }).click();
        await page.getByRole('button', { name: 'New category', exact: true }).click();
        const form = page.getByRole('form', { name: 'New service category' });
        const create = form.getByRole('button', { name: 'Create', exact: true });
        await expect(form.getByText('Name is required.', { exact: true })).toBeVisible();
        await expect(create).toBeDisabled();
        await form.getByRole('textbox', { name: 'Name' }).fill('Connectivity');
        await expect(form.getByText('Name already exists!', { exact: true })).toBeVisible();
        await expect(create).toBeDisabled();
        await form.getByRole('textbox', { name: 'Name' }).fill('Broadcast');
        await form.getByRole('textbox', { name: 'Type' }).fill('');
        await expect(form.getByText('Type is required.', { exact: true })).toBeVisible();
        await expect(create).toBeDisabled();
        await form.getByRole('textbox', { name: 'Type' }).fill('Channel');
        await expect(create).toBeEnabled();
    });
    test('disables service specification creation for duplicate names', { tag: '@C4' }, async ({ page }) => {
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: 'inventory_2 Catalog' }).click();
        await page.getByRole('button', { name: 'Add specification', exact: true }).click();
        const form = page.getByRole('form', { name: 'New service specification' });
        await form.getByRole('textbox', { name: 'Label' }).fill('Managed switching');
        await expect(form.getByText('Name already exists!', { exact: true })).toBeVisible();
        await expect(form.getByRole('button', { name: 'Create', exact: true })).toBeDisabled();
        await expect(form.getByRole('textbox', { name: 'Label' })).toHaveAttribute('placeholder', 'Specification #00002');
        await form.getByRole('textbox', { name: 'Label' }).fill('');
        await expect(form.getByRole('button', { name: 'Create', exact: true })).toBeEnabled();
    });
    test('shows related service orders in service details', { tag: '@S4' }, async ({ page }) => {
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: /Services/ }).click();
        const serviceRow = page.locator('.service-row').filter({ hasText: 'Test service' });
        await serviceRow.getByRole('button', { name: 'Open details of Test service' }).click();
        const dialog = page.getByRole('dialog');
        const related = dialog.getByRole('heading', { name: 'Related service orders' }).locator('..');
        await expect(related).toContainText('1');
        await expect(dialog).toContainText('Network refresh');
        const relatedOrdersCall = mock.calls.find(call => {
            if (call.method !== 'OpenQuerySession') return false;
            const descriptor = JSON.parse(call.body.query.Options[0].Value.Value);
            return descriptor.TypeFullName === 'SLC_SM_GQIDS_Get_Related_Service_Orders.GetRelatedServiceOrders';
        });
        expect(relatedOrdersCall?.body.query.Child.Options[0].Value).toEqual(expect.arrayContaining([
            expect.objectContaining({ ID: 'Service ID', Value: expect.objectContaining({ Value: 'service-1' }) }),
        ]));
    });
    test('loads service configuration only for the selected service when requested', { tag: '@C3' }, async ({ page }) => {
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: /Services/ }).click();
        const serviceRow = page.locator('.service-row').filter({ hasText: 'Test service' });
        const configurationCalls = () => mock.calls.filter(call => {
            if (call.method !== 'OpenQuerySession') return false;
            const descriptor = JSON.parse(call.body.query.Options[0].Value.Value);
            return descriptor.ScriptName === 'SLC_SM_GQIDS_Get Service Configurations';
        });

        expect(configurationCalls()).toHaveLength(0);
        await serviceRow.getByRole('button', { name: 'Open details of Test service' }).click();
        const dialog = page.getByRole('dialog');
        await expect(dialog.getByRole('button', { name: 'Configurations', exact: true })).toBeVisible();
        expect(configurationCalls()).toHaveLength(0);

        await dialog.getByRole('button', { name: 'Configurations', exact: true }).click();
        await expect(dialog.getByRole('heading', { name: 'Service configuration' })).toBeVisible();
        await expect.poll(() => configurationCalls().length).toBeGreaterThan(0);
        const configurationCall = configurationCalls()[0];
        expect(configurationCall.body.query.Child.Options[0].Value).toEqual(expect.arrayContaining([
            expect.objectContaining({ ID: 'DOM ID', Value: expect.objectContaining({ Value: 'service-1' }) }),
            expect.objectContaining({ ID: 'Configuration Type', Value: expect.objectContaining({ Value: 'Service' }) }),
        ]));
    });
    test('opens logo assignment from the service table icon', { tag: '@S3' }, async ({ page }) => {
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: /Services/ }).click();
        await page.getByRole('button', { name: 'Set logo for Test service' }).click();
        const form = page.getByRole('form', { name: 'Assign service logo' });
        await expect(form).toBeVisible();
        await expect(form.getByRole('combobox', { name: 'Logo category' }).locator('option')).toContainText(['Connectivity', 'Production']);
    });
    test('edits specification configurations with the shared profile editor', { tag: '@C2' }, async ({ page }) => {
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: 'inventory_2 Catalog' }).click();
        await expect(page.getByRole('heading', { name: 'Catalog', exact: true })).toBeVisible();
        const specificationRow = page.locator('.catalog-row').filter({ hasText: 'Managed switching' });
        await expect(specificationRow).toBeVisible();
        await specificationRow.getByRole('button', { name: 'Open details of Managed switching' }).click();

        const dialog = page.getByRole('dialog');
        await dialog.getByRole('button', { name: 'Configurations', exact: true }).click();
        await expect(dialog.getByRole('heading', { name: 'Specification configuration' })).toBeVisible();
        const version = dialog.getByRole('combobox', { name: 'Version' });
        const activeControl = dialog.locator('.configuration-version-active');
        await expect(activeControl).toHaveCount(1);
        await expect(activeControl).toContainText('Active');
        await expect(activeControl).toBeDisabled();
        await expect(dialog.getByRole('button', { name: 'Delete configuration', exact: true })).toBeDisabled();
        await version.selectOption({ label: 'Archived version' });
        await expect(activeControl).toContainText('Activate');
        await expect(activeControl).toBeEnabled();
        await expect(dialog.getByRole('button', { name: 'Delete configuration', exact: true })).toBeEnabled();
        await version.selectOption({ label: 'Current version (Active)' });
        await dialog.getByRole('button', { name: 'Show Value Details', exact: true }).click();
        await expect(dialog.getByRole('button', { name: 'Hide Value Details', exact: true })).toBeVisible();
        await dialog.getByRole('button', { name: 'Expand standalone parameters' }).click();
        await expect(dialog.locator('.native-configuration-table thead th')).toHaveText(['Label', 'Parameter', 'Type', 'Link', 'Value', 'Unit', 'Start', 'End', 'Step size', 'Decimals', 'Values', 'Mandatory', '']);
        await dialog.getByRole('button', { name: 'Hide Value Details', exact: true }).click();
        const polarization = dialog.getByRole('combobox', { name: 'Value for Polarization' });
        await expect(polarization).toBeVisible();
        await expect(polarization.locator('option')).toHaveText(['Select value…', 'Horizontal', 'Vertical']);
        await polarization.selectOption({ label: 'Vertical' });
        await expect(polarization).toHaveValue('Vertical');
        await dialog.getByRole('button', { name: 'Show Value Details', exact: true }).click();
        const bitrate = dialog.getByLabel('Value for Max Overall Bitrate');
        await expect(bitrate).toHaveAttribute('type', 'number');
        await bitrate.fill('42');
        await dialog.getByLabel('start for Max Overall Bitrate').fill('-500');
        await dialog.getByLabel('end for Max Overall Bitrate').fill('500');
        await dialog.getByLabel('step for Max Overall Bitrate').fill('0.5');
        await dialog.getByLabel('decimals for Max Overall Bitrate').fill('1');
        await expect(bitrate).toHaveValue('42');
        await dialog.getByRole('button', { name: 'Hide Value Details', exact: true }).click();
        await dialog.getByRole('button', { name: 'Show Lifecycle Details', exact: true }).click();
        await expect(dialog.locator('.native-configuration-table thead th')).toContainText(['N/A', 'Fixed', 'Expose at order', 'Mandatory at order', 'Mandatory at service']);
        await dialog.getByRole('button', { name: 'Hide Lifecycle Details', exact: true }).click();
        await expect(dialog.getByRole('button', { name: 'Collapse standalone parameters' })).toBeVisible();
        await expect(dialog.getByRole('button', { name: 'Collapse profiles' })).toBeVisible();
        await expect(dialog.locator('.configuration-profile-card')).toHaveCount(1);
        await expect(dialog.getByLabel('Select parameter to add')).toHaveCSS('width', '240px');
        await expect(dialog.getByLabel('Select profile to add')).toHaveCSS('width', '240px');

        const configurationCall = mock.calls.find(call => {
            if (call.method !== 'OpenQuerySession') return false;
            const descriptor = JSON.parse(call.body.query.Options[0].Value.Value);
            return descriptor.ScriptName === 'SLC_SM_GQIDS_Get Service Configurations';
        });
        expect(configurationCall?.body.query.Child.Options[0].Value).toEqual(expect.arrayContaining([
            expect.objectContaining({ ID: 'DOM ID', Value: expect.objectContaining({ Value: 'spec-1' }) }),
            expect.objectContaining({ ID: 'Configuration Type', Value: expect.objectContaining({ Value: 'Specification' }) }),
        ]));

        await dialog.getByRole('button', { name: 'Collapse profiles' }).click();
        await expect(dialog.getByRole('button', { name: 'Expand profiles' })).toBeVisible();
        await expect(dialog.locator('.configuration-profile-card')).toHaveCount(0);
        await dialog.getByRole('button', { name: 'Expand profiles' }).click();
        await dialog.getByLabel('Select profile to add').selectOption('nested-definition');
        await expect(dialog.getByLabel('Select reusable profile to add')).toBeVisible();
        await dialog.getByLabel('Select profile to add').selectOption('profile-definition');
        await dialog.locator('.native-configuration-profiles').getByRole('button', { name: 'Add', exact: true }).click();
        await expect(dialog.getByRole('button', { name: /Collapse Transport profile #2/ })).toBeVisible();
        await expect(dialog.getByLabel('Select reusable nested profile for Transport profile #2')).toHaveCount(0);
        await dialog.getByLabel('Profile name for Transport profile #2').fill('Custom transport');
        await dialog.getByLabel('Select profile definition to add below Custom transport').selectOption('nested-definition');
        await expect(dialog.getByLabel('Select reusable nested profile for Custom transport')).toBeVisible();

        await expect(dialog.getByRole('button', { name: 'Collapse profiles' })).toBeVisible();
        const profileCard = dialog.locator('.configuration-profile-card').first();
        await expect(dialog.locator('.configuration-profile-card')).toHaveCount(2);
        await expect(profileCard.getByLabel('Profile name for Transport profile')).toHaveValue('Transport profile');
        await expect(profileCard.locator('.configuration-profile-detail')).toHaveCount(0);

        await profileCard.getByRole('button', { name: 'Expand Transport profile', exact: true }).click();
        await dialog.getByRole('button', { name: 'Open Reusable encapsulation profile' }).click();
        await expect(dialog.getByRole('heading', { name: 'Reusable encapsulation profile' })).toBeVisible();
        await expect(dialog.getByRole('button', { name: 'Delete profile parameter Symbol Rate' })).toHaveCount(0);
        await expect(dialog).not.toContainText(/Level \d/);

        await dialog.getByRole('button', { name: 'Back to Transport profile' }).click();
        await expect(dialog.locator('.native-configuration-profile-view')).toHaveCount(0);
        await expect(dialog.locator('.configuration-profile-card')).toHaveCount(2);
        await expect(dialog.getByRole('button', { name: /^(Expand|Collapse) Transport profile$/ })).toBeVisible();
    });
    test('keeps a new configuration version inactive until activation', { tag: '@C7' }, async ({ page }) => {
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: 'inventory_2 Catalog' }).click();
        await page.locator('.catalog-row').filter({ hasText: 'Managed switching' }).getByRole('button', { name: 'Open details of Managed switching' }).click();
        const dialog = page.getByRole('dialog');
        await dialog.getByRole('button', { name: 'Configurations', exact: true }).click();
        await expect(dialog.getByRole('heading', { name: 'Specification configuration' })).toBeVisible();
        await dialog.getByRole('button', { name: 'New version', exact: true }).click();
        await expect(dialog.getByText('Inactive until Activate', { exact: true })).toBeVisible();
        await expect(dialog.getByRole('button', { name: 'Delete configuration', exact: true })).toBeDisabled();
    });
    test('manages service operations through the Automation script contract', { tag: '@W8' }, async ({ page }) => {
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: /Services/ }).click();
        const serviceRow = page.locator('.service-row').filter({ hasText: 'Test service' });
        await expect(serviceRow).toBeVisible();
        await serviceRow.getByRole('button', { name: 'Open details of Test service' }).click();

        const panel = page.getByRole('dialog');
        await panel.getByRole('button', { name: 'Operations', exact: true }).click();
        await expect(panel.locator('tbody tr')).toHaveCount(2);

        await panel.getByRole('button', { name: 'Add', exact: true }).click();
        const addForm = page.getByRole('form', { name: 'Add service operation' });
        await addForm.getByRole('textbox', { name: 'Script name' }).fill('Provision service');
        await addForm.getByRole('textbox', { name: 'Description' }).fill('Provisioning operation');
        await addForm.getByRole('button', { name: 'Add operation' }).click();
        await expect(panel.getByText('Provisioning operation', { exact: true })).toBeVisible();

        const getParameter = (call: any, name: string) => {
            const parameter = call.body.script.Parameters.find((candidate: any) => candidate.Name === name);
            return JSON.parse(parameter.Value)[0];
        };
        const addCall = mock.calls.filter(call => call.method === 'ExecuteAutomationScriptWithOutput').at(-1);
        expect(addCall?.body.script.Name).toBe('SLC_SM_IAS_Manage Service Scripts');
        expect(getParameter(addCall, 'Action')).toBe('Add');
        expect(getParameter(addCall, 'Service Id')).toBe(SAMPLE_SERVICE['DOM ID']);
        expect(getParameter(addCall, 'Script Name')).toBe('Provision service');
        expect(getParameter(addCall, 'Script Description')).toBe('Provisioning operation');
        expect(getParameter(addCall, 'Original Script Description')).toBe('Provisioning operation');
        expect(getParameter(addCall, 'Payload')).toBe('{}');

        await panel.getByRole('button', { name: 'Edit operation Provisioning operation' }).click();
        const editForm = page.getByRole('form', { name: 'Edit service operation' });
        await editForm.getByRole('textbox', { name: 'Description' }).fill('Provisioning updated');
        await editForm.getByRole('button', { name: 'Save operation' }).click();
        await expect(panel.getByText('Provisioning updated', { exact: true })).toBeVisible();
        await expect(panel.getByText('Provisioning operation', { exact: true })).toHaveCount(0);
        const updateCall = mock.calls.filter(call => call.method === 'ExecuteAutomationScriptWithOutput').at(-1);
        expect(getParameter(updateCall, 'Action')).toBe('Update');
        expect(getParameter(updateCall, 'Original Script Description')).toBe('Provisioning operation');
        expect(getParameter(updateCall, 'Script Description')).toBe('Provisioning updated');

        await panel.getByRole('button', { name: 'Delete operation Provisioning updated' }).click();
        const confirmation = page.getByRole('alertdialog', { name: 'Confirm deletion' });
        await confirmation.getByRole('button', { name: 'Delete', exact: true }).click();
        await expect(panel.getByText('Provisioning updated', { exact: true })).toHaveCount(0);
        const removeCall = mock.calls.filter(call => call.method === 'ExecuteAutomationScriptWithOutput').at(-1);
        expect(getParameter(removeCall, 'Action')).toBe('Remove');
        expect(getParameter(removeCall, 'Original Script Description')).toBe('Provisioning updated');
        expect(mock.calls.filter(call => call.method === 'OpenQuerySession').length).toBeGreaterThanOrEqual(4);
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
        await page.locator('.order-row').filter({ hasText: 'Network refresh' }).getByRole('button', { name: 'Open details of Network refresh' }).click();
        const panel = page.getByRole('dialog', { name: 'Network refresh' });
        await expect(panel).toBeVisible();
        expect(new URL(page.url()).hash).toBe(`#orders/${SAMPLE_ORDERS[0]['Order ID']}`);
        for (const text of ['SO-1042', 'Skyline Broadcast', 'Replace core switches']) await expect(panel).toContainText(text);
        await expect(panel.getByText('DOM instance ID', { exact: true })).toHaveCount(0);
        await expect(panel.locator('.order-transition-action')).not.toHaveCount(0);
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
        await page.locator('.order-row').filter({ hasText: 'Studio expansion' }).getByRole('button', { name: 'Open details of Studio expansion' }).click();
        await expect(page.getByRole('dialog', { name: 'Studio expansion' })).toContainText('This order has no items.');
        await page.getByRole('button', { name: 'Close order details' }).click();
        await expect(page.getByRole('dialog')).toHaveCount(0);
    });
    test('blocks service initialization until the specification is configured', { tag: '@W3' }, async ({ page }) => {
        await page.locator('.order-row').filter({ hasText: 'Network refresh' }).getByRole('button', { name: 'Open details of Network refresh' }).click();
        const panel = page.getByRole('dialog', { name: 'Network refresh' });
        const item = panel.locator('.order-item').filter({ hasText: 'Core switch B' });
        const initialize = item.getByRole('button', { name: 'Initialize service for Core switch B' });
        await expect(initialize).toBeEnabled();
        await initialize.click();
        await expect(item.getByRole('note')).toContainText('Configure the service specification before initializing a service.');
        await expect(initialize).toBeDisabled();
        await expect.poll(() => mock.calls.filter(call => call.method === 'ExecuteAutomationScriptWithOutput' && call.body.script?.Name === 'SLC_SM_Create Service Inventory Item')).toHaveLength(0);
        expect(mock.calls.some(call => {
            if (call.method !== 'OpenQuerySession') return false;
            const descriptor = JSON.parse(call.body.query.Options[0].Value.Value);
            return descriptor.ScriptName === 'SLC_SM_GQIDS_Get Service Configurations'
                && call.body.query.Child.Options[0].Value.some((option: any) => option.ID === 'DOM ID' && option.Value.Value === 'unconfigured-spec');
        })).toBeTruthy();
    });
    test('disables order-item creation until IAS-required selections are valid', { tag: '@D5' }, async ({ page }) => {
       await page.locator('.order-row').filter({ hasText: 'Network refresh' }).getByRole('button', { name: 'Open details of Network refresh' }).click();
       const orderDialog = page.getByRole('dialog', { name: 'Network refresh' });
       await orderDialog.getByRole('button', { name: 'Add item', exact: true }).click();
       const itemForm = page.getByRole('form', { name: 'Add service order item' });
       await expect(itemForm.getByRole('button', { name: 'Add item', exact: true })).toBeDisabled();
       await expect(itemForm.getByText('Selection is mandatory!', { exact: true })).toBeVisible();
       await itemForm.getByRole('combobox', { name: 'Service specification' }).selectOption({ label: 'Managed switching' });
       await expect(itemForm.getByRole('button', { name: 'Add item', exact: true })).toBeEnabled();
    });
    test('order details can be deep linked and show an item error with retry', { tag: ['@D3', '@D4'] }, async ({ page }) => {
        mock.itemsFail = true;
        await page.goto(`${APP_ROOT_URL}#orders/${SAMPLE_ORDERS[0]['Order ID']}`);
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
        await page.emulateMedia({ colorScheme: 'dark' }); await page.reload(); await page.getByRole('button', { name: 'Choose theme' }).click(); await page.getByRole('menuitem', { name: 'System' }).click(); await expect.poll(() => page.locator('html').getAttribute('data-theme')).toBe('dark'); await page.emulateMedia({ colorScheme: 'light' }); await expect.poll(() => page.locator('html').getAttribute('data-theme')).toBe('light'); await page.setViewportSize({ width: 390, height: 844 }); await expect(page.getByRole('heading', { name: 'Orders', exact: true })).toBeVisible(); await expect(page.locator('.order-row')).toHaveCount(3); expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBeTruthy();
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
        expect(new URL(page.url()).searchParams.get('url')).toContain(APP_ROOT_URL);
    });
});
