import type { BrowserContext, Page, Route } from '@playwright/test';

// DataMiner hosts the build (vite outDir .../Webpages/public/service-management) at /public/service-management/.
export const APP_BASE = '/public/service-management/';
export const APP_URL = `${APP_BASE}index.html`;
export const CONNECTION_GUID = '00000000-1111-4222-8333-444444444444';
// vite preview serves the build at '/', so map the DataMiner base path onto the preview root. Without this, relative
// asset URLs under the base path hit the SPA fallback (index.html instead of JS/CSS) and the app never starts.
export async function serveAppAtBasePath(page: Page): Promise<void> { await page.route(`**${APP_BASE}**`, async route => { const url = new URL(route.request().url()); url.pathname = url.pathname.slice(APP_BASE.length - 1); await route.continue({ url: url.toString() }); }); }
export async function setSessionCookie(context: BrowserContext): Promise<void> { await context.addCookies([{ name: 'DMAConnection', value: CONNECTION_GUID, domain: 'localhost', path: '/' }]); }

// Sample rows returned by the mocked "Get_ServiceOrders" GQI ad hoc data source.
export const SAMPLE_ORDERS = [
    { 'Order ID': '6f1c2a90-1b2c-4d3e-8f40-5a6b7c8d9e01', Name: 'Network refresh', Description: 'Replace core switches', Priority: 'High', 'External ID': 'SO-1042', 'Related Organization': 'Skyline Broadcast', State: 'In Progress' },
    { 'Order ID': '7a2d3b81-2c3d-4e5f-9a51-6b7c8d9e0f12', Name: 'Studio expansion', Description: 'Add studio B', Priority: 'Medium', 'External ID': '', 'Related Organization': 'Acme Media', State: 'New' },
    { 'Order ID': '8b3e4c72-3d4e-4f60-ab62-7c8d9e0f1a23', Name: 'Backup link', Description: 'Secondary contribution link', Priority: 'Low', 'External ID': 'SO-1038', 'Related Organization': 'Skyline Broadcast', State: 'Completed' },
];
// Deliberately not in the C# column order: the app must map cells by column name, not by index.
const COLUMNS = ['State', 'Name', 'Order ID', 'Related Organization', 'Priority', 'External ID', 'Description'];
// Rows returned by the mocked "Get_ServiceOrderItems" data source, keyed by the "DOM ID" argument.
export const SAMPLE_ORDER_ITEMS: Record<string, Record<string, string>[]> = {
    [SAMPLE_ORDERS[0]['Order ID']]: [
        { ID: 'a1', Name: 'Core switch A', Start: '9/1/2026 8:00:00 AM', End: '9/2/2026 6:00:00 PM', Action: 'Add', Category: 'Connectivity', 'Service Specification': 'Managed switching', Service: 'Core network', Status: 'In Progress', Description: 'Primary site' },
        { ID: 'a2', Name: 'Core switch B', Start: 'No Start Time', End: 'No End Time', Action: 'Modify', Category: 'Connectivity', 'Service Specification': 'Managed switching', Service: '', Status: 'New', Description: '' },
    ],
};
const ITEM_COLUMNS = ['Status', 'ID', 'Name', 'Start', 'End', 'Action', 'Category', 'Service Specification', 'Service', 'Description'];
const ORDERS_TYPE = 'SLC_SM_GQIDS_Get_Service_Orders.GetServiceOrders';
const ITEMS_TYPE = 'SLC_SM_GQIDS_Get_Service_Order_Items_1.EventManagerGetMultipleSections';
const PAGE_SIZE = 2;

export type GqiMode = 'ok' | 'empty' | 'error' | 'expired';
export interface GqiMock { mode: GqiMode; itemsFail: boolean; calls: { method: string; body: Record<string, any> }[] }

async function json(route: Route, status: number, body: unknown): Promise<void> { await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) }); }

export async function mockDataMinerApi(page: Page, mode: GqiMode = 'ok'): Promise<GqiMock> {
    const mock: GqiMock = { mode, itemsFail: false, calls: [] };
    const sessions = new Map<string, { columns: string[]; rows: Record<string, string>[]; offset: number }>();
    let sessionCount = 0;
    await serveAppAtBasePath(page);
    await page.route('**/API/v1/Json.asmx/IsConnectionAlive', async route => { await json(route, 200, { d: true }); });
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
            const isItems = descriptor.ScriptName === 'SLC_SM_GQIDS_Get Service Order Items' && descriptor.TypeFullName === ITEMS_TYPE && typeof args['DOM ID'] === 'string';
            const valid = query.__type === 'Skyline.DataMiner.Web.Common.v1.DMAGenericInterfaceQuery' && query.ID === 'Custom data' && query.Options?.[0]?.ID === 'Data source' && query.Child?.ID === 'Custom data arguments' && (isOrders || isItems);
            if (!valid) return json(route, 500, { Message: 'Unexpected GQI query', ExceptionType: 'System.ArgumentException' });
            if (mock.mode === 'error' || (isItems && mock.itemsFail)) return json(route, 500, { Message: `Data source "${isItems ? 'Get_ServiceOrderItems' : 'Get_ServiceOrders'}" failed: DOM manager is unavailable.`, ExceptionType: 'Skyline.DataMiner.Web.Common.WebApiException' });
            const id = `session-${++sessionCount}`;
            const columns = isItems ? ITEM_COLUMNS : COLUMNS;
            sessions.set(id, { columns, rows: isItems ? SAMPLE_ORDER_ITEMS[args['DOM ID']] ?? [] : mock.mode === 'empty' ? [] : SAMPLE_ORDERS, offset: 0 });
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
