import type { BrowserContext, Page, Route } from '@playwright/test';

// DataMiner hosts the build (vite outDir .../Webpages/public/service-management) at /public/service-management/.
export const APP_BASE = '/public/service-management/';
export const APP_ROOT_URL = `${APP_BASE}index.html`;
export const APP_URL = `${APP_ROOT_URL}#orders`;
export const CONNECTION_GUID = '00000000-1111-4222-8333-444444444444';
// vite preview serves the build at '/', so map the DataMiner base path onto the preview root. Without this, relative
// asset URLs under the base path hit the SPA fallback (index.html instead of JS/CSS) and the app never starts.
export async function serveAppAtBasePath(page: Page): Promise<void> { await page.route(`**${APP_BASE}**`, async route => { const url = new URL(route.request().url()); url.pathname = url.pathname.slice(APP_BASE.length - 1); await route.continue({ url: url.toString() }); }); }
export async function setSessionCookie(context: BrowserContext): Promise<void> { await context.addCookies([{ name: 'DMAConnection', value: CONNECTION_GUID, domain: 'localhost', path: '/' }]); }

// Sample rows returned by the mocked "Get_ServiceOrders" GQI ad hoc data source; OrderId is the UDAPI route key.
export const SAMPLE_ORDERS = [
    { 'Order ID': '6f1c2a90-1b2c-4d3e-8f40-5a6b7c8d9e01', OrderId: 'ORDER-000001', Name: 'Network refresh', Description: 'Replace core switches', Priority: 'High', 'External ID': 'SO-1042', 'Related Organization': 'Skyline Broadcast', State: 'In Progress', Start: '2026-10-20T17:30:00.000Z', End: '2026-10-20T19:45:00.000Z' },
    { 'Order ID': '7a2d3b81-2c3d-4e5f-9a51-6b7c8d9e0f12', OrderId: 'ORDER-000002', Name: 'Studio expansion', Description: 'Add studio B', Priority: 'Medium', 'External ID': '', 'Related Organization': 'Acme Media', State: 'New' },
    { 'Order ID': '8b3e4c72-3d4e-4f60-ab62-7c8d9e0f1a23', OrderId: 'ORDER-000003', Name: 'Backup link', Description: 'Secondary contribution link', Priority: 'Low', 'External ID': 'SO-1038', 'Related Organization': 'Skyline Broadcast', State: 'Completed' },
];
// Deliberately not in the C# column order: the app must map cells by column name, not by index.
const COLUMNS = ['State', 'Name', 'Order ID', 'Related Organization', 'Priority', 'External ID', 'Description', 'End', 'Start'];
// Rows returned by the mocked "Get_ServiceOrderItems" data source, keyed by the "DOM ID" argument.
export const SAMPLE_ORDER_ITEMS: Record<string, Record<string, string>[]> = {
    [SAMPLE_ORDERS[0]['Order ID']]: [
        { ID: 'a1', Name: 'Core switch A', Start: '9/1/2026 8:00:00 AM', End: '9/2/2026 6:00:00 PM', Action: 'Add', Category: 'Connectivity', 'Service Specification': 'Managed switching', Service: 'Core network', ServiceId: 'service-1', Status: 'In Progress', Description: 'Primary site' },
        { ID: 'a2', Name: 'Core switch B', Start: 'No Start Time', End: 'No End Time', Action: 'Modify', Category: 'Connectivity', 'Service Specification': 'Managed switching', 'Service Specification ID': 'unconfigured-spec', Service: '', ServiceId: '', Status: 'New', Description: '' },
    ],
};
const ITEM_COLUMNS = ['Status', 'ID', 'Name', 'Start', 'End', 'Action', 'Category', 'Service Specification', 'Service Specification ID', 'Service', 'ServiceId', 'Description'];
const ORDERS_TYPE = 'SLC_SM_GQIDS_Get_Service_Orders.GetServiceOrders';
const RELATED_SERVICE_ORDERS_TYPE = 'SLC_SM_GQIDS_Get_Related_Service_Orders.GetRelatedServiceOrders';
const ITEMS_TYPE = 'SLC_SM_GQIDS_Get_Service_Order_Items_1.EventManagerGetMultipleSections';
const SERVICES_TYPE = 'SLC_SM_GQIDS_Get_Services.GetServices';
const MONITORING_SERVICES_TYPE = 'SLC_SM_GQIDS_Get_Monitoring_Services.GetMonitoringServices';
const DOCUMENT_HUB_LOGOS_TYPE = 'SLCSMDSGetDocumentHubLogos.SLCSMDSGetDocumentHubLogos';
const ORGANIZATIONS_TYPE = 'SLC_SM_GQIDS_Get_Service_Organizations.GetOrganizations';
const SERVICE_SCRIPTS_TYPE = 'SLCSMGQIDSGetServiceScripts.GetServiceScripts';
const SPECIFICATIONS_TYPE = 'SLC_SM_GQIDS_Get_Service_Specifications.GetServiceSpecifications';
const SERVICE_ITEMS_TYPE = 'SLC_SM_GQIDS_Get_Service_Items.EventManagerGetMultipleSections';
const CONFIGURATIONS_TYPE = 'SLC_SM_GQIDS_Get_Service_Configurations.GetServiceConfigurations';
const PAGE_SIZE = 2;

export const SAMPLE_SERVICE = { 'DOM ID': 'service-1', 'Service ID': 'SERVICE-1', 'Service Name': 'Test service', 'Service Status': 'Active', 'Service Start': '9/1/2026 8:00:00 AM', 'Service End': 'No End Time', 'Service Category': 'Connectivity', 'Service Category Type': 'Channel', 'Service Logo': '', 'Service Specification': 'Managed switching', 'Alarm Level': '0', 'Service Organization': 'Skyline Broadcast', 'Monitoring Service': 'Monitoring service 1', 'Related Service Orders': 'Network refresh', 'Related Service Order Count': '1' };
const SERVICE_COLUMNS = ['DOM ID', 'Service ID', 'Service Name', 'Service Status', 'Service Start', 'Service End', 'Service Category', 'Service Category Type', 'Service Logo', 'Service Specification', 'Alarm Level', 'Service Organization', 'Monitoring Service', 'Related Service Orders', 'Related Service Order Count'];
const MONITORING_SERVICE_COLUMNS = ['Monitoring Service ID', 'Monitoring Service Name'];
const DOCUMENT_HUB_COLUMNS = ['Bucket', 'File Name', 'Icon', 'Extension', 'Directory'];
const SERVICE_SCRIPT_COLUMNS = ['Script Name', 'Description'];
const ORGANIZATION_COLUMNS = ['Organization ID', 'Organization Name'];
const SAMPLE_SERVICE_SCRIPTS = [
    { 'Script Name': 'Refresh service', Description: 'Refreshes the service implementation.' },
    { 'Script Name': 'Restart service', Description: 'Restarts the service implementation.' },
    { 'Script Name': '', Description: '' },
];
export const SAMPLE_ORGANIZATIONS = [
    { 'Organization ID': 'org-1', 'Organization Name': 'Acme Media' },
    { 'Organization ID': 'org-2', 'Organization Name': 'Skyline Broadcast' },
];
export const SAMPLE_CATEGORIES = [
    { 'Category ID': 'category-1', 'Category Name': 'Connectivity', 'Category Type': 'Channel', 'Category Icon': '' },
    { 'Category ID': 'category-2', 'Category Name': 'Production', 'Category Type': 'Service', 'Category Icon': '' },
];
const SAMPLE_DOCUMENT_HUB_LOGOS = [
    { Bucket: 'Default', 'File Name': 'connectivity.svg', Icon: '/Public/DocumentHub/connectivity.svg', Extension: '.svg', Directory: '/Public/DocumentHub' },
    { Bucket: 'Default', 'File Name': 'production.svg', Icon: '/Public/DocumentHub/production.svg', Extension: '.svg', Directory: '/Public/DocumentHub' },
];
const CATEGORY_COLUMNS = ['Category ID', 'Category Name', 'Category Type', 'Category Icon'];
export const SAMPLE_SPECIFICATION = { 'Specification ID': 'spec-1', Name: 'Managed switching', Description: 'Managed switching service specification', 'Created At': '9/1/2026 2:40:53 PM', 'Created By': 'Test user', 'Last Modified': '9/2/2026 10:13:44 PM', 'Last Modified By': 'Test user', 'Service Item Count': '2' };
const SPECIFICATION_COLUMNS = ['Specification ID', 'Name', 'Description', 'Created At', 'Created By', 'Last Modified', 'Last Modified By', 'Service Item Count'];
const SERVICE_ITEM_COLUMNS = ['Actions', 'Label', 'Service Item ID', 'Service Item Type', 'Definition Reference', 'Service Item Script', 'Implementation Reference', 'Implementation Reference Name', 'Implementation Reference Link', 'Implementation Reference Has Value', 'Implementation Reference Name Has Value', 'Implementation Reference Link Has Value', 'Implementation State', 'Implementation Reference Custom Link', 'Implementation Reference Custom Link Has Value', 'Monitoring Service State', 'Monitoring Service DMA ID/SID', 'Log'];
const CONFIGURATION_COLUMNS = ['Version ID', 'Version Name', 'Version Description', 'Version Start', 'Version End', 'Is Active', 'Record Type', 'Configuration ID', 'Parameter ID', 'Parameter Value ID', 'Parameter Name', 'Parameter Type', 'Parameter Value', 'Mandatory', 'Profile ID', 'Profile Name', 'Profile Definition ID', 'Profile Definition Name', 'Profile Reusable', 'Profile Mandatory', 'Nested Profile ID', 'Nested Profile Name', 'Nested Profile Definition ID', 'Nested Profile Definition Name', 'Parameter Label', 'Parameter Is Linked', 'Parameter Unit', 'Parameter Unit ID', 'Parameter Start', 'Parameter End', 'Parameter Step Size', 'Parameter Decimals', 'Parameter Options', 'Parameter Fixed', 'Parameter N/A', 'Expose At Order', 'Mandatory At Order', 'Mandatory At Service'];
// GQI configuration row -> UDAPI SpecificationConfigurationRecord (camelCase keys).
const toConfigurationRecord = (row: Record<string, unknown>) => Object.fromEntries(CONFIGURATION_COLUMNS.map(column => [
    column === 'Parameter N/A' ? 'parameterNotApplicable' : column.split(' ').map((word, index) => index === 0 ? word.toLowerCase() : word[0] + word.slice(1).toLowerCase()).join(''),
    row[column] ?? '',
]));
const SAMPLE_SPECIFICATION_CONFIGURATIONS = [
    { 'Version ID': 'spec-1', 'Version Name': 'Current version', 'Version Description': 'Current specification configuration', 'Version Start': '9/1/2026 8:00:00 AM', 'Version End': 'No End Time', 'Is Active': 'true', 'Record Type': 'Profile', 'Configuration ID': 'spec-profile-config', 'Parameter ID': '', 'Parameter Value ID': '', 'Parameter Name': '', 'Parameter Type': '', 'Parameter Value': '', Mandatory: 'false', 'Profile ID': 'spec-profile-config', 'Profile Name': 'Transport profile', 'Profile Definition ID': 'profile-definition', 'Profile Definition Name': 'Transport profile', 'Profile Reusable': 'false', 'Profile Mandatory': 'true', 'Nested Profile ID': '', 'Nested Profile Name': '', 'Nested Profile Definition ID': '', 'Nested Profile Definition Name': '' },
    { 'Version ID': 'spec-1', 'Version Name': 'Current version', 'Version Description': 'Current specification configuration', 'Version Start': '9/1/2026 8:00:00 AM', 'Version End': 'No End Time', 'Is Active': 'true', 'Record Type': 'Parameter', 'Configuration ID': 'spec-discrete-config', 'Parameter ID': 'parameter-discrete', 'Parameter Value ID': 'parameter-discrete-value', 'Parameter Name': 'Polarization', 'Parameter Type': 'Discrete', 'Parameter Value': 'Horizontal', 'Parameter Label': 'Polarization', 'Parameter Is Linked': 'false', 'Parameter Options': '[{"Id":"horizontal","Value":"Horizontal"},{"Id":"vertical","Value":"Vertical"}]', Mandatory: 'false', 'Profile ID': '', 'Profile Name': '', 'Profile Definition ID': '', 'Profile Definition Name': '', 'Profile Reusable': 'false', 'Profile Mandatory': 'false', 'Nested Profile ID': '', 'Nested Profile Name': '', 'Nested Profile Definition ID': '', 'Nested Profile Definition Name': '' },
    { 'Version ID': 'spec-1', 'Version Name': 'Current version', 'Version Description': 'Current specification configuration', 'Version Start': '9/1/2026 8:00:00 AM', 'Version End': 'No End Time', 'Is Active': 'true', 'Record Type': 'Parameter', 'Configuration ID': 'spec-number-config', 'Parameter ID': 'parameter-number', 'Parameter Value ID': 'parameter-number-value', 'Parameter Name': 'Max Overall Bitrate', 'Parameter Type': 'Number', 'Parameter Value': '30', 'Parameter Label': 'Max Overall Bitrate', 'Parameter Is Linked': 'false', 'Parameter Unit': 'Mbps', 'Parameter Unit ID': 'unit-mbps', 'Parameter Start': '-10000', 'Parameter End': '10000', 'Parameter Step Size': '1', 'Parameter Decimals': '0', Mandatory: 'false', 'Profile ID': '', 'Profile Name': '', 'Profile Definition ID': '', 'Profile Definition Name': '', 'Profile Reusable': 'false', 'Profile Mandatory': 'false', 'Nested Profile ID': '', 'Nested Profile Name': '', 'Nested Profile Definition ID': '', 'Nested Profile Definition Name': '' },
    { 'Version ID': 'spec-1', 'Version Name': 'Current version', 'Version Description': 'Current specification configuration', 'Version Start': '9/1/2026 8:00:00 AM', 'Version End': 'No End Time', 'Is Active': 'true', 'Record Type': 'NestedProfile', 'Configuration ID': '', 'Parameter ID': 'parameter-1', 'Parameter Value ID': 'parameter-value-1', 'Parameter Name': 'Symbol Rate', 'Parameter Type': 'Number', 'Parameter Value': '2500', Mandatory: 'false', 'Profile ID': 'spec-profile-config', 'Profile Name': 'Transport profile', 'Profile Definition ID': 'profile-definition', 'Profile Definition Name': 'Transport profile', 'Profile Reusable': 'false', 'Profile Mandatory': 'true', 'Nested Profile ID': 'nested-profile', 'Nested Profile Name': 'Reusable encapsulation profile', 'Nested Profile Definition ID': 'nested-definition', 'Nested Profile Definition Name': 'Encapsulation profile' },
    { 'Version ID': 'spec-2', 'Version Name': 'Archived version', 'Version Description': 'Archived specification configuration', 'Version Start': '8/1/2026 8:00:00 AM', 'Version End': '8/31/2026 11:59:00 PM', 'Is Active': 'false', 'Record Type': 'Version', 'Configuration ID': '', 'Parameter ID': '', 'Parameter Value ID': '', 'Parameter Name': '', 'Parameter Type': '', 'Parameter Value': '', Mandatory: 'false', 'Profile ID': '', 'Profile Name': '', 'Profile Definition ID': '', 'Profile Definition Name': '', 'Profile Reusable': 'false', 'Profile Mandatory': 'false', 'Nested Profile ID': '', 'Nested Profile Name': '', 'Nested Profile Definition ID': '', 'Nested Profile Definition Name': '' },
    { 'Version ID': '', 'Version Name': '', 'Version Description': '', 'Version Start': '', 'Version End': '', 'Is Active': 'false', 'Record Type': 'AvailableParameter', 'Configuration ID': '', 'Parameter ID': 'parameter-1', 'Parameter Value ID': '', 'Parameter Name': 'Symbol Rate', 'Parameter Type': 'Number', 'Parameter Value': '', Mandatory: 'false', 'Profile ID': '', 'Profile Name': '', 'Profile Definition ID': '', 'Profile Definition Name': '', 'Profile Reusable': 'false', 'Profile Mandatory': 'false', 'Nested Profile ID': '', 'Nested Profile Name': '', 'Nested Profile Definition ID': '', 'Nested Profile Definition Name': '' },
    { 'Version ID': '', 'Version Name': '', 'Version Description': '', 'Version Start': '', 'Version End': '', 'Is Active': 'false', 'Record Type': 'AvailableParameter', 'Configuration ID': '', 'Parameter ID': 'parameter-discrete', 'Parameter Value ID': '', 'Parameter Name': 'Polarization', 'Parameter Type': 'Discrete', 'Parameter Value': '', 'Parameter Label': 'Polarization', 'Parameter Is Linked': 'false', 'Parameter Options': '[{"Id":"horizontal","Value":"Horizontal"},{"Id":"vertical","Value":"Vertical"}]', Mandatory: 'false', 'Profile ID': '', 'Profile Name': '', 'Profile Definition ID': '', 'Profile Definition Name': '', 'Profile Reusable': 'false', 'Profile Mandatory': 'false', 'Nested Profile ID': '', 'Nested Profile Name': '', 'Nested Profile Definition ID': '', 'Nested Profile Definition Name': '' },
    { 'Version ID': '', 'Version Name': '', 'Version Description': '', 'Version Start': '', 'Version End': '', 'Is Active': 'false', 'Record Type': 'AvailableParameter', 'Configuration ID': '', 'Parameter ID': 'parameter-number', 'Parameter Value ID': '', 'Parameter Name': 'Max Overall Bitrate', 'Parameter Type': 'Number', 'Parameter Value': '', 'Parameter Label': 'Max Overall Bitrate', 'Parameter Is Linked': 'false', 'Parameter Unit': 'Mbps', 'Parameter Unit ID': 'unit-mbps', 'Parameter Start': '-10000', 'Parameter End': '10000', 'Parameter Step Size': '1', 'Parameter Decimals': '0', Mandatory: 'false', 'Profile ID': '', 'Profile Name': '', 'Profile Definition ID': '', 'Profile Definition Name': '', 'Profile Reusable': 'false', 'Profile Mandatory': 'false', 'Nested Profile ID': '', 'Nested Profile Name': '', 'Nested Profile Definition ID': '', 'Nested Profile Definition Name': '' },
    { 'Version ID': '', 'Version Name': '', 'Version Description': '', 'Version Start': '', 'Version End': '', 'Is Active': 'false', 'Record Type': 'AvailableProfile', 'Configuration ID': '', 'Parameter ID': '', 'Parameter Value ID': '', 'Parameter Name': '', 'Parameter Type': '', 'Parameter Value': '', Mandatory: 'false', 'Profile ID': '', 'Profile Name': '', 'Profile Definition ID': 'profile-definition', 'Profile Definition Name': 'Transport profile', 'Profile Reusable': 'false', 'Profile Mandatory': 'false', 'Nested Profile ID': '', 'Nested Profile Name': '', 'Nested Profile Definition ID': '', 'Nested Profile Definition Name': '' },
    { 'Version ID': '', 'Version Name': '', 'Version Description': '', 'Version Start': '', 'Version End': '', 'Is Active': 'false', 'Record Type': 'AvailableProfile', 'Configuration ID': '', 'Parameter ID': '', 'Parameter Value ID': '', 'Parameter Name': '', 'Parameter Type': '', 'Parameter Value': '', Mandatory: 'false', 'Profile ID': '', 'Profile Name': '', 'Profile Definition ID': 'nested-definition', 'Profile Definition Name': 'Encapsulation profile', 'Profile Reusable': 'false', 'Profile Mandatory': 'false', 'Nested Profile ID': '', 'Nested Profile Name': '', 'Nested Profile Definition ID': '', 'Nested Profile Definition Name': '' },
    { 'Version ID': '', 'Version Name': '', 'Version Description': '', 'Version Start': '', 'Version End': '', 'Is Active': 'false', 'Record Type': 'AvailableNestedProfile', 'Configuration ID': '', 'Parameter ID': '', 'Parameter Value ID': '', 'Parameter Name': '', 'Parameter Type': '', 'Parameter Value': '', Mandatory: 'false', 'Profile ID': '', 'Profile Name': '', 'Profile Definition ID': '', 'Profile Definition Name': '', 'Profile Reusable': 'true', 'Profile Mandatory': 'false', 'Nested Profile ID': 'nested-profile', 'Nested Profile Name': 'Reusable encapsulation profile', 'Nested Profile Definition ID': 'nested-definition', 'Nested Profile Definition Name': 'Encapsulation profile' },
];

export type GqiMode = 'ok' | 'empty' | 'error' | 'expired';
export interface IasMock {
    messages: { ClientSubscriptionID: number; Method: string; Parameters: Record<string, any> }[];
    continues: Record<string, string>[];
    executeError: string | null;
    launchError: string | null;
    subscriptError: string | null;
}
export interface GqiMock { mode: GqiMode; itemsFail: boolean; calls: { method: string; body: Record<string, any> }[]; ias: IasMock }

export const IAS_SCRIPT_ID = 42;
export const SUBSCRIPT_ID = 43;
export const SUBSCRIPT_DEST_VARS = { bookingName: 'dv-booking-name', start: 'dv-booking-start', details: 'dv-error-details', toggle: 'dv-error-toggle', cancel: 'dv-booking-cancel', create: 'dv-booking-create', close: 'dv-error-close' } as const;
// Rows returned by the mocked "Get Service Items" data source for SAMPLE_SERVICE.
const SAMPLE_SERVICE_ITEMS = [
    { Label: 'Encoder booking', 'Service Item ID': '1', 'Service Item Type': 'SRMBooking', 'Definition Reference': 'Encoding Booking Manager', 'Service Item Script': 'SRM_Booking Wizard', 'Implementation Reference': '', 'Implementation State': 'Not Implemented', 'Monitoring Service State': '' },
    { Label: 'Monitoring link', 'Service Item ID': '2', 'Service Item Type': 'Service', 'Definition Reference': 'Monitoring', 'Service Item Script': '', 'Implementation Reference': '', 'Implementation State': 'Not Implemented', 'Monitoring Service State': '' },
];
export const IAS_DEST_VARS = {
    name: 'dv-name', description: 'dv-description', externalId: 'dv-external-id', priority: 'dv-priority', organization: 'dv-organization', contacts: 'dv-contacts',
    collapse: 'dv-collapse', completion: 'dv-completion', start: 'dv-start', end: 'dv-end', create: 'dv-create', cancel: 'dv-cancel',
} as const;
const IAS_ORGANIZATIONS = ['-None-', 'Acme Media', 'Skyline Broadcast'];
const IAS_CONTACTS: Record<string, string[]> = { 'Acme Media': ['Alex Ames'], 'Skyline Broadcast': ['Jane Doe', 'John Smith'] };
const IAS_ORDER_ID = 'ORDER-000004';
const IAS_DEFAULT_DATE = '2026-10-16T09:30:00.0000000Z';
const GS = '\x1d';

interface IasDialogState { name: string; nameError: string; description: string; externalId: string; priority: string; organization: string; contacts: string; expanded: boolean; completion: boolean; start: string; end: string }

function iasLayout(state: IasDialogState, inlineOrganizations: boolean) {
    const label = (text: string) => ({ Type: 'static', Text: text, DestVar: null });
    const cell = (components: unknown[], colSpan = 1) => ({ ColSpan: colSpan, RowSpan: 1, Components: components });
    const empty = () => cell([]);
    const options = (values: string[]) => values.map(value => ({ Key: value, Value: value }));
    const D = IAS_DEST_VARS;
    const row = (...cells: unknown[]) => ({ Cells: cells });
    const completionRows = state.expanded ? [
        row(cell([label('Completion Settings Enabled')]), cell([{ Type: 'checkbox', DestVar: D.completion, Value: state.completion ? 'True' : 'False', WantsOnChange: true }]), empty(), empty()),
        row(cell([label('Requested Start Date')]), cell([{ Type: 'time', DestVar: D.start, Value: state.start, IsEnabled: state.completion }]), empty(), empty()),
        row(cell([label('Requested Completed Date')]), cell([{ Type: 'time', DestVar: D.end, Value: state.end, IsEnabled: state.completion }]), empty(), empty()),
    ] : [];
    return {
        Title: 'Manage Service Order',
        Rows: [
            row(cell([label('Order ID')]), cell([label(IAS_ORDER_ID)]), empty(), empty()),
            row(cell([label('Label')]), cell([{ Type: 'textbox', DestVar: D.name, Value: state.name, Placeholder: IAS_ORDER_ID, WantsOnChange: true }], 2), cell([label(state.nameError)])),
            row(cell([label('Description')]), cell([{ Type: 'textbox', DestVar: D.description, Value: state.description, IsMultiline: true }], 2), empty()),
            row(cell([label('External ID')]), cell([{ Type: 'textbox', DestVar: D.externalId, Value: state.externalId }], 2), empty()),
            row(cell([label('Priority')]), cell([{ Type: 'dropdown', DestVar: D.priority, Value: state.priority, Options: options(['High', 'Medium', 'Low']) }], 2), empty()),
            row(cell([label('Organization')]), cell([{ Type: 'dropdown', DestVar: D.organization, Value: state.organization, WantsOnChange: true, Options: inlineOrganizations ? options(IAS_ORGANIZATIONS) : [] }], 2), empty()),
            row(cell([label('Order Contact')]), cell([{ Type: 'checkboxlist', DestVar: D.contacts, Value: state.contacts, Options: options(IAS_CONTACTS[state.organization] ?? []) }], 2), empty()),
            row(cell([{ Type: 'button', DestVar: D.collapse, Text: state.expanded ? 'Hide Completion Details' : 'Show Completion Details' }]), empty(), empty(), empty()),
            ...completionRows,
            row(cell([label('')]), empty(), empty(), empty()),
            row(empty(), cell([{ Type: 'button', DestVar: D.create, Text: 'Create', Class: 'CallToAction' }]), cell([{ Type: 'button', DestVar: D.cancel, Text: 'Cancel' }]), empty()),
        ],
    };
}

function layoutDestVars(layout: { Rows: { Cells: unknown[] }[] }): string[] {
    return layout.Rows.flatMap(row => (row.Cells as { Components: unknown[] }[]).flatMap(cell => (cell.Components as { DestVar?: string | null }[]).map(component => component.DestVar).filter((destVar): destVar is string => Boolean(destVar))));
}

// Simulates DataMiner's WebSocket IAS protocol (SetConnectionID, LaunchAutomationScript, ContinueAutomationScript) for
// SLC_SM_IAS_Add Service Order (script ID 42) and Launch Interactive Subscript (script ID 43, showing the item's booking wizard).
async function mockIasWebSocket(page: Page, mock: GqiMock, orders: Record<string, string>[], serviceItems: Record<string, string>[]): Promise<void> {
    await page.routeWebSocket('**/API/v1/WebSocket.ashx', ws => {
        let launchId = 0;
        let activeScriptId = 0;
        let subscriptExpanded = false;
        let state: IasDialogState;
        let layout: { Title: string; Rows: { Cells: unknown[] }[] };
        const send = (message: Record<string, unknown>) => ws.send(JSON.stringify(message));
        const event = (Type: string, Data: unknown) => send({ Type, Data, ClientSubscriptionID: launchId });
        const render = (asyncOrganizations = false) => {
            layout = iasLayout(state, !asyncOrganizations);
            event('DMAAutomationScriptUIUpdate', { Type: 'InteractiveUI', AsyncCalls: asyncOrganizations ? 1 : 0, Layout: layout });
            if (asyncOrganizations) event('DMAAutomationScriptUIDropDownUpdate', { Info: { DropDownID: IAS_DEST_VARS.organization, Page: 0, Options: IAS_ORGANIZATIONS.map(value => ({ Key: value, Value: value })) } });
        };
        const renderSubscript = () => {
            const S = SUBSCRIPT_DEST_VARS;
            const cell = (components: unknown[], colSpan = 1) => ({ ColSpan: colSpan, RowSpan: 1, Components: components });
            const label = (text: string) => ({ Type: 'static', Text: text, DestVar: null });
            layout = mock.ias.subscriptError
                // Mirrors engine.ShowErrorDialog from SLC_SM_Common: message, collapsible details, and a Close button.
                ? { Title: 'Error', Rows: [
                    { Cells: [cell([label(mock.ias.subscriptError)], 2)] },
                    { Cells: [cell([{ Type: 'button', DestVar: S.toggle, Text: subscriptExpanded ? '-' : '+' }]), cell([])] },
                    ...(subscriptExpanded ? [{ Cells: [cell([{ Type: 'textbox', DestVar: S.details, Value: `System.InvalidOperationException: ${mock.ias.subscriptError}`, IsMultiline: true }], 2)] }] : []),
                    { Cells: [cell([]), cell([{ Type: 'button', DestVar: S.close, Text: 'Close', Class: 'CallToAction' }])] },
                ] }
                : { Title: 'Encoding Booking Wizard', Rows: [
                    { Cells: [cell([label('Booking name')]), cell([{ Type: 'textbox', DestVar: S.bookingName, Value: 'Test service' }], 2)] },
                    { Cells: [cell([label('Start')]), cell([{ Type: 'time', DestVar: S.start, Value: '2026-09-01T08:00:00.0000000Z' }], 2)] },
                    { Cells: [cell([]), cell([{ Type: 'button', DestVar: S.cancel, Text: 'Cancel' }]), cell([{ Type: 'button', DestVar: S.create, Text: 'Create Booking', Class: 'CallToAction' }])] },
                ] };
            event('DMAAutomationScriptUIUpdate', { Type: 'InteractiveUI', AsyncCalls: 0, Layout: layout });
        };
        const continueSubscript = (values: Record<string, string>) => {
            const S = SUBSCRIPT_DEST_VARS;
            const trigger = values._onchange;
            if (trigger === S.toggle) { subscriptExpanded = !subscriptExpanded; return renderSubscript(); }
            if (trigger === S.close || trigger === S.cancel) return event('DMAAutomationScriptBasicUpdate', { Type: 'Completed', Data: 'SUCCEEDED' });
            if (trigger === S.create) {
                const item = serviceItems.find(candidate => candidate.Label === 'Encoder booking');
                if (item) Object.assign(item, { 'Implementation Reference': 'reservation-1', 'Implementation State': 'Implemented' });
                return event('DMAAutomationScriptBasicUpdate', { Type: 'Completed', Data: 'SUCCEEDED' });
            }
            return event('WebSocketError', { Message: `Unexpected trigger ${trigger}` });
        };
        ws.onMessage(raw => {
            const message = JSON.parse(String(raw));
            mock.ias.messages.push(message);
            const parameters = message.Parameters ?? {};
            if (message.Method === 'Ping') return send({ Type: 'Pong', ClientSubscriptionID: 0 });
            if (message.Method === 'SetConnectionID') {
                if (parameters.connectionID !== CONNECTION_GUID) return send({ Type: 'WebSocketError', Data: { Message: 'Unknown connection.' }, ClientSubscriptionID: message.ClientSubscriptionID });
                return send({ Type: 'String', Data: CONNECTION_GUID, ClientSubscriptionID: message.ClientSubscriptionID });
            }
            if (message.Method === 'LaunchAutomationScript') {
                launchId = message.ClientSubscriptionID;
                activeScriptId = parameters.scriptID;
                subscriptExpanded = false;
                if (parameters.connection !== CONNECTION_GUID || ![IAS_SCRIPT_ID, SUBSCRIPT_ID].includes(parameters.scriptID)) return event('WebSocketError', { Message: `Unexpected LaunchAutomationScript parameters ${JSON.stringify(parameters)}` });
                if (mock.ias.launchError) return event('WebSocketError', { Message: mock.ias.launchError });
                event('DMAAutomationScriptBasicUpdate', { Type: 'Started', Data: '' });
                if (activeScriptId === SUBSCRIPT_ID) return renderSubscript();
                state = { name: '', nameError: '', description: '', externalId: '', priority: 'High', organization: '-None-', contacts: '', expanded: false, completion: false, start: IAS_DEFAULT_DATE, end: IAS_DEFAULT_DATE };
                return render(true);
            }
            if (message.Method === 'ContinueAutomationScript') {
                if (parameters.connection !== CONNECTION_GUID || parameters.scriptId !== activeScriptId) return event('WebSocketError', { Message: "Parameter 'scriptId' is not defined." });
                const values: Record<string, string> = {};
                for (const part of String(parameters.values ?? '').split(GS)) { const index = part.indexOf('='); values[part.slice(0, index)] = part.slice(index + 1); }
                mock.ias.continues.push(values);
                const missing = layoutDestVars(layout).filter(destVar => !(destVar in values));
                if (missing.length) return event('WebSocketError', { Message: `Missing dialog values: ${missing.join(', ')}` });
                if (activeScriptId === SUBSCRIPT_ID) return continueSubscript(values);
                const D = IAS_DEST_VARS;
                Object.assign(state, {
                    name: values[D.name], description: values[D.description], externalId: values[D.externalId], priority: values[D.priority],
                    organization: values[D.organization], contacts: values[D.contacts],
                    ...(D.completion in values ? { completion: values[D.completion] === 'True', start: values[D.start], end: values[D.end] } : {}),
                });
                const trigger = values._onchange;
                const duplicate = orders.some(order => order.Name.toLowerCase() === state.name.trim().toLowerCase());
                if (trigger === D.name) { state.nameError = !state.name.trim() ? 'Placeholder will be used' : duplicate ? 'Name already exists!' : ''; return render(); }
                if (trigger === D.organization) { state.contacts = ''; return render(); }
                if (trigger === D.collapse) { state.expanded = !state.expanded; return render(); }
                if (trigger === D.completion) return render();
                if (trigger === D.cancel) return event('DMAAutomationScriptBasicUpdate', { Type: 'Completed', Data: 'SUCCEEDED' });
                if (trigger === D.create) {
                    if (values[D.create] !== D.create || values[D.cancel] !== '') return event('WebSocketError', { Message: 'Unexpected button values.' });
                    if (duplicate) { state.nameError = 'Name already exists!'; return render(); }
                    orders.push({ 'Order ID': '9c4f5d83-4e5f-4a71-bc73-8d9e0f1a2b34', Name: state.name.trim() || IAS_ORDER_ID, Description: state.description, Priority: state.priority, 'External ID': state.externalId, 'Related Organization': state.organization === '-None-' ? '' : state.organization, State: 'New' });
                    return event('DMAAutomationScriptBasicUpdate', { Type: 'Completed', Data: 'SUCCEEDED' });
                }
                return event('WebSocketError', { Message: `Unexpected trigger ${trigger}` });
            }
            return send({ Type: 'WebSocketError', Data: { Message: `Unexpected WebSocket method ${message.Method}` }, ClientSubscriptionID: message.ClientSubscriptionID });
        });
    });
}

async function json(route: Route, status: number, body: unknown): Promise<void> { await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) }); }

// UDAPI order (item) records: Priority as the enum number, StatusName as the camelCase enum name, dates as ISO strings.
const udapiDate = (value?: string) => { const time = Date.parse(value ?? ''); return Number.isNaN(time) ? null : new Date(time).toISOString(); };
const udapiReference = (value?: string) => value ? { Identifier: value } : null;
const camelCase = (value?: string) => String(value ?? '').replace(/\s+(\w)/g, (_, letter: string) => letter.toUpperCase()).replace(/^\w/, letter => letter.toLowerCase());
function toUdapiOrder(row: Record<string, string>) {
    const completion = row.Start || row.End ? { RequestedStartDate: udapiDate(row.Start), RequestedCompletedDate: udapiDate(row.End), CompletionDate: null } : null;
    return { Identifier: row['Order ID'], OrderId: row.OrderId, Name: row.Name, Description: row.Description, Priority: ['High', 'Medium', 'Low'].indexOf(row.Priority), ExternalID: row['External ID'], StatusName: camelCase(row.State), OrganizationId: SAMPLE_ORGANIZATIONS.find(organization => organization['Organization Name'] === row['Related Organization'])?.['Organization ID'] ?? null, OrganizationName: row['Related Organization'] || null, CompletionInfo: completion, CreatedAt: null };
}
function toUdapiOrderItem(row: Record<string, string>) {
    const end = udapiDate(row.End);
    return { Identifier: row.ID, Name: row.Name, Action: row.Action, Description: row.Description, StatusName: camelCase(row.Status), StartTime: udapiDate(row.Start), EndTime: end, IndefiniteRuntime: !end, ServiceInfo: { ServiceCategoryId: udapiReference(row['Category ID']), SpecificationId: udapiReference(row['Service Specification ID']), ServiceId: udapiReference(row.ServiceId) }, ServiceCategoryName: row.Category || null, ServiceSpecificationName: row['Service Specification'] || null, ServiceName: row.Service || null };
}
// UDAPI service records: the SDM Service properties plus the lookups added by GET service.
function toUdapiService(row: Record<string, string>) {
    return { Identifier: row['DOM ID'], ServiceID: row['Service ID'], Name: row['Service Name'], StatusName: row['Service Status'].toLowerCase(), StartTime: udapiDate(row['Service Start']), EndTime: udapiDate(row['Service End']), Icon: row['Service Logo'] || null, MonitoringService: row['Monitoring Service'] || null, CategoryName: row['Service Category'] || null, CategoryType: row['Service Category Type'] || null, ServiceSpecificationName: row['Service Specification'] || null, OrganizationId: SAMPLE_ORGANIZATIONS.find(organization => organization['Organization Name'] === row['Service Organization'])?.['Organization ID'] ?? null, OrganizationName: row['Service Organization'] || null };
}
function selectUdapiFields(
    record: Record<string, unknown>,
    request: { QueryParameters?: { fields?: string[] } },
) {
    const requested = new Set(
        (request.QueryParameters?.fields ?? [])
            .flatMap(field => field.split(','))
            .map(field => field.trim().toLowerCase())
            .filter(Boolean),
    );
    if (requested.size === 0) return record;
    return Object.fromEntries(Object.entries(record).filter(([name]) => requested.has(name.toLowerCase())));
}
function applyServiceRequest(row: Record<string, string>, body: any, categories: Record<string, string>[]) {
    if ('serviceId' in body) row['Service ID'] = body.serviceId;
    if ('name' in body) row['Service Name'] = body.name;
    if ('startTime' in body) row['Service Start'] = body.startTime ?? '';
    if ('endTime' in body) row['Service End'] = body.endTime ?? 'No End Time';
    if ('category' in body) {
        const category = categories.find(item => item['Category Name'] === body.category?.name);
        Object.assign(row, { 'Service Category': category?.['Category Name'] ?? '', 'Service Category Type': category?.['Category Type'] ?? '', 'Service Logo': category?.['Category Icon'] ?? '' });
    }
    if ('serviceSpecificationId' in body) row['Service Specification'] = body.serviceSpecificationId === SAMPLE_SPECIFICATION['Specification ID'] ? SAMPLE_SPECIFICATION.Name : '';
    if ('organizationId' in body) row['Service Organization'] = SAMPLE_ORGANIZATIONS.find(organization => organization['Organization ID'] === body.organizationId)?.['Organization Name'] ?? '';
    if ('monitoringService' in body) row['Monitoring Service'] = body.monitoringService ?? '';
    return row;
}
// UDAPI service items: the SDM ServiceItem properties (Type as SDM enum number) plus the computed implementation states.
const SERVICE_ITEM_TYPE_VALUES: Record<string, number> = { workflow: 0, service: 1, srmbooking: 2 };
const SERVICE_ITEM_TYPE_NAMES = ['Workflow', 'Service', 'SRMBooking'];
function toUdapiServiceItem(row: Record<string, string>) {
    return { ServiceItemID: Number(row['Service Item ID']), Label: row.Label, Type: SERVICE_ITEM_TYPE_VALUES[row['Service Item Type'].replace(/[\s_]/g, '').toLowerCase()] ?? 1, DefinitionReference: row['Definition Reference'] || '', ImplementationReference: row['Implementation Reference'] || '', Script: row['Service Item Script'] || '', Icon: '', ImplementationName: '', ImplementationState: row['Implementation State'] || '', MonitoringServiceState: row['Monitoring Service State'] || '' };
}
function applyServiceItemRequest(row: Record<string, string>, body: any) {
    if ('label' in body) row.Label = String(body.label ?? '').trim();
    if ('type' in body) row['Service Item Type'] = SERVICE_ITEM_TYPE_NAMES[SERVICE_ITEM_TYPE_VALUES[String(body.type || 'Service').replace(/[\s_]/g, '').toLowerCase()] ?? 1];
    if ('definitionReference' in body) row['Definition Reference'] = body.definitionReference ?? '';
    if ('implementationReference' in body) row['Implementation Reference'] = body.implementationReference ?? '';
    if ('script' in body) row['Service Item Script'] = body.script ?? '';
    return row;
}

export async function mockDataMinerApi(page: Page, mode: GqiMode = 'ok'): Promise<GqiMock> {
    const mock: GqiMock = { mode, itemsFail: false, calls: [], ias: { messages: [], continues: [], executeError: null, launchError: null, subscriptError: null } };
    const orders = SAMPLE_ORDERS.map(order => ({ ...order }));
    const orderItems = Object.fromEntries(Object.entries(SAMPLE_ORDER_ITEMS).map(([orderId, items]) => [orderId, items.map(item => ({ ...item }))]));
    const serviceItems = SAMPLE_SERVICE_ITEMS.map(item => ({ ...item }));
    const sessions = new Map<string, { columns: string[]; rows: Record<string, string>[]; offset: number }>();
    const serviceScripts = SAMPLE_SERVICE_SCRIPTS.map(script => ({ ...script }));
    const serviceScriptsById = new Map<string, Record<string, string>[]>([[SAMPLE_SERVICE['Service ID'], serviceScripts]]);
    const serviceItemsByDomId = new Map<string, Record<string, string>[]>([[SAMPLE_SERVICE['DOM ID'], serviceItems]]);
    const serviceConfigurationOverrides = new Map<string, Record<string, unknown>[]>();
    const orderItemConfigurationOverrides = new Map<string, Record<string, unknown>[]>();
    const categories = SAMPLE_CATEGORIES.map(category => ({ ...category }));
    const services: Record<string, string>[] = [{ ...SAMPLE_SERVICE }];
    const monitoringServices = [{ Id: 'monitoring-1', Name: 'Monitoring service 1' }];
    let sessionCount = 0;
    await serveAppAtBasePath(page);
    await mockIasWebSocket(page, mock, orders, serviceItems);
    await page.route('**/API/v1/Json.asmx/IsConnectionAlive', async route => { await json(route, 200, { d: true }); });
    await page.route('**/API/v1/Json.asmx/ExecuteAutomationScript', async route => {
        const body = route.request().postDataJSON() ?? {};
        mock.calls.push({ method: 'ExecuteAutomationScript', body });
        if (body.connection !== CONNECTION_GUID) return json(route, 500, { Message: 'Unexpected connection', ExceptionType: 'System.ArgumentException' });
        const scriptIds: Record<string, number> = { 'SLC_SM_IAS_Add Service Order': IAS_SCRIPT_ID, 'Launch Interactive Subscript': SUBSCRIPT_ID };
        if (!scriptIds[body.script?.Name]) return json(route, 500, { Message: `Unexpected Automation script ${body.script?.Name}`, ExceptionType: 'System.ArgumentException' });
        if (mock.ias.executeError) return json(route, 500, { Message: mock.ias.executeError, ExceptionType: 'Skyline.DataMiner.Web.Common.WebApiException' });
        return json(route, 200, { d: scriptIds[body.script.Name] });
    });
    await page.route('**/API/v1/Json.asmx/DetachAutomationScript', async route => {
        const body = route.request().postDataJSON() ?? {};
        mock.calls.push({ method: 'DetachAutomationScript', body });
        return json(route, 200, { d: null });
    });
    await page.route('**/API/v1/Json.asmx/ExecuteAutomationScriptWithOutput', async route => {
        const body = route.request().postDataJSON() ?? {};
        mock.calls.push({ method: 'ExecuteAutomationScriptWithOutput', body });
        if (body.connection !== CONNECTION_GUID) return json(route, 500, { Message: 'Unexpected connection', ExceptionType: 'System.ArgumentException' });
        if (mock.mode === 'expired') return json(route, 500, { Message: 'No connection was found.', ExceptionType: 'Skyline.DataMiner.Web.Common.NoConnectionWebApiException' });
        const script = body.script ?? {};
        if (script.Name === 'SLC_SM_AS_GetDocumentHubLogos') {
            const parameter = (name: string) => {
                const raw = (script.Parameters ?? []).find((item: any) => item.Name === name)?.Value ?? '';
                return raw === '[]' ? '' : String(raw).replace(/^\["?|"?\]$/g, '');
            };
            const bucketName = parameter('Bucket Name');
            const nameFilter = parameter('Name Filter');
            const files = SAMPLE_DOCUMENT_HUB_LOGOS
                .filter(file => !bucketName || file.Bucket.toLowerCase() === bucketName.toLowerCase())
                .filter(file => !nameFilter || file['File Name'].toLowerCase().includes(nameFilter.toLowerCase()))
                .map(file => ({ bucket: file.Bucket, fileName: file['File Name'], icon: file.Icon, extension: file.Extension, directory: file.Directory }));
            return json(route, 200, { d: { ErrorCode: 0, Output: [{ Name: 'Response', Value: JSON.stringify(files) }] } });
        }
        if (script.Name === 'SLC_SM_UDAPI_ServiceManagement') {
            const raw = (script.Parameters ?? []).find((parameter: any) => parameter.Name === 'Request')?.Value ?? '';
            let request: any;
            try { request = JSON.parse(raw); } catch { return json(route, 200, { d: { ErrorCode: -1, Output: [] } }); }
            const respond = (status: number, response: unknown = '') => json(route, 200, { d: { ErrorCode: 0, Output: [{ Name: 'StatusCode', Value: String(status) }, { Name: 'Response', Value: typeof response === 'string' ? response : JSON.stringify(response) }] } });
            const notFound = () => respond(404, { error: [{ errorCode: 'NotFound', detail: 'Not found', faultingNode: '', title: 'Not found' }] });
            const [resource, id, subResource, subId, subAction] = String(request.Route ?? '').replace(/^service-management\/v1\//, '').split('/').map(decodeURIComponent);
            const requestBody = request.Body ? JSON.parse(request.Body) : {};
            const baseConfigurationRecords = (ownerId: string) => (serviceConfigurationOverrides.get(ownerId) ?? SAMPLE_SPECIFICATION_CONFIGURATIONS.map(row => ({ ...row }))).map(toConfigurationRecord);
            if (resource === 'monitoringService' && request.RequestMethod === 1) return respond(200, monitoringServices);
            if (resource === 'serviceOrder') {
                if (mock.mode === 'error' || (subResource === 'item' && mock.itemsFail)) return respond(500, { error: [{ errorCode: 'InternalServerError', detail: 'DOM manager is unavailable.', faultingNode: '', title: 'Internal server error' }] });
                if (!id && request.RequestMethod === 1) return respond(200, mock.mode === 'empty' ? [] : orders.map(toUdapiOrder));
                const order = orders.find(row => row.OrderId === id);
                if (!order) return notFound();
                if (!subResource && request.RequestMethod === 4) {
                    orders.splice(orders.indexOf(order), 1);
                    delete orderItems[order['Order ID']];
                    return respond(204);
                }
                if (!subResource && request.RequestMethod === 5) {
                    if ('name' in requestBody) order.Name = requestBody.name;
                    return respond(200, toUdapiOrder(order));
                }
                const items = orderItems[order['Order ID']] ??= [];
                if (subResource === 'item' && !subId && request.RequestMethod === 1) return respond(200, items.map(toUdapiOrderItem));
                if (subResource === 'item' && !subId && request.RequestMethod === 3) {
                    const created = { ID: `item-${items.length + 1}`, Name: requestBody.name, Action: requestBody.action, Status: 'New', Description: requestBody.description ?? '', 'Service Specification ID': requestBody.serviceSpecificationId ?? '', 'Service Specification': requestBody.serviceSpecificationId === SAMPLE_SPECIFICATION['Specification ID'] ? SAMPLE_SPECIFICATION.Name : '' };
                    items.push(created);
                    return respond(201, toUdapiOrderItem(created));
                }
                const itemIndex = items.findIndex(item => item.ID === subId);
                if (subResource === 'item' && subId && itemIndex < 0) return notFound();
                if (subResource === 'item' && subId && subAction === 'configuration') {
                    const key = `${id}:${subId}`;
                    if (request.RequestMethod === 1) {
                        const rows = orderItemConfigurationOverrides.get(key) ?? SAMPLE_SPECIFICATION_CONFIGURATIONS.filter(row => !['AvailableProfile', 'AvailableNestedProfile', 'Profile', 'ProfileParameter', 'NestedProfile', 'NestedProfileParameter'].includes(row['Record Type'])).map(row => ({ ...row }));
                        return respond(200, rows.map(toConfigurationRecord));
                    }
                    if (request.RequestMethod === 2) {
                        orderItemConfigurationOverrides.set(key, SAMPLE_SPECIFICATION_CONFIGURATIONS.map(row => ({ ...row })));
                        return respond(200, orderItemConfigurationOverrides.get(key)!.map(toConfigurationRecord));
                    }
                }
                if (subResource === 'item' && subId && subAction === 'initializeService' && request.RequestMethod === 3) {
                    const item = items[itemIndex];
                    const linked = services.find(service => service['DOM ID'] === item.ServiceId);
                    if (linked) return respond(200, toUdapiService(linked));
                    if (!item['Service Specification ID']) {
                        return respond(400, { error: [{ errorCode: 'InvalidRequest', detail: 'A service specification must be assigned before initializing a service.', faultingNode: '', title: 'Invalid request' }] });
                    }
                    const nextServiceNumber = Math.max(0, ...services.map(service => Number(service['Service ID'].match(/^SERVICE-(\d+)$/)?.[1] ?? 0))) + 1;
                    const created = {
                        'DOM ID': `service-${services.length + 1}`,
                        'Service ID': `SERVICE-${nextServiceNumber}`,
                        'Service Name': item.Name || 'New service',
                        'Service Status': 'New',
                        'Service Start': item.Start || 'No Start Time',
                        'Service End': item.End || 'No End Time',
                        'Service Category': categories.find(category => category['Category ID'] === item['Category ID'])?.['Category Name'] ?? '',
                        'Service Category Type': categories.find(category => category['Category ID'] === item['Category ID'])?.['Category Type'] ?? '',
                        'Service Logo': '',
                        'Service Specification': SAMPLE_SPECIFICATION.Name,
                        'Service Organization': '',
                        'Monitoring Service': '',
                    };
                    services.push(created);
                    serviceItemsByDomId.set(created['DOM ID'], []);
                    item.ServiceId = created['DOM ID'];
                    item.Service = created['Service Name'];
                    return respond(201, toUdapiService(created));
                }
                if (subResource === 'item' && subId && subAction === 'transition' && request.RequestMethod === 3) {
                    items[itemIndex].Status = String(requestBody.targetStatus ?? items[itemIndex].Status);
                    return respond(200, toUdapiOrderItem(items[itemIndex]));
                }
                if (subResource === 'item' && request.RequestMethod === 5) {
                    Object.assign(items[itemIndex], { Name: requestBody.name ?? items[itemIndex].Name, Action: requestBody.action ?? items[itemIndex].Action, Description: requestBody.description ?? items[itemIndex].Description });
                    return respond(200, toUdapiOrderItem(items[itemIndex]));
                }
                if (subResource === 'item' && request.RequestMethod === 4) {
                    items.splice(itemIndex, 1);
                    return respond(204);
                }
            }
            if (resource === 'service') {
                if (!id && request.RequestMethod === 1) return respond(200, services.map(service => selectUdapiFields(toUdapiService(service), request)));
                if (!id && request.RequestMethod === 3) {
                    if (services.some(row => row['Service ID'] === requestBody.serviceId)) return respond(409, { error: [{ errorCode: 'Conflict', detail: `Service ID '${requestBody.serviceId}' is already in use.`, faultingNode: '', title: 'Conflict' }] });
                    const created = applyServiceRequest({ 'DOM ID': `service-${services.length + 1}`, 'Service Status': 'New', 'Service Logo': '', 'Service Category': '', 'Service Category Type': '', 'Service Specification': '', 'Service Organization': '', 'Monitoring Service': '' }, requestBody, categories);
                    services.push(created);
                    return respond(201, toUdapiService(created));
                }
                const service = services.find(row => row['Service ID'] === id);
                if (!service) return notFound();
                if (!subResource && request.RequestMethod === 1) return respond(200, selectUdapiFields(toUdapiService(service), request));
                if (!subResource && request.RequestMethod === 4) {
                    services.splice(services.indexOf(service), 1);
                    serviceItemsByDomId.delete(service['DOM ID']);
                    serviceConfigurationOverrides.delete(service['DOM ID']);
                    serviceScriptsById.delete(service['Service ID']);
                    return respond(204);
                }
                if (!subResource && request.RequestMethod === 5) return respond(200, toUdapiService(applyServiceRequest(service, requestBody, categories)));
                if (subResource === 'transition' && request.RequestMethod === 3) {
                    const target = String(requestBody.targetStatus ?? '');
                    service['Service Status'] = target.charAt(0).toUpperCase() + target.slice(1).toLowerCase();
                    return respond(200, toUdapiService(service));
                }
                if (subResource === 'relatedOrder' && request.RequestMethod === 1) {
                    const related = orders.flatMap(order => (orderItems[order['Order ID']] ?? [])
                        .filter(item => item.ServiceId === service['DOM ID'])
                        .map(item => ({
                            OrderInstanceId: order['Order ID'],
                            OrderId: order.OrderId,
                            OrderName: order.Name,
                            ServiceOrderItemId: item.ID,
                            Action: item.Action,
                            State: item.Status,
                        })));
                    return respond(200, related);
                }
                if (subResource === 'configuration') {
                    if (request.RequestMethod === 1) return respond(200, baseConfigurationRecords(service['DOM ID']));
                    if (request.RequestMethod === 2) {
                        serviceConfigurationOverrides.set(service['DOM ID'], SAMPLE_SPECIFICATION_CONFIGURATIONS.map(row => ({ ...row })));
                        return respond(200, baseConfigurationRecords(service['DOM ID']));
                    }
                }
                if (subResource === 'script') {
                    const scripts = serviceScriptsById.get(service['Service ID']) ?? [];
                    const toScript = (scriptRow: Record<string, string>) => ({ Name: scriptRow['Script Name'], Description: scriptRow.Description });
                    if (request.RequestMethod === 1) return respond(200, scripts.map(toScript));
                    const description = String(requestBody.description ?? '').trim();
                    const originalDescription = String(requestBody.originalDescription ?? '').trim();
                    const name = String(requestBody.name ?? '').trim();
                    if (request.RequestMethod === 3) {
                        if (!name || !description) return respond(400, { error: [{ errorCode: 'InvalidRequest', detail: "'name' and 'description' are required.", faultingNode: '', title: 'Invalid request' }] });
                        if (scripts.some(scriptRow => scriptRow.Description.toLowerCase() === description.toLowerCase())) return respond(409, { error: [{ errorCode: 'Conflict', detail: 'A service script with this description already exists.', faultingNode: '', title: 'Conflict' }] });
                        const created = { 'Script Name': name, Description: description };
                        scripts.push(created);
                        serviceScriptsById.set(service['Service ID'], scripts);
                        return respond(201, toScript(created));
                    }
                    const index = scripts.findIndex(scriptRow => scriptRow.Description.toLowerCase() === (request.RequestMethod === 5 ? originalDescription : description).toLowerCase());
                    if (index < 0) return notFound();
                    if (request.RequestMethod === 5) {
                        if (!name || !description) return respond(400, { error: [{ errorCode: 'InvalidRequest', detail: "'originalDescription', 'name' and 'description' are required.", faultingNode: '', title: 'Invalid request' }] });
                        scripts[index] = { 'Script Name': name, Description: description };
                        return respond(200, toScript(scripts[index]));
                    }
                    if (request.RequestMethod === 4) {
                        scripts.splice(index, 1);
                        return respond(204);
                    }
                }
                if (subResource === 'duplicate' && request.RequestMethod === 3) {
                    const nextServiceNumber = Math.max(0, ...services.map(row => Number(row['Service ID'].match(/^SERVICE-(\d+)$/)?.[1] ?? 0))) + 1;
                    const created = {
                        ...service,
                        'DOM ID': `service-${services.length + 1}`,
                        'Service ID': `SERVICE-${nextServiceNumber}`,
                        'Service Name': `${service['Service Name']} (Copy)`,
                        'Service Status': 'New',
                        'Monitoring Service': '',
                    };
                    services.push(created);
                    const sourceItems = serviceItemsByDomId.get(service['DOM ID']) ?? [];
                    serviceItemsByDomId.set(created['DOM ID'], sourceItems.map(item => ({ ...item, 'Implementation Reference': '' })));
                    serviceConfigurationOverrides.set(created['DOM ID'], (serviceConfigurationOverrides.get(service['DOM ID']) ?? SAMPLE_SPECIFICATION_CONFIGURATIONS).map(row => ({ ...row })));
                    serviceScriptsById.set(created['Service ID'], (serviceScriptsById.get(service['Service ID']) ?? []).map(scriptRow => ({ ...scriptRow })));
                    return respond(201, toUdapiService(created));
                }
                if (subResource === 'item') {
                    const items = serviceItemsByDomId.get(service['DOM ID']) ?? [];
                    const conflict = (label: string) => respond(409, { error: [{ errorCode: 'Conflict', detail: `Service item label '${label}' is already in use on service '${id}'.`, faultingNode: '', title: 'Conflict' }] });
                    const labelInUse = (label: string, except?: Record<string, string>) => items.some(row => row !== except && row.Label.toLowerCase() === label.toLowerCase());
                    if (!subId && request.RequestMethod === 1) return respond(200, items.map(toUdapiServiceItem));
                    if (!subId && request.RequestMethod === 3) {
                        const label = String(requestBody.label ?? '').trim();
                        if (labelInUse(label)) return conflict(label);
                        const created = applyServiceItemRequest({ 'Service Item ID': String(Math.max(-1, ...items.map(row => Number(row['Service Item ID']))) + 1), 'Service Item Type': 'Service', 'Implementation State': '', 'Monitoring Service State': '' }, requestBody);
                        items.push(created);
                        serviceItemsByDomId.set(service['DOM ID'], items);
                        return respond(201, toUdapiServiceItem(created));
                    }
                    const item = items.find(row => row['Service Item ID'] === subId);
                    if (!item) return notFound();
                    if (request.RequestMethod === 5) {
                        if ('label' in requestBody && labelInUse(String(requestBody.label).trim(), item)) return conflict(String(requestBody.label).trim());
                        return respond(200, toUdapiServiceItem(applyServiceItemRequest(item, requestBody)));
                    }
                    if (request.RequestMethod === 4) {
                        items.splice(items.indexOf(item), 1);
                        serviceItemsByDomId.set(service['DOM ID'], items);
                        return respond(204);
                    }
                }
            }
            if (resource === 'serviceCategory') {
                const index = categories.findIndex(item => item['Category ID'] === id);
                if (request.RequestMethod === 1 && !id) return respond(200, categories.map(item => ({ id: item['Category ID'], name: item['Category Name'], type: item['Category Type'], icon: item['Category Icon'] })));
                if (request.RequestMethod === 3 && !id) {
                    const created = { 'Category ID': `category-${categories.length + 1}`, 'Category Name': requestBody.name, 'Category Type': requestBody.type, 'Category Icon': requestBody.icon || '' };
                    categories.push(created);
                    return respond(201, { id: created['Category ID'], name: created['Category Name'], type: created['Category Type'], icon: created['Category Icon'] });
                }
                if (request.RequestMethod === 5 && id) {
                    if (index < 0) return notFound();
                    Object.assign(categories[index], { 'Category Name': requestBody.name, 'Category Type': requestBody.type, 'Category Icon': requestBody.icon || '' });
                    return respond(200, { id, name: requestBody.name, type: requestBody.type, icon: requestBody.icon || '' });
                }
                if (request.RequestMethod === 4 && id) {
                    if (index < 0) return notFound();
                    categories.splice(index, 1);
                    return respond(204);
                }
            }
            if (resource === 'serviceSpecification' && request.RequestMethod === 1 && !id) {
                return respond(200, [SAMPLE_SPECIFICATION].map(row => ({ Identifier: row['Specification ID'], Name: row.Name, Description: row.Description, CreatedAt: '2026-09-01T14:40:53Z', CreatedBy: row['Created By'], LastModified: '2026-09-02T22:13:44Z', LastModifiedBy: row['Created By'], ServiceItems: Array.from({ length: Number(row['Service Item Count']) }, (_, index) => ({ ID: index, Label: `Item ${index + 1}` })) })));
            }
            if (resource === 'serviceSpecification' && request.RequestMethod === 1 && id && !subResource) {
                // The DOM definition always stores an empty placeholder item next to the real ones; Type is the SDM enum number.
                return respond(200, { ServiceItems: [
                    { ServiceItemID: null, Label: null, Type: null, DefinitionReference: null, Script: null, ImplementationReference: null },
                    { ServiceItemID: 1, Label: 'Switch booking', Type: 2, DefinitionReference: 'Switching Booking Manager', Script: 'SRM_Booking Wizard', ImplementationReference: null },
                ] });
            }
            if (resource === 'serviceSpecification' && subResource === 'configuration' && [1, 2].includes(request.RequestMethod)) {
                const rows = id === 'unconfigured-spec'
                    ? [...SAMPLE_SPECIFICATION_CONFIGURATIONS.filter(row => !row['Version ID']), { 'Record Type': 'Version', 'Version ID': id, 'Version Name': 'Unconfigured', 'Is Active': 'true' }]
                    : SAMPLE_SPECIFICATION_CONFIGURATIONS;
                return respond(200, rows.map(toConfigurationRecord));
            }
            if (resource === 'serviceSpecification' && [3, 4, 5].includes(request.RequestMethod)) return respond(request.RequestMethod === 3 ? 201 : request.RequestMethod === 4 ? 204 : 200, request.RequestMethod === 4 ? '' : { Identifier: id || 'spec-new', Name: requestBody.name ?? '' });
            return respond(405, { error: [{ errorCode: 'MethodNotAllowed', detail: `Unsupported request ${request.RequestMethod} ${request.Route}`, faultingNode: '', title: 'Method not allowed' }] });
        }
        if (script.Name !== 'SLC_SM_IAS_Add Service Order' && script.Name !== 'SLC_SM_IAS_Manage Service Scripts' && script.Name !== 'SLC_SM_AS_ManageServiceCategory' && script.Name !== 'SLC_SM_AS_DeleteServiceCategory') return json(route, 500, { Message: `Unexpected Automation script ${script.Name}`, ExceptionType: 'System.ArgumentException' });
        const values = Object.fromEntries((script.Parameters ?? []).map((parameter: any) => {
            let value = '';
            try { value = JSON.parse(parameter.Value ?? '[]')?.[0] ?? ''; } catch { value = ''; }
            return [parameter.Name, value];
        }));
        if (script.Name === 'SLC_SM_IAS_Add Service Order') return json(route, 200, { d: { ErrorCode: 0, Output: [] } });
        if (script.Name === 'SLC_SM_IAS_Manage Service Scripts') {
            if (values.Action === 'Add') serviceScripts.push({ 'Script Name': values['Script Name'], Description: values['Script Description'] });
            if (values.Action === 'Update') {
                const index = serviceScripts.findIndex(scriptRow => scriptRow.Description === values['Original Script Description']);
                if (index >= 0) serviceScripts[index] = { 'Script Name': values['Script Name'], Description: values['Script Description'] };
            }
            if (values.Action === 'Remove') {
                const index = serviceScripts.findIndex(scriptRow => scriptRow.Description === values['Original Script Description']);
                if (index >= 0) serviceScripts.splice(index, 1);
            }
        } else {
            const payload = JSON.parse(values.Payload || '{}');
            if (values.Action === 'Add') categories.push({ 'Category ID': `category-${categories.length + 1}`, 'Category Name': payload.Name, 'Category Type': payload.Type, 'Category Icon': payload.Icon || '' });
            if (values.Action === 'Edit') {
                const category = categories.find(item => item['Category ID'] === values.domId);
                if (category) Object.assign(category, { 'Category Name': payload.Name, 'Category Type': payload.Type, 'Category Icon': payload.Icon || '' });
            }
            if (values.Action === 'Delete') {
                const index = categories.findIndex(item => item['Category ID'] === values.domId);
                if (index >= 0) categories.splice(index, 1);
            }
        }
        return json(route, 200, { d: { ErrorCode: 0, Output: [] } });
    });
    await page.route('**/API/v1/Internal.asmx/*', async route => {
        const method = new URL(route.request().url()).pathname.split('/').pop() ?? '';
        const body = route.request().postDataJSON() ?? {};
        mock.calls.push({ method, body });
        if (body.connection !== CONNECTION_GUID) return json(route, 500, { Message: 'Unexpected connection', ExceptionType: 'System.ArgumentException' });
        if (mock.mode === 'expired') return json(route, 500, { Message: 'No connection was found.', ExceptionType: 'Skyline.DataMiner.Web.Common.NoConnectionWebApiException' });
        if (method === 'OpenQuerySession') {
            const query = body.query ?? {};
            const descriptor = JSON.parse(query.Options?.[0]?.Value?.Value ?? '{}');
            const args = Object.fromEntries((query.Child?.Options?.[0]?.Value ?? []).map((option: any) => [option.ID, option.Value?.Value]));
            const isOrders = descriptor.ScriptName === 'SLC_SM_GQIDS_Get Service Orders' && descriptor.TypeFullName === ORDERS_TYPE;
            const isRelatedServiceOrders = descriptor.ScriptName === 'SLC_SM_GQIDS_Get Related Service Orders' && descriptor.TypeFullName === RELATED_SERVICE_ORDERS_TYPE && typeof args['Service ID'] === 'string';
            const isItems = descriptor.ScriptName === 'SLC_SM_GQIDS_Get Service Order Items' && descriptor.TypeFullName === ITEMS_TYPE && typeof args['DOM ID'] === 'string';
            const isServices = descriptor.ScriptName === 'SLC_SM_GQIDS_Get Services' && descriptor.TypeFullName === SERVICES_TYPE;
            const isMonitoringServices = descriptor.ScriptName === 'SLC_SM_GQIDS_Get Monitoring Services' && descriptor.TypeFullName === MONITORING_SERVICES_TYPE;
            const isDocumentHubLogos = descriptor.ScriptName === 'SLC_SM_DS_GetDocumentHubLogos' && descriptor.TypeFullName === DOCUMENT_HUB_LOGOS_TYPE;
            const isOrganizations = descriptor.ScriptName === 'SLC_SM_GQIDS_Get Service Organizations' && descriptor.TypeFullName === ORGANIZATIONS_TYPE;
            const isCategories = descriptor.ScriptName === 'SLC_SM_GQIDS_Get Service Categories';
            const isServiceScripts = descriptor.ScriptName === 'SLC_SM_GQIDS_Get Service Scripts' && descriptor.TypeFullName === SERVICE_SCRIPTS_TYPE && typeof args['Service Name'] === 'string';
            const isSpecifications = descriptor.ScriptName === 'SLC_SM_GQIDS_Get Service Specifications' && descriptor.TypeFullName === SPECIFICATIONS_TYPE;
            const isServiceItems = descriptor.ScriptName === 'SLC_SM_GQIDS_Get Service Items' && descriptor.TypeFullName === SERVICE_ITEMS_TYPE && typeof args['DOM ID'] === 'string';
            const isConfigurations = descriptor.ScriptName === 'SLC_SM_GQIDS_Get Service Configurations' && descriptor.TypeFullName === CONFIGURATIONS_TYPE && typeof args['DOM ID'] === 'string' && ['Service', 'Specification'].includes(args['Configuration Type']);
            const valid = query.__type === 'Skyline.DataMiner.Web.Common.v1.DMAGenericInterfaceQuery' && query.ID === 'Custom data' && query.Options?.[0]?.ID === 'Data source' && query.Child?.ID === 'Custom data arguments' && (isOrders || isRelatedServiceOrders || isItems || isServices || isMonitoringServices || isDocumentHubLogos || isOrganizations || isCategories || isServiceScripts || isSpecifications || isServiceItems || isConfigurations);
            if (!valid) return json(route, 500, { Message: 'Unexpected GQI query', ExceptionType: 'System.ArgumentException' });
            if (mock.mode === 'error' || (isItems && mock.itemsFail)) return json(route, 500, { Message: `Data source "${isItems ? 'Get_ServiceOrderItems' : 'Get_ServiceOrders'}" failed: DOM manager is unavailable.`, ExceptionType: 'Skyline.DataMiner.Web.Common.WebApiException' });
            const id = `session-${++sessionCount}`;
            const relatedServiceOrderColumns = ['Order ID', 'Order Name', 'Service Order Item ID', 'Action', 'State'];
            const relatedServiceOrderRows = args['Service ID'] === 'service-1'
                ? [{ 'Order ID': SAMPLE_ORDERS[0]['Order ID'], 'Order Name': SAMPLE_ORDERS[0].Name, 'Service Order Item ID': 'a1', Action: 'Add', State: 'In Progress' }]
                : [];
            const configurationRows = args['DOM ID'] === 'unconfigured-spec'
                ? SAMPLE_SPECIFICATION_CONFIGURATIONS.filter(row => !row['Version ID'])
                : SAMPLE_SPECIFICATION_CONFIGURATIONS;
            const columns = isOrders ? COLUMNS : isRelatedServiceOrders ? relatedServiceOrderColumns : isItems ? ITEM_COLUMNS : isServices ? SERVICE_COLUMNS : isMonitoringServices ? MONITORING_SERVICE_COLUMNS : isDocumentHubLogos ? DOCUMENT_HUB_COLUMNS : isOrganizations ? ORGANIZATION_COLUMNS : isCategories ? CATEGORY_COLUMNS : isServiceScripts ? SERVICE_SCRIPT_COLUMNS : isSpecifications ? SPECIFICATION_COLUMNS : isServiceItems ? SERVICE_ITEM_COLUMNS : CONFIGURATION_COLUMNS;
            const rows = isOrders ? mock.mode === 'empty' ? [] : orders : isRelatedServiceOrders ? relatedServiceOrderRows : isItems ? orderItems[args['DOM ID']] ?? [] : isServices ? services : isMonitoringServices ? [{ 'Monitoring Service ID': 'monitoring-1', 'Monitoring Service Name': 'Monitoring service 1' }] : isDocumentHubLogos ? SAMPLE_DOCUMENT_HUB_LOGOS : isOrganizations ? SAMPLE_ORGANIZATIONS : isCategories ? categories : isServiceScripts ? serviceScripts : isSpecifications ? [SAMPLE_SPECIFICATION] : isServiceItems ? args['DOM ID'] === SAMPLE_SERVICE['DOM ID'] ? serviceItems : [] : isConfigurations ? configurationRows : SAMPLE_SPECIFICATION_CONFIGURATIONS;
            sessions.set(id, { columns, rows, offset: 0 });
            return json(route, 200, { d: { __type: 'Skyline.DataMiner.Web.Common.v1.DMAGenericInterfaceSessionInfo', ID: id, Columns: columns.map(name => ({ Name: name, ID: name, ClientType: 'string', ServerType: 'string', IsDiscreet: false, Discreets: [] })) } });
        }
        if (method === 'GetNextQuerySessionPage') {
            const session = sessions.get(body.sessionID);
            if (!session) return json(route, 500, { Message: 'Unknown session', ExceptionType: 'System.ArgumentException' });
            const rows = session.rows.slice(session.offset, session.offset + PAGE_SIZE);
            session.offset += rows.length;
            return json(route, 200, { d: { Rows: rows.map(row => ({ Cells: session.columns.map(name => ({ Value: row[name], DisplayValue: row[name] })) })), IsLast: session.offset >= session.rows.length } });
        }
        if (method === 'CloseQuerySession') { sessions.delete(body.sessionID); return json(route, 200, { d: null }); }
        return json(route, 500, { Message: `Unexpected method ${method}`, ExceptionType: 'System.InvalidOperationException' });
    });
    return mock;
}
