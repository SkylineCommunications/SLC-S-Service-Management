// Dev-only DataMiner Web API stand-in for `npm run dev` (apply: 'serve'), so the UI can be previewed live without an
// Agent. It answers the same Json.asmx / Internal.asmx calls the app makes and is never part of `vite build`.
const CONNECTION = 'dev-connection';
const ORDER_COLUMNS = ['Order ID', 'Name', 'Description', 'Priority', 'External ID', 'Related Organization', 'State', 'Start'];
const ITEM_COLUMNS = ['ID', 'Name', 'Start', 'End', 'Action', 'Category', 'Service Specification', 'Service', 'ServiceId', 'Property', 'Configuration', 'Status', 'StatusId', 'Description'];
const SERVICE_COLUMNS = ['DOM ID', 'Service ID', 'Service Name', 'Service Status', 'Service Start', 'Service End', 'Service Category', 'Service Category Type', 'Service Logo', 'Service Specification', 'Alarm Level'];
const SERVICE_ITEM_COLUMNS = ['Actions', 'Label', 'Service Item ID', 'Service Item Type', 'Definition Reference', 'Service Item Script', 'Implementation Reference', 'Implementation Reference Name', 'Implementation Reference Link', 'Implementation Reference Has Value', 'Implementation Reference Name Has Value', 'Implementation Reference Link Has Value', 'Implementation State', 'Implementation Reference Custom Link', 'Implementation Reference Custom Link Has Value', 'Monitoring Service State', 'Monitoring Service DMA ID/SID', 'Log'];
const SPECIFICATION_COLUMNS = ['Specification ID', 'Name', 'Description', 'Created At', 'Created By', 'Last Modified', 'Last Modified By', 'Service Item Count'];
const STATES = ['New', 'Acknowledged', 'In Progress', 'Held', 'Pending', 'Completed', 'Completed', 'Cancelled', 'Rejected', 'Failed', 'Partially Failed', 'Assess Cancellation', 'Pending Cancellation'];
const PRIORITIES = ['High', 'Medium', 'Low'];
const ORGANIZATIONS = ['Skyline Broadcast', 'Acme Media', 'Northwind Telecom', 'Fox OTT', ''];
const NAMES = ['Network refresh', 'Studio expansion', 'Backup link', 'FOX OTT distribution', 'Satellite uplink', 'Cloud playout', 'Managed IP transit', 'Event production', 'Contribution feed', 'Video transport'];
const ACTIONS = ['Add', 'Modify', 'Delete'];
const CATEGORIES = ['Connectivity', 'Playout', 'Contribution'];

function guid(seed) {
    const hex = Array.from({ length: 32 }, (_, i) => ((seed * 2654435761 + i * 40503) >>> (i % 8)) % 16).map(n => n.toString(16)).join('');
    return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-4${hex.slice(13, 16)}-8${hex.slice(17, 20)}-${hex.slice(20)}`;
}

let ORDERS = Array.from({ length: 42 }, (_, i) => {
    const year = 2024 + (i % 3);
    const number = String(100 + i).padStart(6, '0');
    return {
        'Order ID': guid(i + 1),
        Name: i % 4 === 0 ? `SO-${year}-${number}` : `${NAMES[i % NAMES.length]}${i >= NAMES.length ? ` ${Math.floor(i / NAMES.length) + 1}` : ''}`,
        Description: i % 3 === 0 ? '' : `${NAMES[i % NAMES.length]} for ${ORGANIZATIONS[i % 4]}`,
        Priority: PRIORITIES[i % 3],
        'External ID': i % 5 === 0 ? '' : `SO-${year}-${number}`,
        'Related Organization': ORGANIZATIONS[i % ORGANIZATIONS.length],
        State: STATES[i % STATES.length],
        Start: `9/${(i % 9) + 1}/2026 ${String((i % 9) + 1)}:00:00 AM`,
    };
});

const SERVICE_STATES = ['Active', 'Reserved', 'Designed', 'Terminated', 'New', 'Retired'];
const SERVICE_CATEGORIES = ['Connectivity', 'Playout', 'Contribution', 'Production'];
const SERVICE_SPECIFICATIONS = ['Managed switching', 'Cloud playout', 'Video transport', 'Managed IP transit'];
const SERVICES = Array.from({ length: 24 }, (_, i) => {
    const specification = SERVICE_SPECIFICATIONS[i % SERVICE_SPECIFICATIONS.length];
    return {
        'DOM ID': guid(3000 + i),
        'Service ID': `SVC-${String(200 + i).padStart(5, '0')}`,
        'Service Name': `${specification} ${i + 1}`,
        'Service Status': SERVICE_STATES[i % SERVICE_STATES.length],
        'Service Start': `9/${(i % 9) + 1}/2026 8:00:00 AM`,
        'Service End': i % 5 === 0 ? 'No End Time' : `9/${(i % 9) + 8}/2026 6:00:00 PM`,
        'Service Category': SERVICE_CATEGORIES[i % SERVICE_CATEGORIES.length],
        'Service Category Type': i % 2 === 0 ? 'Channel' : 'Service',
        'Service Logo': '',
        'Service Specification': specification,
        'Alarm Level': i % 7 === 0 ? '2' : '0',
    };
});
const SPECIFICATIONS = SERVICE_SPECIFICATIONS.map((name, index) => ({
    'Specification ID': guid(4000 + index),
    Name: name,
    Description: `${name} service specification`,
    'Created At': `9/${index + 1}/2026 2:40:53 PM`,
    'Created By': 'Sulejman',
    'Last Modified': `9/${index + 2}/2026 10:13:44 PM`,
    'Last Modified By': 'Sulejman',
    'Service Item Count': String((index % 3) + 2),
}));

const EXTRA_ITEMS = new Map();

function serviceItemsFor(serviceId) {
    const service = SERVICES.find(item => item['DOM ID'] === serviceId);
    const specification = SPECIFICATIONS.find(item => item['Specification ID'] === serviceId);
    const name = service?.['Service Name'] || specification?.Name;
    const definition = service?.['Service Specification'] || specification?.Name;
    if (!name) return [];
    return [
        { Actions: '', Label: `${name} workflow`, 'Service Item ID': '1', 'Service Item Type': 'Service', 'Definition Reference': definition, 'Service Item Script': '', 'Implementation Reference': '', 'Implementation Reference Name': '', 'Implementation Reference Link': '', 'Implementation Reference Has Value': 'false', 'Implementation Reference Name Has Value': 'false', 'Implementation Reference Link Has Value': 'false', 'Implementation State': 'New', 'Implementation Reference Custom Link': '', 'Implementation Reference Custom Link Has Value': 'false', 'Monitoring Service State': 'Not monitored', 'Monitoring Service DMA ID/SID': '', Log: '' },
        { Actions: '', Label: `${name} monitoring`, 'Service Item ID': '2', 'Service Item Type': 'Service', 'Definition Reference': definition, 'Service Item Script': '', 'Implementation Reference': '', 'Implementation Reference Name': '', 'Implementation Reference Link': '', 'Implementation Reference Has Value': 'false', 'Implementation Reference Link Has Value': 'false', 'Implementation Reference Name Has Value': 'false', 'Implementation State': 'Active', 'Implementation Reference Custom Link': '', 'Implementation Reference Custom Link Has Value': 'false', 'Monitoring Service State': 'Active', 'Monitoring Service DMA ID/SID': '', Log: '' },
    ];
}

function itemsFor(orderId) {
    if (EXTRA_ITEMS.has(orderId)) return EXTRA_ITEMS.get(orderId);
    const index = ORDERS.findIndex(order => order['Order ID'] === orderId);
    if (index < 0) return [];
    return Array.from({ length: (index % 4) + 1 }, (_, i) => ({
        ID: guid(1000 + index * 10 + i), Name: `${ORDERS[index].Name} item ${i + 1}`,
        Start: i % 2 ? 'No Start Time' : `9/${i + 1}/2026 8:00:00 AM`, End: i % 2 ? 'No End Time' : `9/${i + 2}/2026 6:00:00 PM`,
        Action: ACTIONS[i % 3], Category: CATEGORIES[(index + i) % 3], 'Service Specification': `${CATEGORIES[(index + i) % 3]} spec`,
        Service: i % 2 ? '' : `${ORDERS[index].Name} service`, ServiceId: '', Property: '', Configuration: '',
        Status: STATES[(index + i) % STATES.length], StatusId: '', Description: '',
    }));
}

function readBody(req) { return new Promise(resolve => { let data = ''; req.on('data', chunk => { data += chunk; }); req.on('end', () => { try { resolve(JSON.parse(data || '{}')); } catch { resolve({}); } }); }); }
function send(res, status, body) { res.statusCode = status; res.setHeader('Content-Type', 'application/json'); res.end(JSON.stringify(body)); }
function scriptParameter(parameters, name) {
    const raw = parameters.find(parameter => parameter.Name === name)?.Value;
    if (!raw || raw === 'null' || raw === '[]') return '';
    try {
        const values = JSON.parse(raw);
        return Array.isArray(values) ? values[0] || '' : String(values);
    } catch {
        return raw;
    }
}

export default function mockDataMiner() {
    const sessions = new Map();
    let sessionCount = 0;
    return {
        name: 'mock-dataminer',
        apply: 'serve',
        configureServer(server) {
            server.config.logger.info('  [mock-dataminer] Serving sample service orders (dev only).');
            server.middlewares.use(async (req, res, next) => {
                if (!/(?:^|;\s*)DMAConnection=/.test(req.headers.cookie || '')) res.setHeader('Set-Cookie', `DMAConnection=${CONNECTION}; Path=/`);
                const path = (req.url || '').split('?')[0];
                if (path === '/auth/' || path === '/auth/logout') { res.statusCode = 302; res.setHeader('Location', '/'); return res.end(); }
                if (path === '/API/v1/Json.asmx/IsConnectionAlive') return send(res, 200, { d: true });
                if (path === '/API/v1/Json.asmx/ExecuteAutomationScriptWithOutput') {
                    const body = await readBody(req);
                    await new Promise(resolve => setTimeout(resolve, 300));
                    if (body.connection !== CONNECTION) return send(res, 500, { Message: 'Unexpected connection' });
                    const script = body.script || {};
                    const parameters = script.Parameters || [];
                    const values = Object.fromEntries(parameters.map(parameter => [parameter.Name, scriptParameter(parameters, parameter.Name)]));
                    if (script.Name === 'SLC_SM_IAS_Add Service Order' && values.Action === 'Add') {
                        const index = ORDERS.length + 100;
                        const year = 2026;
                        const number = String(index).padStart(6, '0');
                        ORDERS.unshift({ 'Order ID': guid(5000 + index), Name: `New service order ${number}`, Description: '', Priority: 'Medium', 'External ID': `SO-${year}-${number}`, 'Related Organization': '', State: 'New' });
                    } else if (script.Name === 'SLC_SM_Delete Service Order') {
                        ORDERS = ORDERS.filter(order => order['Order ID'] !== values['DOM ID']);
                        EXTRA_ITEMS.delete(values['DOM ID']);
                    } else if (script.Name === 'SLC_SM_IAS_Add Service Order Item' && values.Action === 'Add') {
                        const existing = [...itemsFor(values['DOM ID'])];
                        const itemNumber = existing.length + 1;
                        existing.push({ ID: guid(9000 + itemNumber), Name: `New order item ${itemNumber}`, Start: 'No Start Time', End: 'No End Time', Action: 'Add', Category: '', 'Service Specification': '', Service: '', ServiceId: '', Property: '', Configuration: '', Status: 'New', StatusId: '', Description: '' });
                        EXTRA_ITEMS.set(values['DOM ID'], existing);
                    } else if (script.Name === 'SLC_SM_Delete Service Order Item') {
                        const existing = itemsFor(values['DOM ID']).filter(item => item.ID !== values['Service Order Item ID']);
                        EXTRA_ITEMS.set(values['DOM ID'], existing);
                    }
                    return send(res, 200, { d: { ErrorCode: 0, Output: [] } });
                }
                if (!path.startsWith('/API/v1/Internal.asmx/')) return next();
                const method = path.split('/').pop();
                const body = await readBody(req);
                await new Promise(resolve => setTimeout(resolve, 300));
                if (method === 'OpenQuerySession') {
                    const descriptor = JSON.parse(body.query?.Options?.[0]?.Value?.Value || '{}');
                    const args = Object.fromEntries((body.query?.Child?.Options?.[0]?.Value || []).map(option => [option.ID, option.Value?.Value]));
                    const isItems = descriptor.ScriptName === 'SLC_SM_GQIDS_Get Service Order Items';
                    const isServices = descriptor.ScriptName === 'SLC_SM_GQIDS_Get Services' || descriptor.ScriptName === 'SLC_SM_DS_GetServicesByCharacteristic';
                    const isServiceItems = descriptor.ScriptName === 'SLC_SM_GQIDS_Get Service Items';
                    const isServiceScripts = descriptor.ScriptName === 'SLC_SM_GQIDS_Get Service Scripts';
                    const isSpecifications = descriptor.ScriptName === 'SLC_SM_GQIDS_Get Service Specifications';
                    if (!isItems && !isServices && !isServiceItems && !isServiceScripts && !isSpecifications && descriptor.ScriptName !== 'SLC_SM_GQIDS_Get Service Orders') return send(res, 500, { Message: `Unknown data source ${descriptor.ScriptName}` });
                    const id = `dev-session-${++sessionCount}`;
                    const columns = isItems ? ITEM_COLUMNS : isServices ? SERVICE_COLUMNS : isServiceItems ? SERVICE_ITEM_COLUMNS : isServiceScripts ? ['Script Name', 'Description'] : isSpecifications ? SPECIFICATION_COLUMNS : ORDER_COLUMNS;
                    const rows = isServiceScripts ? [{ 'Script Name': 'Refresh service', Description: 'Refreshes the service implementation.' }, { 'Script Name': 'Restart service', Description: 'Restarts the service implementation.' }] : isItems ? itemsFor(args['DOM ID']) : isServices ? SERVICES : isServiceItems ? serviceItemsFor(args['DOM ID']) : isSpecifications ? SPECIFICATIONS : ORDERS;
                    sessions.set(id, { columns, rows, offset: 0 });
                    return send(res, 200, { d: { ID: id, Columns: columns.map(name => ({ Name: name })) } });
                }
                if (method === 'GetNextQuerySessionPage') {
                    const session = sessions.get(body.sessionID);
                    if (!session) return send(res, 500, { Message: 'Unknown session' });
                    const rows = session.rows.slice(session.offset, session.offset + (body.pageSize || 500));
                    session.offset += rows.length;
                    return send(res, 200, { d: { Rows: rows.map(row => ({ Cells: session.columns.map(name => ({ Value: row[name] })) })), IsLast: session.offset >= session.rows.length } });
                }
                if (method === 'CloseQuerySession') { sessions.delete(body.sessionID); return send(res, 200, { d: null }); }
                return send(res, 500, { Message: `Unexpected method ${method}` });
            });
        },
    };
}
