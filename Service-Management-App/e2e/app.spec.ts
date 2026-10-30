import { expect, test } from '@playwright/test';
import { APP_ROOT_URL, APP_URL, SAMPLE_ORDERS, SAMPLE_SERVICE, mockDataMinerApi, setSessionCookie } from './fixtures';
import type { GqiMock } from './fixtures';

const count = (mock: GqiMock, method: string) => mock.calls.filter(call => call.method === method).length;
const udapiRequests = (mock: GqiMock) => mock.calls
    .filter(call => call.method === 'ExecuteAutomationScriptWithOutput' && call.body.script?.Name === 'SLC_SM_UDAPI_ServiceManagement')
    .map(call => JSON.parse(call.body.script.Parameters.find((parameter: any) => parameter.Name === 'Request')?.Value ?? '{}'));
const ORDERS_ROUTE = 'service-management/v1/serviceOrder';
const SERVICES_ROUTE = 'service-management/v1/service';
const orderListCount = (mock: GqiMock) => udapiRequests(mock).filter(request => request.Route === ORDERS_ROUTE && request.RequestMethod === 1).length;

test.describe('Service Management workspace', () => {
    let mock: GqiMock;
    test.beforeEach(async ({ context, page }) => { await setSessionCookie(context); mock = await mockDataMinerApi(page); await page.goto(APP_URL); });
    test('renders Orders and switches between the four workspace pages', { tag: ['@A1', '@A2'] }, async ({ page }) => {
        await expect(page.getByRole('heading', { name: 'Orders', exact: true })).toBeVisible();
        await expect(page.locator('.order-row').filter({ hasText: 'Network refresh' })).toBeVisible();
        for (const name of ['Services', 'Catalog', 'Settings']) { await page.getByRole('link', { name }).click(); await expect(page.getByRole('heading', { name, exact: true })).toBeVisible(); }
    });
    test('loads the overview through bounded summary routes', { tag: '@G7' }, async ({ page }) => {
        await page.goto(`${APP_ROOT_URL}#overview`);
        await expect(page.getByRole('heading', { name: 'Service Management', exact: true })).toBeVisible();
        const overviewRequests = udapiRequests(mock).filter(request => String(request.Route).endsWith('/overview'));
        expect(overviewRequests.map(request => request.Route).sort()).toEqual([
            'service-management/v1/service/overview',
            'service-management/v1/serviceOrder/overview',
            'service-management/v1/serviceSpecification/overview',
        ]);
        expect(overviewRequests.every(request => request.QueryParameters?.limit?.[0] === '6')).toBeTruthy();
    });
    test('uses cursor pagination for specifications and shows the maximum page', { tag: '@G8' }, async ({ page }) => {
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: 'Catalog' }).click();
        const pagination = page.locator('.table-pagination');
        await expect(pagination.getByText('Page 1 of 2', { exact: true })).toBeVisible();
        const specificationRequests = () => udapiRequests(mock).filter(request => request.Route === 'service-management/v1/serviceSpecification' && request.RequestMethod === 1);
        expect(specificationRequests().at(-1).QueryParameters.pagination).toEqual(['cursor']);
        expect(specificationRequests().at(-1).QueryParameters.includeTotalCount).toEqual(['true']);
        await pagination.getByRole('button', { name: 'Next', exact: true }).click();
        await expect(pagination.getByText('Page 2 of 2', { exact: true })).toBeVisible();
        expect(specificationRequests().at(-1).QueryParameters.after?.[0]).toBeTruthy();
        expect(specificationRequests().at(-1).QueryParameters.includeTotalCount).toEqual(['false']);
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

        await form.getByRole('textbox', { name: 'Service ID' }).fill('SERVICE-1');
        await expect(form.getByText('Service ID already exists!', { exact: true })).toBeVisible();
        await form.getByRole('textbox', { name: 'Service ID' }).fill('SVC-NEW-001');
        await expect(form.getByText('Service ID must look like SERVICE-123.', { exact: true })).toBeVisible();
        await expect(save).toBeDisabled();
        await form.getByRole('textbox', { name: 'Service ID' }).fill('SERVICE-2');
        await form.getByRole('textbox', { name: 'Name' }).fill('New service');
        await expect(save).toBeEnabled();

        await form.getByLabel('Start date').fill('2020-01-01T08:00');
        await expect(form.getByText("Please make a selection which doesn't lie in the past.", { exact: true })).toBeVisible();
        await expect(save).toBeDisabled();
        await form.getByLabel('Start date').fill('2099-01-01T08:00');
        await form.getByRole('textbox', { name: /End date/ }).fill('2098-01-01T08:00');
        await expect(form.getByText('End date must come after the start date.', { exact: true })).toBeVisible();
        await expect(save).toBeDisabled();
        await form.getByRole('textbox', { name: /End date/ }).fill('2100-01-01T08:00');

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
    test('lists, adds, edits and deletes service items through the UDAPI item routes', { tag: ['@S2', '@S5'] }, async ({ page }) => {
        const itemsRoute = `${SERVICES_ROUTE}/SERVICE-1/item`;
        const lastRequest = (method: number) => udapiRequests(mock).filter(request => request.RequestMethod === method && String(request.Route).startsWith(itemsRoute)).at(-1);
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: /Services/ }).click();
        await page.locator('.service-row').filter({ hasText: 'Test service' }).getByRole('button', { name: 'Open details of Test service' }).click();
        const details = page.getByRole('dialog', { name: 'Test service' });
        const rows = details.locator('.service-items-table tbody tr');
        await expect(rows.filter({ hasText: 'Encoder booking' })).toContainText('SRM Booking');
        await expect(rows.filter({ hasText: 'Encoder booking' })).toContainText('Not Implemented');
        expect(lastRequest(1).Route).toBe(itemsRoute);

        await details.getByRole('button', { name: 'Add item', exact: true }).click();
        const addForm = page.getByRole('form', { name: 'Add service item' });
        await addForm.getByRole('textbox', { name: 'Label' }).fill('Playout workflow');
        await addForm.getByRole('combobox', { name: 'Type' }).selectOption('Workflow');
        await addForm.getByRole('textbox', { name: 'Definition reference' }).fill('Playout');
        await addForm.getByRole('button', { name: 'Add item', exact: true }).click();
        await expect(rows.filter({ hasText: 'Playout workflow' })).toContainText('Workflow');
        expect(JSON.parse(lastRequest(3).Body)).toEqual({ label: 'Playout workflow', type: 'Workflow', definitionReference: 'Playout', implementationReference: '', script: '' });

        await details.getByRole('button', { name: 'Edit Playout workflow', exact: true }).click();
        const editForm = page.getByRole('form', { name: 'Edit service item' });
        await editForm.getByRole('textbox', { name: 'Label' }).fill('Playout workflow 2');
        await editForm.getByRole('button', { name: 'Save', exact: true }).click();
        await expect(rows.filter({ hasText: 'Playout workflow 2' })).toBeVisible();
        expect(lastRequest(5).Route).toBe(`${itemsRoute}/3`);
        expect(JSON.parse(lastRequest(5).Body)).toMatchObject({ label: 'Playout workflow 2', type: 'Workflow' });

        await details.getByRole('button', { name: 'Delete Playout workflow 2', exact: true }).click();
        await page.getByRole('alertdialog', { name: 'Confirm deletion' }).getByRole('button', { name: 'Delete', exact: true }).click();
        await expect(rows.filter({ hasText: 'Playout workflow 2' })).toHaveCount(0);
        expect(lastRequest(4).Route).toBe(`${itemsRoute}/3`);
        expect(mock.calls.some(call => /^SLC_SM_(IAS_Add Service Item|Delete Service Item)$/.test(call.body.script?.Name ?? ''))).toBeFalsy();
        expect(mock.calls.some(call => call.method === 'OpenQuerySession' && JSON.parse(call.body.query?.Options?.[0]?.Value?.Value ?? '{}').ScriptName === 'SLC_SM_GQIDS_Get Service Items')).toBeFalsy();
    });
    test('keeps the background inert for the service item IAS dialog and restores focus when closed', { tag: '@A5' }, async ({ page }) => {
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: /Services/ }).click();
        await page.locator('.service-row').filter({ hasText: 'Test service' }).getByRole('button', { name: 'Open details of Test service' }).click();
        const details = page.getByRole('dialog', { name: 'Test service' });
        const launch = details.getByRole('button', { name: 'Launch script for Encoder booking' });
        await launch.focus();
        await launch.click();

        const iasDialog = page.getByRole('dialog', { name: 'Encoding Booking Wizard' });
        await expect(iasDialog).toBeVisible();
        await expect.poll(() => iasDialog.evaluate(dialog => dialog.contains(document.activeElement))).toBe(true);

        expect(await iasDialog.evaluate(dialog => dialog.matches(':modal'))).toBe(true);
        const backgroundLink = page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: /Services/ });
        await backgroundLink.focus();
        await expect(backgroundLink).not.toBeFocused();

        await iasDialog.getByRole('button', { name: 'Create Booking', exact: true }).focus();
        await page.keyboard.press('Tab');
        await expect.poll(() => iasDialog.evaluate(dialog => {
            const active = dialog.ownerDocument.activeElement;
            return active === dialog.ownerDocument.body || dialog.contains(active);
        })).toBe(true);
        await expect(backgroundLink).not.toBeFocused();
        await page.keyboard.press('Tab');
        await expect(iasDialog.getByRole('button', { name: 'Close interactive script' })).toBeFocused();

        await page.keyboard.press('Escape');
        await expect(iasDialog).not.toBeVisible();
        await expect(details).toBeVisible();
        await expect(launch).toBeFocused();
    });
    test('loads the selected service organization only when editing', { tag: '@S3' }, async ({ page }) => {
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: /Services/ }).click();
        await page.locator('.service-row').filter({ hasText: 'Test service' }).getByRole('button', { name: 'Open details of Test service' }).click();
        const details = page.getByRole('dialog');
        await details.getByRole('button', { name: 'Edit', exact: true }).click();
        const form = page.getByRole('form', { name: 'Edit service' });
        await expect(form.getByRole('combobox', { name: 'Organization' })).toHaveValue('Skyline Broadcast');
        const serviceReads = udapiRequests(mock).filter(request => request.RequestMethod === 1);
        expect(serviceReads.filter(request => request.Route === SERVICES_ROUTE)).toHaveLength(1);
        expect(serviceReads.find(request => request.Route === `${SERVICES_ROUTE}/SERVICE-1`)).toMatchObject({
            QueryParameters: { fields: ['OrganizationId,OrganizationName'] },
        });
        expect(mock.calls.some(call => call.method === 'OpenQuerySession' && JSON.parse(call.body.query.Options[0].Value.Value).ScriptName === 'SLC_SM_GQIDS_Get Services')).toBeFalsy();
    });
    test('loads lightweight service summaries and defers configuration until the tab opens', { tag: '@S3' }, async ({ page }) => {
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: /Services/ }).click();
        const row = page.locator('.service-row').filter({ hasText: 'Test service' });
        await expect(row).toBeVisible();

        const listRequest = udapiRequests(mock).find(request => request.Route === SERVICES_ROUTE && request.RequestMethod === 1);
        const fields = listRequest.QueryParameters.fields.join(',');
        expect(fields).toContain('Name');
        expect(fields).toContain('StatusName');
        expect(fields).toContain('CategoryName');
        expect(fields).not.toMatch(/ServiceItems|ServiceItemRelationships|ServiceConfigurationId|ConfigurationVersions|ServiceScripts|OrganizationId|OrganizationName/);
        expect(udapiRequests(mock).some(request => request.Route === `${SERVICES_ROUTE}/SERVICE-1/configuration`)).toBeFalsy();

        await row.getByRole('button', { name: 'Open details of Test service' }).click();
        const details = page.getByRole('dialog');
        await expect(details).toBeVisible();
        await expect.poll(() => udapiRequests(mock).some(request => request.Route === `${SERVICES_ROUTE}/SERVICE-1/item`)).toBeTruthy();
        await expect.poll(() => udapiRequests(mock).some(request => request.Route === `${SERVICES_ROUTE}/SERVICE-1/relatedOrder`)).toBeTruthy();
        await expect.poll(() => udapiRequests(mock).some(request => request.Route === `${SERVICES_ROUTE}/SERVICE-1/script`)).toBeTruthy();
        expect(udapiRequests(mock).some(request => request.Route === `${SERVICES_ROUTE}/SERVICE-1/configuration`)).toBeFalsy();

        await details.getByRole('button', { name: 'Configurations', exact: true }).click();
        await expect.poll(() => udapiRequests(mock).some(request => request.Route === `${SERVICES_ROUTE}/SERVICE-1/configuration`)).toBeTruthy();
    });
    test('creates, edits, re-logos and transitions services through the UDAPI service routes', { tag: ['@S3', '@S6'] }, async ({ page }) => {
        const lastRequest = (method: number) => udapiRequests(mock).filter(request => request.RequestMethod === method && String(request.Route).startsWith(SERVICES_ROUTE)).at(-1);
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: /Services/ }).click();

        await page.getByRole('button', { name: 'Add service', exact: true }).click();
        const createForm = page.getByRole('form', { name: 'New service' });
        await createForm.getByRole('textbox', { name: 'Service ID' }).fill('SERVICE-7');
        await createForm.getByRole('textbox', { name: 'Name' }).fill('UDAPI service');
        await createForm.getByRole('combobox', { name: 'Category' }).selectOption('Production');
        await createForm.getByRole('combobox', { name: 'Service specification' }).selectOption('Managed switching');
        await createForm.getByRole('combobox', { name: 'Organization' }).selectOption('Acme Media');
        await createForm.getByRole('checkbox', { name: 'Indefinite (no end date)' }).check();
        await createForm.getByRole('checkbox', { name: 'Link service' }).check();
        await createForm.getByRole('combobox', { name: 'Linked service' }).selectOption({ label: 'Monitoring service 1' });
        await createForm.getByRole('button', { name: 'Create', exact: true }).click();
        await expect(createForm).toBeHidden();
        const create = lastRequest(3);
        expect(create.Route).toBe(SERVICES_ROUTE);
        expect(JSON.parse(create.Body)).toMatchObject({ serviceId: 'SERVICE-7', name: 'UDAPI service', category: { name: 'Production' }, serviceSpecificationId: 'spec-1', organizationId: 'org-1', endTime: null, generateMonitoringService: true, monitoringService: 'monitoring-1' });
        const createdRow = page.locator('.service-row').filter({ hasText: 'UDAPI service' });
        await expect(createdRow).toBeVisible();

        await createdRow.getByRole('button', { name: 'Open details of UDAPI service' }).click();
        const details = page.getByRole('dialog', { name: 'UDAPI service' });
        await details.getByRole('button', { name: 'Edit', exact: true }).click();
        const editForm = page.getByRole('form', { name: 'Edit service' });
        await editForm.getByRole('textbox', { name: 'Name' }).fill('UDAPI service renamed');
        await editForm.getByRole('combobox', { name: 'Organization' }).selectOption('');
        await editForm.getByRole('button', { name: 'Save', exact: true }).click();
        await expect(editForm).toBeHidden();
        const edit = lastRequest(5);
        expect(edit.Route).toBe(`${SERVICES_ROUTE}/SERVICE-7`);
        const editBody = JSON.parse(edit.Body);
        expect(editBody).toMatchObject({ name: 'UDAPI service renamed', organizationId: null });
        expect(editBody).not.toHaveProperty('serviceId');
        expect(editBody).not.toHaveProperty('serviceSpecificationId');

        await page.getByRole('dialog', { name: 'UDAPI service renamed' }).getByRole('button', { name: 'Design', exact: true }).click();
        await expect.poll(() => lastRequest(3)?.Route).toBe(`${SERVICES_ROUTE}/SERVICE-7/transition`);
        expect(JSON.parse(lastRequest(3).Body)).toEqual({ targetStatus: 'Designed' });

        await page.keyboard.press('Escape');
        await page.getByRole('button', { name: 'Set logo for UDAPI service renamed' }).click();
        const logoForm = page.getByRole('form', { name: 'Assign service logo' });
        await logoForm.getByRole('combobox', { name: 'Logo category' }).selectOption('Connectivity');
        await logoForm.getByRole('button', { name: 'Apply logo', exact: true }).click();
        await expect(logoForm).toBeHidden();
        expect(lastRequest(5).Route).toBe(`${SERVICES_ROUTE}/SERVICE-7`);
        expect(JSON.parse(lastRequest(5).Body)).toEqual({ category: { name: 'Connectivity' } });
        expect(mock.calls.some(call => /^SLC_SM_(Create Service Inventory Item|Service_StateTransitions)$/.test(call.body.script?.Name ?? ''))).toBeFalsy();
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
        const udapiCall = mock.calls.find(call => call.method === 'ExecuteAutomationScriptWithOutput' && call.body.script?.Name === 'SLC_SM_UDAPI_ServiceManagement' && JSON.parse(call.body.script.Parameters?.[0]?.Value ?? '{}').RequestMethod === 3);
        expect(udapiCall).toBeTruthy();
        const request = JSON.parse(udapiCall!.body.script.Parameters[0].Value);
        expect(request).toMatchObject({ Route: 'service-management/v1/serviceCategory', RouteParameters: {} });
        expect(JSON.parse(request.Body)).toEqual({ name: 'Broadcast', type: 'Channel', icon: '/Public/DocumentHub/connectivity.svg' });
        expect(udapiCall!.body.script.Parameters[0]).toMatchObject({ ParameterId: 2, Name: 'Request' });
        const logoScript = mock.calls.find(call => call.method === 'ExecuteAutomationScriptWithOutput'
            && call.body.script?.Name === 'SLC_SM_AS_GetDocumentHubLogos');
        expect(logoScript).toBeTruthy();
        expect(logoScript!.body.script.Parameters).toEqual(expect.arrayContaining([
            expect.objectContaining({ ParameterId: 2, Name: 'Bucket Name' }),
            expect.objectContaining({ ParameterId: 3, Name: 'Name Filter' }),
        ]));
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
        await expect(form.getByRole('textbox', { name: 'Label' })).toHaveAttribute('placeholder', 'Specification #00013');
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
        expect(udapiRequests(mock)).toContainEqual(expect.objectContaining({
            Route: `${SERVICES_ROUTE}/SERVICE-1/relatedOrder`,
            RequestMethod: 1,
        }));
        expect(mock.calls.some(call => {
            if (call.method !== 'OpenQuerySession') return false;
            const descriptor = JSON.parse(call.body.query.Options[0].Value.Value);
            return descriptor.TypeFullName === 'SLC_SM_GQIDS_Get_Related_Service_Orders.GetRelatedServiceOrders';
        })).toBeFalsy();
    });
    test('loads service configuration only for the selected service when requested', { tag: '@C3' }, async ({ page }) => {
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: /Services/ }).click();
        const serviceRow = page.locator('.service-row').filter({ hasText: 'Test service' });
        const configurationRoute = `${SERVICES_ROUTE}/SERVICE-1/configuration`;
        const configurationRequests = () => udapiRequests(mock).filter(request => request.Route === configurationRoute);

        expect(configurationRequests()).toHaveLength(0);
        await serviceRow.getByRole('button', { name: 'Open details of Test service' }).click();
        const dialog = page.getByRole('dialog');
        await expect(dialog.getByRole('button', { name: 'Configurations', exact: true })).toBeVisible();
        expect(configurationRequests()).toHaveLength(0);

        await dialog.getByRole('button', { name: 'Configurations', exact: true }).click();
        await expect(dialog.getByRole('heading', { name: 'Service configuration' })).toBeVisible();
        await expect.poll(() => configurationRequests().length).toBeGreaterThan(0);
        expect(configurationRequests()[0]).toMatchObject({ RequestMethod: 1, Route: configurationRoute });
        expect(mock.calls.some(call => {
            if (call.method !== 'OpenQuerySession') return false;
            const descriptor = JSON.parse(call.body.query.Options[0].Value.Value);
            return descriptor.ScriptName === 'SLC_SM_GQIDS_Get Service Configurations';
        })).toBeFalsy();
    });
    test('saves service configurations through the UDAPI PUT route', { tag: '@C3' }, async ({ page }) => {
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: /Services/ }).click();
        const serviceRow = page.locator('.service-row').filter({ hasText: 'Test service' });
        await serviceRow.getByRole('button', { name: 'Open details of Test service' }).click();
        const dialog = page.getByRole('dialog');
        await dialog.getByRole('button', { name: 'Configurations', exact: true }).click();
        await expect(dialog.getByRole('heading', { name: 'Service configuration' })).toBeVisible();
        await dialog.getByRole('button', { name: 'Expand standalone parameters' }).click();
        await dialog.getByRole('combobox', { name: 'Value for Polarization' }).selectOption({ label: 'Vertical' });
        await dialog.getByRole('button', { name: 'Save configuration', exact: true }).click();
        await expect.poll(() => udapiRequests(mock).some(request =>
            request.Route === `${SERVICES_ROUTE}/SERVICE-1/configuration` && request.RequestMethod === 2,
        )).toBeTruthy();
        const saveRequest = udapiRequests(mock).find(request =>
            request.Route === `${SERVICES_ROUTE}/SERVICE-1/configuration` && request.RequestMethod === 2,
        );
        expect(JSON.parse(saveRequest.Body).Parameters).toEqual(expect.arrayContaining([
            expect.objectContaining({ ParameterId: 'parameter-discrete', Value: 'Vertical' }),
        ]));
    });
    test('adds a new service configuration parameter through the UDAPI PUT route', { tag: '@C3' }, async ({ page }) => {
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: /Services/ }).click();
        const serviceRow = page.locator('.service-row').filter({ hasText: 'Test service' });
        await serviceRow.getByRole('button', { name: 'Open details of Test service' }).click();
        const dialog = page.getByRole('dialog');
        await dialog.getByRole('button', { name: 'Configurations', exact: true }).click();
        await expect(dialog.getByRole('heading', { name: 'Service configuration' })).toBeVisible();

        const parameters = dialog.locator('.native-configuration-parameters');
        await parameters.getByRole('combobox', { name: 'Select parameter to add' }).selectOption('parameter-1');
        await parameters.getByRole('button', { name: 'Add', exact: true }).click();
        await dialog.getByRole('spinbutton', { name: 'Value for Symbol Rate' }).fill('2500');
        await dialog.getByRole('button', { name: 'Save configuration', exact: true }).click();

        const saveRequest = () => udapiRequests(mock).find(request =>
            request.Route === `${SERVICES_ROUTE}/SERVICE-1/configuration` && request.RequestMethod === 2,
        );
        await expect.poll(saveRequest).toBeTruthy();
        expect(JSON.parse(saveRequest().Body).Parameters).toEqual(expect.arrayContaining([
            expect.objectContaining({
                ConfigurationId: '',
                ParameterId: 'parameter-1',
                ParameterValueId: '',
                Value: '2500',
                Mandatory: false,
            }),
        ]));
    });
    test('duplicates a service configuration version and selects the inactive copy',     { tag: '@C9' }, async ({ page }) => {
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: /Services/ }).click();
        await page.locator('.service-row').filter({ hasText: 'Test service' }).getByRole('button', { name: 'Open details of Test service' }).click();
        const dialog = page.getByRole('dialog');
        await dialog.getByRole('button', { name: 'Configurations', exact: true }).click();
        await expect(dialog.getByRole('heading', { name: 'Service configuration' })).toBeVisible();

        await dialog.getByRole('button', { name: 'Duplicate configuration' }).click();
        const confirmation = page.getByRole('alertdialog');
        await expect(confirmation.getByRole('heading', { name: 'Duplicate configuration' })).toBeVisible();
        await expect(confirmation).toContainText('Are you sure you want to duplicate the configuration "Current version"?');
        await confirmation.getByRole('button', { name: 'Cancel' }).click();
        await expect(confirmation).toBeHidden();
        expect(udapiRequests(mock).some(request => request.Route.endsWith('/duplicate'))).toBe(false);

        await dialog.getByRole('button', { name: 'Duplicate configuration' }).click();
        await confirmation.getByRole('button', { name: 'Duplicate', exact: true }).click();
        await expect.poll(() => udapiRequests(mock).some(request =>
            request.Route === `${SERVICES_ROUTE}/SERVICE-1/configurationVersion/spec-1/duplicate` && request.RequestMethod === 3,
        )).toBe(true);
        const version = dialog.getByRole('combobox', { name: 'Version' });
        await expect(version.locator('option:checked')).toHaveText('Current version (Copy)');
        await expect(dialog.getByRole('button', { name: 'Activate' })).toBeEnabled();
        await expect(version.locator('option', { hasText: 'Current version (Active)' })).toHaveCount(1);
        await expect(version.locator('option', { hasText: 'Current version (Copy)' })).toHaveCount(1);
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

        const configurationCall = mock.calls.find(call => call.method === 'ExecuteAutomationScriptWithOutput'
            && call.body.script?.Name === 'SLC_SM_UDAPI_ServiceManagement'
            && JSON.parse(call.body.script.Parameters?.[0]?.Value ?? '{}').Route === 'service-management/v1/serviceSpecification/spec-1/configuration');
        expect(configurationCall).toBeTruthy();
        expect(JSON.parse(configurationCall!.body.script.Parameters[0].Value)).toMatchObject({ RequestMethod: 1 });

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
    test('loads specification items through the UDAPI instead of GQI', { tag: '@C2' }, async ({ page }) => {
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: 'inventory_2 Catalog' }).click();
        await page.locator('.catalog-row').filter({ hasText: 'Managed switching' }).getByRole('button', { name: 'Open details of Managed switching' }).click();
        const dialog = page.getByRole('dialog');
        const rows = dialog.locator('.service-items-table tbody tr');
        await expect(rows).toHaveCount(1);
        for (const text of ['Switch booking', 'SRM Booking', 'Switching Booking Manager']) await expect(rows.first()).toContainText(text);
        const itemRequest = mock.calls
            .filter(call => call.method === 'ExecuteAutomationScriptWithOutput' && call.body.script?.Name === 'SLC_SM_UDAPI_ServiceManagement')
            .map(call => JSON.parse(call.body.script.Parameters?.[0]?.Value ?? '{}'))
            .find(request => request.Route === 'service-management/v1/serviceSpecification/spec-1');
        expect(itemRequest).toMatchObject({ RequestMethod: 1, QueryParameters: { fields: ['ServiceItems'] } });
        expect(mock.calls.some(call => call.method === 'OpenQuerySession' && JSON.parse(call.body.query?.Options?.[0]?.Value?.Value ?? '{}').ScriptName === 'SLC_SM_GQIDS_Get Service Items')).toBeFalsy();
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
    test('saves specification configurations through the UDAPI PUT route', { tag: '@C2' }, async ({ page }) => {
        await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: 'inventory_2 Catalog' }).click();
        await page.locator('.catalog-row').filter({ hasText: 'Managed switching' }).getByRole('button', { name: 'Open details of Managed switching' }).click();
        const dialog = page.getByRole('dialog');
        await dialog.getByRole('button', { name: 'Configurations', exact: true }).click();
        await expect(dialog.getByRole('heading', { name: 'Specification configuration' })).toBeVisible();
        const configurationRoute = 'service-management/v1/serviceSpecification/spec-1/configuration';
        const configurationReadCount = () => udapiRequests(mock).filter(request =>
            request.Route === configurationRoute && request.RequestMethod === 1,
        ).length;
        const readsBeforeSave = configurationReadCount();
        await dialog.getByRole('button', { name: 'Expand standalone parameters' }).click();
        await dialog.getByRole('combobox', { name: 'Value for Polarization' }).selectOption({ label: 'Vertical' });
        await dialog.getByRole('textbox', { name: 'Version name' }).fill('Managed switching v2');
        await dialog.getByRole('button', { name: 'Save configuration', exact: true }).click();
        const saveRequest = () => mock.calls
            .filter(call => call.method === 'ExecuteAutomationScriptWithOutput' && call.body.script?.Name === 'SLC_SM_UDAPI_ServiceManagement')
            .map(call => JSON.parse(call.body.script.Parameters?.[0]?.Value ?? '{}'))
            .find(request => request.RequestMethod === 2);
        await expect.poll(saveRequest).toBeTruthy();
        expect(saveRequest().Route).toBe('service-management/v1/serviceSpecification/spec-1/configuration');
        expect(JSON.parse(saveRequest().Body).Parameters).toEqual(expect.arrayContaining([expect.objectContaining({ Value: 'Vertical' })]));
        // The specification is its own configuration version, so the version name is sent as the specification name.
        expect(JSON.parse(saveRequest().Body)).toMatchObject({ Name: 'Managed switching v2' });
        expect(JSON.parse(saveRequest().Body)).not.toHaveProperty('VersionName');
        expect(mock.calls.some(call => call.body.script?.Name === 'SLC_SM_IAS_Service_Spec_Configuration')).toBeFalsy();
        await expect.poll(configurationReadCount).toBeGreaterThan(readsBeforeSave);
        await expect(dialog.getByLabel('Version name')).toBeVisible();
        await expect(dialog.getByRole('button', { name: 'Save configuration', exact: true })).toBeEnabled();
    });
    test('manages service operations through the UDAPI script routes', { tag: '@W8' }, async ({ page }) => {
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

        const scriptsRoute = `${SERVICES_ROUTE}/SERVICE-1/script`;
        const addRequest = udapiRequests(mock).find(request => request.Route === scriptsRoute && request.RequestMethod === 3);
        expect(addRequest).toBeTruthy();
        expect(JSON.parse(addRequest.Body)).toEqual({ name: 'Provision service', description: 'Provisioning operation' });

        await panel.getByRole('button', { name: 'Edit operation Provisioning operation' }).click();
        const editForm = page.getByRole('form', { name: 'Edit service operation' });
        await editForm.getByRole('textbox', { name: 'Description' }).fill('Provisioning updated');
        await editForm.getByRole('button', { name: 'Save operation' }).click();
        await expect(panel.getByText('Provisioning updated', { exact: true })).toBeVisible();
        await expect(panel.getByText('Provisioning operation', { exact: true })).toHaveCount(0);
        const updateRequest = udapiRequests(mock).find(request => request.Route === scriptsRoute && request.RequestMethod === 5);
        expect(JSON.parse(updateRequest.Body)).toEqual({
            originalDescription: 'Provisioning operation',
            name: 'Provision service',
            description: 'Provisioning updated',
        });

        await panel.getByRole('button', { name: 'Delete operation Provisioning updated' }).click();
        const confirmation = page.getByRole('alertdialog', { name: 'Confirm deletion' });
        await confirmation.getByRole('button', { name: 'Delete', exact: true }).click();
        await expect(panel.getByText('Provisioning updated', { exact: true })).toHaveCount(0);
        const deleteRequest = udapiRequests(mock).find(request => request.Route === scriptsRoute && request.RequestMethod === 4);
        expect(JSON.parse(deleteRequest.Body)).toEqual({ description: 'Provisioning updated' });
        expect(mock.calls.some(call => call.body.script?.Name === 'SLC_SM_IAS_Manage Service Scripts')).toBeFalsy();
        expect(mock.calls.some(call => {
            if (call.method !== 'OpenQuerySession') return false;
            const descriptor = JSON.parse(call.body.query.Options[0].Value.Value);
            return descriptor.ScriptName === 'SLC_SM_GQIDS_Get Service Scripts';
        })).toBeFalsy();
    });
    test('lists service orders from the Service Management UDAPI', { tag: ['@G1', '@G2'] }, async ({ page }) => {
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
        expect(orderListCount(mock)).toBe(1);
        expect(count(mock, 'OpenQuerySession')).toBe(0);
    });
    test.describe('service order date serialization', () => {
        test.use({ timezoneId: 'America/Los_Angeles' });
        test('preserves the requested completion date when editing an order', { tag: '@W1' }, async ({ page }) => {
            await page.locator('.order-row').filter({ hasText: 'Network refresh' }).getByRole('button', { name: 'Open details of Network refresh' }).click();
            await page.getByRole('dialog', { name: 'Network refresh' }).locator('.drawer-header-actions').getByRole('button', { name: 'Edit', exact: true }).click();

            const form = page.getByRole('form', { name: 'Edit service order' });
            await expect(form.getByLabel('Requested start date')).toHaveValue('2026-10-20T10:30');
            await expect(form.getByLabel('Requested completed date')).toHaveValue('2026-10-20T12:45');
            await form.getByRole('textbox', { name: 'Label' }).fill('Network refresh updated');
            await form.getByRole('button', { name: 'Save', exact: true }).click();

            await expect.poll(() => udapiRequests(mock).filter(request => request.RequestMethod === 5)).toHaveLength(1);
            const patch = udapiRequests(mock).find(request => request.RequestMethod === 5);
            expect(patch.Route).toBe(`${ORDERS_ROUTE}/${SAMPLE_ORDERS[0].OrderId}`);
            const body = JSON.parse(patch.Body);
            expect(body).toEqual({
                name: 'Network refresh updated',
                description: 'Replace core switches',
                externalId: 'SO-1042',
                priority: 'high',
                completionInfo: { requestedStartDate: '2026-10-20T17:30:00.000Z', requestedCompletedDate: '2026-10-20T19:45:00.000Z' },
            });
            expect(mock.calls.some(call => call.body.script?.Name === 'SLC_SM_IAS_Add Service Order')).toBeFalsy();
        });
    });
    test('refresh re-runs the UDAPI order list request', { tag: '@G3' }, async ({ page }) => {
        await expect(page.locator('.order-row')).toHaveCount(3);
        await page.getByRole('button', { name: 'Refresh', exact: true }).click();
        await expect.poll(() => orderListCount(mock)).toBe(2);
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
        expect(udapiRequests(mock).some(request => request.Route === `${ORDERS_ROUTE}/${SAMPLE_ORDERS[0].OrderId}/item` && request.RequestMethod === 1)).toBeTruthy();
        await page.keyboard.press('Escape');
        await expect(panel).toHaveCount(0);
        expect(new URL(page.url()).hash).toBe('#orders');
        await page.locator('.order-row').filter({ hasText: 'Studio expansion' }).getByRole('button', { name: 'Open details of Studio expansion' }).click();
        await expect(page.getByRole('dialog', { name: 'Studio expansion' })).toContainText('This order has no items.');
        await page.getByRole('button', { name: 'Close order details' }).click();
        await expect(page.getByRole('dialog')).toHaveCount(0);
    });
    test('initializes a service when its specification has no configuration', { tag: '@W3' }, async ({ page }) => {
        await page.locator('.order-row').filter({ hasText: 'Network refresh' }).getByRole('button', { name: 'Open details of Network refresh' }).click();
        const panel = page.getByRole('dialog', { name: 'Network refresh' });
        const item = panel.locator('.order-item').filter({ hasText: 'Core switch B' });
        const initialize = item.getByRole('button', { name: 'Initialize service for Core switch B' });
        await expect(initialize).toBeEnabled();
        await initialize.click();
        await expect(item.getByRole('button', { name: 'Open service Core switch B' })).toBeVisible();
        const requests = udapiRequests(mock);
        const initializeRequests = requests.filter(request => request.Route.endsWith('/initializeService'));
        expect(initializeRequests).toHaveLength(1);
        expect(initializeRequests[0].Route).toBe(`${ORDERS_ROUTE}/${SAMPLE_ORDERS[0].OrderId}/item/a2/initializeService`);
        expect(initializeRequests[0].RequestMethod).toBe(3);
        expect(requests.some(request => request.Route === 'service-management/v1/serviceSpecification/unconfigured-spec/configuration')).toBe(false);
        await expect.poll(() => mock.calls.filter(call => call.method === 'ExecuteAutomationScriptWithOutput' && call.body.script?.Name === 'SLC_SM_Create Service Inventory Item')).toHaveLength(0);
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
       await itemForm.getByRole('checkbox', { name: 'Indefinite runtime' }).uncheck();
       await itemForm.getByLabel('Start date').fill('2099-01-01T08:00');
       await itemForm.getByLabel('End date').fill('2098-01-01T08:00');
       await expect(itemForm.getByText('End date must come after the start date.', { exact: true })).toBeVisible();
       await expect(itemForm.getByRole('button', { name: 'Add item', exact: true })).toBeDisabled();
       await itemForm.getByLabel('End date').fill('2100-01-01T08:00');
       await expect(itemForm.getByRole('button', { name: 'Add item', exact: true })).toBeEnabled();
    });
    test('adds, edits and deletes order items through the UDAPI item routes', { tag: '@D6' }, async ({ page }) => {
        const itemsRoute = `${ORDERS_ROUTE}/${SAMPLE_ORDERS[1].OrderId}/item`;
        const lastRequest = (method: number) => udapiRequests(mock).filter(request => request.RequestMethod === method).at(-1);
        await page.locator('.order-row').filter({ hasText: 'Studio expansion' }).getByRole('button', { name: 'Open details of Studio expansion' }).click();
        const panel = page.getByRole('dialog', { name: 'Studio expansion' });
        await expect(panel).toContainText('This order has no items.');

        await panel.getByRole('button', { name: 'Add item', exact: true }).click();
        const addForm = page.getByRole('form', { name: 'Add service order item' });
        await addForm.getByRole('textbox', { name: 'Label' }).fill('Studio B uplink');
        await addForm.getByRole('combobox', { name: 'Service specification' }).selectOption({ label: 'Managed switching' });
        await addForm.getByRole('button', { name: 'Add item', exact: true }).click();
        await expect(panel.locator('.order-item').filter({ hasText: 'Studio B uplink' })).toContainText('Managed switching');
        expect(lastRequest(3).Route).toBe(itemsRoute);
        expect(JSON.parse(lastRequest(3).Body)).toMatchObject({ name: 'Studio B uplink', action: 'Add', indefiniteRuntime: true, startTime: null, endTime: null, serviceSpecificationId: 'spec-1' });

        await panel.locator('.order-item').filter({ hasText: 'Studio B uplink' }).getByRole('button', { name: 'Edit', exact: true }).click();
        const editForm = page.getByRole('form', { name: 'Edit service order item' });
        await editForm.getByRole('textbox', { name: 'Label' }).fill('Studio B uplink 2');
        await editForm.getByRole('button', { name: 'Save', exact: true }).click();
        await expect(panel.locator('.order-item').filter({ hasText: 'Studio B uplink 2' })).toBeVisible();
        expect(lastRequest(5).Route).toBe(`${itemsRoute}/item-1`);
        const patch = JSON.parse(lastRequest(5).Body);
        expect(patch).toMatchObject({ name: 'Studio B uplink 2', action: 'Add' });
        expect(patch).not.toHaveProperty('serviceSpecificationId');

        await panel.locator('.order-item').filter({ hasText: 'Studio B uplink 2' }).getByRole('button', { name: 'Delete', exact: true }).click();
        await page.getByRole('alertdialog', { name: 'Confirm deletion' }).getByRole('button', { name: 'Delete', exact: true }).click();
        await expect(panel).toContainText('This order has no items.');
        expect(lastRequest(4).Route).toBe(`${itemsRoute}/item-1`);
        expect(mock.calls.some(call => /^SLC_SM_(IAS_Add Service Order Item|Delete Service Order Item)$/.test(call.body.script?.Name ?? ''))).toBeFalsy();
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
    test('shows an error with retry when the order list request fails', { tag: '@G4' }, async ({ page }) => {
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
    test('redirects to DataMiner authentication when the order list call reports an expired connection', { tag: '@G6' }, async ({ page }) => {
        await mockDataMinerApi(page, 'expired');
        const deepLink = `${APP_ROOT_URL}#orders/${encodeURIComponent(SAMPLE_ORDERS[0]['Order ID'])}`;
        await page.goto(deepLink);
        await expect.poll(() => new URL(page.url()).pathname).toBe('/auth/');
        expect(new URL(page.url()).searchParams.get('url')).toBe(deepLink);
    });
});
