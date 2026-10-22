// Dev-only DataMiner Web API stand-in for `npm run dev` (apply: 'serve'), so the UI can be previewed live without an
// Agent. It answers the same Json.asmx / Internal.asmx calls the app makes and is never part of `vite build`.
const CONNECTION = 'dev-connection';
const ORDER_COLUMNS = ['Order ID', 'Name', 'Description', 'Priority', 'External ID', 'Related Organization', 'State', 'Start', 'Created At'];
const ITEM_COLUMNS = ['ID', 'Name', 'Start', 'End', 'Action', 'Category', 'Category ID', 'Service Specification', 'Service Specification ID', 'Service', 'ServiceId', 'Property', 'Configuration', 'Status', 'StatusId', 'Description'];
const SERVICE_COLUMNS = ['DOM ID', 'Service ID', 'Service Name', 'Service Status', 'Service Start', 'Service End', 'Service Category', 'Service Category Type', 'Service Logo', 'Service Specification', 'Alarm Level', 'Service Organization', 'Monitoring Service', 'Related Service Orders', 'Related Service Order Count'];
const SERVICE_ITEM_COLUMNS = ['Actions', 'Label', 'Service Item ID', 'Service Item Type', 'Definition Reference', 'Service Item Script', 'Implementation Reference', 'Implementation Reference Name', 'Implementation Reference Link', 'Implementation Reference Has Value', 'Implementation Reference Name Has Value', 'Implementation Reference Link Has Value', 'Implementation State', 'Implementation Reference Custom Link', 'Implementation Reference Custom Link Has Value', 'Monitoring Service State', 'Monitoring Service DMA ID/SID', 'Log'];
const SERVICE_CONFIG_COLUMNS = ['Version ID', 'Version Name', 'Version Description', 'Version Start', 'Version End', 'Is Active', 'Record Type', 'Configuration ID', 'Parameter ID', 'Parameter Value ID', 'Parameter Name', 'Parameter Type', 'Parameter Value', 'Mandatory', 'Profile ID', 'Profile Name', 'Profile Definition ID', 'Profile Definition Name', 'Profile Reusable', 'Profile Mandatory', 'Nested Profile ID', 'Nested Profile Name', 'Nested Profile Definition ID', 'Nested Profile Definition Name', 'Parameter Label', 'Parameter Is Linked', 'Parameter Unit', 'Parameter Unit ID', 'Parameter Start', 'Parameter End', 'Parameter Step Size', 'Parameter Decimals', 'Parameter Options', 'Parameter Fixed', 'Parameter N/A', 'Expose At Order', 'Mandatory At Order', 'Mandatory At Service'];
const SPECIFICATION_COLUMNS = ['Specification ID', 'Name', 'Description', 'Created At', 'Created By', 'Last Modified', 'Last Modified By', 'Service Item Count'];
const ORGANIZATION_COLUMNS = ['Organization ID', 'Organization Name'];
const MONITORING_SERVICE_COLUMNS = ['Monitoring Service ID', 'Monitoring Service Name'];
const DOCUMENT_HUB_COLUMNS = ['Bucket', 'File Name', 'Icon', 'Extension', 'Directory'];
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
        'Created At': `8/${(i % 28) + 1}/2026 10:00:00 AM`,
    };
});

const SERVICE_STATES = ['Active', 'Reserved', 'Designed', 'Terminated', 'New', 'Retired'];
const SERVICE_CATEGORIES = ['Connectivity', 'Playout', 'Contribution', 'Production'];
const SERVICE_SPECIFICATIONS = ['Managed switching', 'Cloud playout', 'Video transport', 'Managed IP transit'];
const SERVICES = Array.from({ length: 24 }, (_, i) => {
    const specification = SERVICE_SPECIFICATIONS[i % SERVICE_SPECIFICATIONS.length];
    return {
        'DOM ID': guid(3000 + i),
        'Service ID': `SERVICE-${200 + i}`,
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
let CATEGORY_ROWS = SERVICE_CATEGORIES.map((name, index) => ({
    'Category ID': guid(4500 + index),
    'Category Name': name,
    'Category Type': index % 2 === 0 ? 'Channel' : 'Service',
    'Category Icon': '',
}));
const ORGANIZATION_ROWS = [...new Set(ORGANIZATIONS.filter(Boolean))].map((name, index) => ({
    'Organization ID': guid(4700 + index),
    'Organization Name': name,
}));
const DOCUMENT_HUB_LOGOS = [
    { Bucket: 'Default', 'File Name': 'connectivity.svg', Icon: '/Public/DocumentHub/connectivity.svg', Extension: '.svg', Directory: '/Public/DocumentHub' },
    { Bucket: 'Default', 'File Name': 'production.svg', Icon: '/Public/DocumentHub/production.svg', Extension: '.svg', Directory: '/Public/DocumentHub' },
];

const EXTRA_ITEMS = new Map();
const SERVICE_SCRIPT_OVERRIDES = new Map();
const SERVICE_CONFIGURATION_OVERRIDES = new Map();
const SERVICE_WITHOUT_CONFIGURATION_ID = SERVICES[SERVICES.length - 1]['DOM ID'];

function serviceItemsFor(serviceId) {
    if (EXTRA_ITEMS.has(serviceId)) return EXTRA_ITEMS.get(serviceId);
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

function serviceScriptsFor(serviceId) {
    if (SERVICE_SCRIPT_OVERRIDES.has(serviceId)) return SERVICE_SCRIPT_OVERRIDES.get(serviceId);
    return [{ 'Script Name': 'Refresh service', Description: 'Refreshes the service implementation.' }, { 'Script Name': 'Restart service', Description: 'Restarts the service implementation.' }];
}

function relatedOrdersForService(serviceId) {
    return ORDERS
        .filter(order => itemsFor(order['Order ID']).some(item => item.ServiceId === serviceId))
        .map(order => order.Name || order['Order ID']);
}

function relatedOrderRowsForService(serviceId) {
    return ORDERS.flatMap(order => itemsFor(order['Order ID'])
        .filter(item => item.ServiceId === serviceId)
        .map(item => ({
            'Order ID': order['Order ID'],
            'Order Name': order.Name || order['Order ID'],
            'Service Order Item ID': item.ID || '',
            Action: item.Action || '',
            State: item.Status || '',
        })));
}

function serviceConfigurationsFor(serviceId, { includeProfiles = true } = {}) {
    if (SERVICE_CONFIGURATION_OVERRIDES.has(serviceId)) {
        const overridden = SERVICE_CONFIGURATION_OVERRIDES.get(serviceId);
        return includeProfiles ? overridden : overridden.filter(row => !['AvailableProfile', 'AvailableNestedProfile', 'Profile', 'ProfileParameter', 'NestedProfile', 'NestedProfileParameter'].includes(row['Record Type']));
    }
    const hasConfigurationVersion = serviceId !== SERVICE_WITHOUT_CONFIGURATION_ID;
    const versionId = guid(7000 + SERVICES.findIndex(service => service['DOM ID'] === serviceId));
    const discreteOptions = '[{"Id":"disabled","Value":"Disabled"},{"Id":"enabled","Value":"Enabled"}]';
    const parameters = [
        ['Bitrate', 'Number', '2500', 'false', 7200, ''],
        ['Protocol', 'Text', 'SRT', 'true', 7201, ''],
        ['Redundant', 'Discrete', 'Enabled', 'false', 7203, discreteOptions],
    ];
    const rows = hasConfigurationVersion ? parameters.map((parameter, index) => ({
        'Version ID': versionId,
        'Version Name': 'Current version',
        'Version Description': 'Current service configuration',
        'Version Start': '9/1/2026 8:00:00 AM',
        'Version End': 'No End Time',
        'Is Active': 'true',
        'Record Type': 'Parameter',
        'Configuration ID': guid(7100 + index),
        'Parameter ID': guid(parameter[4]),
        'Parameter Value ID': guid(7300 + index),
        'Parameter Name': parameter[0],
        'Parameter Type': parameter[1],
        'Parameter Value': parameter[2],
        'Parameter Label': parameter[0],
        'Parameter Is Linked': 'false',
        'Parameter Unit': '',
        'Parameter Unit ID': '',
        'Parameter Start': parameter[1] === 'Number' ? '-10000' : '',
        'Parameter End': parameter[1] === 'Number' ? '10000' : '',
        'Parameter Step Size': parameter[1] === 'Number' ? '1' : '',
        'Parameter Decimals': parameter[1] === 'Number' ? '0' : '',
        'Parameter Options': parameter[5],
        Mandatory: parameter[3],
        'Profile ID': '',
        'Profile Name': '',
        'Profile Definition ID': '',
        'Profile Definition Name': '',
        'Profile Reusable': 'false',
        'Profile Mandatory': 'false',
    })) : [];
    rows.push(
        { 'Version ID': '', 'Record Type': 'AvailableParameter', 'Parameter ID': guid(7200), 'Parameter Name': 'Bitrate', 'Parameter Type': 'Number' },
        { 'Version ID': '', 'Record Type': 'AvailableParameter', 'Parameter ID': guid(7201), 'Parameter Name': 'Protocol', 'Parameter Type': 'Text' },
        { 'Version ID': '', 'Record Type': 'AvailableParameter', 'Parameter ID': guid(7202), 'Parameter Name': 'Latency', 'Parameter Type': 'Number' },
        { 'Version ID': '', 'Record Type': 'AvailableParameter', 'Parameter ID': guid(7203), 'Parameter Name': 'Redundant', 'Parameter Type': 'Discrete', 'Parameter Options': discreteOptions },
    );
    if (!includeProfiles) return rows;
    rows.push(
        { 'Version ID': '', 'Record Type': 'AvailableProfile', 'Profile Definition ID': guid(7400), 'Profile Definition Name': 'Transport profile' },
        { 'Version ID': '', 'Record Type': 'AvailableProfile', 'Profile Definition ID': guid(7401), 'Profile Definition Name': 'Encapsulation profile' },
        { 'Version ID': '', 'Record Type': 'AvailableProfile', 'Profile Definition ID': guid(7402), 'Profile Definition Name': 'Timing profile' },
        { 'Version ID': '', 'Record Type': 'AvailableProfile', 'Profile Definition ID': guid(7403), 'Profile Definition Name': 'Monitoring profile' },
        { 'Version ID': '', 'Record Type': 'AvailableNestedProfile', 'Nested Profile ID': guid(7601), 'Nested Profile Name': 'Reusable encapsulation profile', 'Nested Profile Definition ID': guid(7401), 'Nested Profile Definition Name': 'Encapsulation profile', 'Profile Reusable': 'true' },
        { 'Version ID': '', 'Record Type': 'AvailableNestedProfile', 'Nested Profile ID': guid(7602), 'Nested Profile Name': 'Reusable timing profile', 'Nested Profile Definition ID': guid(7402), 'Nested Profile Definition Name': 'Timing profile', 'Profile Reusable': 'true' },
        { 'Version ID': '', 'Record Type': 'AvailableNestedProfile', 'Nested Profile ID': guid(7603), 'Nested Profile Name': 'Reusable monitoring profile', 'Nested Profile Definition ID': guid(7403), 'Nested Profile Definition Name': 'Monitoring profile', 'Profile Reusable': 'true' },
    );
    if (!hasConfigurationVersion) return rows;
    const rootProfileId = guid(7600);
    const childProfileId = guid(7601);
    const grandchildProfileId = guid(7602);
    const greatGrandchildProfileId = guid(7603);
    const profileBase = { 'Version ID': versionId, 'Version Name': 'Current version', 'Version Description': 'Current service configuration', 'Version Start': '9/1/2026 8:00:00 AM', 'Version End': 'No End Time', 'Is Active': 'true', 'Configuration ID': '', 'Parameter ID': '', 'Parameter Value ID': '', 'Parameter Name': '', 'Parameter Type': '', 'Parameter Value': '', 'Parameter Label': '', 'Parameter Is Linked': 'false', 'Parameter Unit': '', 'Parameter Unit ID': '', 'Parameter Start': '', 'Parameter End': '', 'Parameter Step Size': '', 'Parameter Decimals': '', 'Parameter Options': '', Mandatory: 'false', 'Profile ID': '', 'Profile Name': '', 'Profile Definition ID': '', 'Profile Definition Name': '', 'Profile Reusable': 'false', 'Profile Mandatory': 'true', 'Nested Profile ID': '', 'Nested Profile Name': '', 'Nested Profile Definition ID': '', 'Nested Profile Definition Name': '' };
    rows.push(
        { ...profileBase, 'Record Type': 'Profile', 'Profile ID': rootProfileId, 'Profile Name': 'Transport profile', 'Profile Definition ID': guid(7400), 'Profile Definition Name': 'Transport profile' },
        { ...profileBase, 'Record Type': 'NestedProfile', 'Profile ID': rootProfileId, 'Profile Name': 'Transport profile', 'Profile Definition ID': guid(7400), 'Profile Definition Name': 'Transport profile', 'Nested Profile ID': childProfileId, 'Nested Profile Name': 'Reusable encapsulation profile', 'Nested Profile Definition ID': guid(7401), 'Nested Profile Definition Name': 'Encapsulation profile', 'Parameter ID': guid(7202), 'Parameter Value ID': guid(7302), 'Parameter Name': 'Latency', 'Parameter Type': 'Number', 'Parameter Value': '50' },
        { ...profileBase, 'Record Type': 'NestedProfile', 'Profile ID': childProfileId, 'Profile Name': 'Reusable encapsulation profile', 'Profile Definition ID': guid(7401), 'Profile Definition Name': 'Encapsulation profile', 'Nested Profile ID': grandchildProfileId, 'Nested Profile Name': 'Reusable timing profile', 'Nested Profile Definition ID': guid(7402), 'Nested Profile Definition Name': 'Timing profile', 'Profile Reusable': 'true', 'Parameter ID': guid(7200), 'Parameter Value ID': guid(7300), 'Parameter Name': 'Bitrate', 'Parameter Type': 'Number', 'Parameter Value': '2500' },
        { ...profileBase, 'Record Type': 'NestedProfile', 'Profile ID': grandchildProfileId, 'Profile Name': 'Reusable timing profile', 'Profile Definition ID': guid(7402), 'Profile Definition Name': 'Timing profile', 'Nested Profile ID': greatGrandchildProfileId, 'Nested Profile Name': 'Reusable monitoring profile', 'Nested Profile Definition ID': guid(7403), 'Nested Profile Definition Name': 'Monitoring profile', 'Profile Reusable': 'true', 'Parameter ID': guid(7203), 'Parameter Value ID': guid(7303), 'Parameter Name': 'Redundant', 'Parameter Type': 'Discrete', 'Parameter Value': 'Enabled', 'Parameter Options': discreteOptions },
    );
    return rows;
}

function configurationRowsFromPayload(serviceId, payload, { includeProfiles = true } = {}) {
    const existingRows = SERVICE_CONFIGURATION_OVERRIDES.get(serviceId) || serviceConfigurationsFor(serviceId, { includeProfiles });
    const profileRecordTypes = new Set(['AvailableProfile', 'AvailableNestedProfile', 'Profile', 'ProfileParameter', 'NestedProfile', 'NestedProfileParameter']);
    const rows = existingRows.filter(row => !row['Version ID'] && (includeProfiles || !profileRecordTypes.has(row['Record Type'])));
    const versionId = payload.VersionId || guid(7000 + SERVICES.findIndex(service => service['DOM ID'] === serviceId));
    const field = (value, name) => value?.[name] ?? value?.[`${name[0].toLowerCase()}${name.slice(1)}`];
    const versionFields = {
        'Version ID': versionId,
        'Version Name': payload.VersionName || 'Current version',
        'Version Description': payload.Description || '',
        'Version Start': payload.StartDate || '9/1/2026 8:00:00 AM',
        'Version End': payload.EndDate || 'No End Time',
        'Is Active': String(Boolean(payload.ActivateVersion)),
    };
    let generatedId = 0;
    const idFor = value => value || guid(9000 + generatedId++);
    const parameterRow = (recordType, parameter, profileFields = {}) => {
        const parameterId = field(parameter, 'ParameterId');
        const available = rows.find(row => row['Record Type'] === 'AvailableParameter' && row['Parameter ID'] === parameterId) || {};
        return {
            ...versionFields,
            'Record Type': recordType,
            'Configuration ID': recordType === 'Parameter' ? field(parameter, 'ConfigurationId') || idFor('') : '',
            'Parameter ID': parameterId || available['Parameter ID'] || '',
            'Parameter Value ID': parameterId ? field(parameter, 'ParameterValueId') || idFor('') : '',
            'Parameter Name': field(parameter, 'Name') || available['Parameter Name'] || '',
            'Parameter Type': field(parameter, 'Type') || available['Parameter Type'] || '',
            'Parameter Value': field(parameter, 'Value') ?? '',
            'Parameter Label': field(parameter, 'Label') || available['Parameter Label'] || field(parameter, 'Name') || available['Parameter Name'] || '',
            'Parameter Is Linked': String(field(parameter, 'IsLinked') ?? false),
            'Parameter Unit': field(parameter, 'Unit') || available['Parameter Unit'] || '',
            'Parameter Unit ID': field(parameter, 'UnitId') || available['Parameter Unit ID'] || '',
            'Parameter Start': field(parameter, 'Start') || available['Parameter Start'] || '',
            'Parameter End': field(parameter, 'End') || available['Parameter End'] || '',
            'Parameter Step Size': field(parameter, 'StepSize') || available['Parameter Step Size'] || '',
            'Parameter Decimals': field(parameter, 'Decimals') || available['Parameter Decimals'] || '',
            'Parameter Options': available['Parameter Options'] || field(parameter, 'Options') || '',
            Mandatory: String(field(parameter, 'Mandatory') ?? false),
            'Profile ID': profileFields.profileId || '',
            'Profile Name': profileFields.profileName || '',
            'Profile Definition ID': profileFields.profileDefinitionId || '',
            'Profile Definition Name': profileFields.profileDefinitionName || '',
            'Profile Reusable': String(profileFields.profileReusable ?? false),
            'Profile Mandatory': String(profileFields.profileMandatory ?? false),
            'Nested Profile ID': profileFields.nestedProfileId || '',
            'Nested Profile Name': profileFields.nestedProfileName || '',
            'Nested Profile Definition ID': profileFields.nestedProfileDefinitionId || '',
            'Nested Profile Definition Name': profileFields.nestedProfileDefinitionName || '',
        };
    };
    (payload.Parameters || []).forEach(parameter => rows.push(parameterRow('Parameter', parameter)));
    const appendNested = (parent, child, parentId) => {
        const childId = idFor(field(child, 'Id'));
        const parentFields = {
            profileId: parentId,
            profileName: field(parent, 'Name') || '',
            profileDefinitionId: field(parent, 'DefinitionId') || '',
            profileDefinitionName: field(parent, 'DefinitionName') || '',
            profileReusable: Boolean(field(parent, 'Reusable')),
        };
        const nestedFields = {
            ...parentFields,
            nestedProfileId: childId,
            nestedProfileName: field(child, 'Name') || '',
            nestedProfileDefinitionId: field(child, 'DefinitionId') || '',
            nestedProfileDefinitionName: field(child, 'DefinitionName') || '',
            profileReusable: Boolean(field(parent, 'Reusable')),
        };
        const childParameters = field(child, 'Parameters') || [];
        if (childParameters.length === 0) rows.push(parameterRow('NestedProfile', {}, nestedFields));
        else childParameters.forEach(parameter => rows.push(parameterRow('NestedProfile', parameter, nestedFields)));
        (field(child, 'NestedProfiles') || []).forEach(nested => appendNested(child, nested, childId));
    };
    if (includeProfiles) (payload.Profiles || []).forEach(profile => {
        const profileId = idFor(field(profile, 'Id'));
        const profileFields = {
            profileId,
            profileName: field(profile, 'Name') || '',
            profileDefinitionId: field(profile, 'DefinitionId') || '',
            profileDefinitionName: field(profile, 'DefinitionName') || '',
            profileReusable: Boolean(field(profile, 'Reusable')),
            profileMandatory: Boolean(field(profile, 'Mandatory')),
        };
        const profileParameters = field(profile, 'Parameters') || [];
        if (profileParameters.length === 0) rows.push(parameterRow('Profile', {}, profileFields));
        else profileParameters.forEach(parameter => rows.push(parameterRow('Profile', parameter, profileFields)));
        (field(profile, 'NestedProfiles') || []).forEach(nested => appendNested(profile, nested, profileId));
    });
    if (!rows.some(row => row['Version ID'] === versionId)) rows.push(parameterRow('Version', {}));
    return rows;
}

function itemsFor(orderId) {
    if (EXTRA_ITEMS.has(orderId)) return EXTRA_ITEMS.get(orderId);
    const index = ORDERS.findIndex(order => order['Order ID'] === orderId);
    if (index < 0) return [];
    return Array.from({ length: (index % 4) + 1 }, (_, i) => {
        const linkedService = i % 2 ? null : SERVICES[(index + i) % SERVICES.length];
        return {
            ID: guid(1000 + index * 10 + i), Name: `${ORDERS[index].Name} item ${i + 1}`,
            Start: i % 2 ? 'No Start Time' : `9/${i + 1}/2026 8:00:00 AM`, End: i % 2 ? 'No End Time' : `9/${i + 2}/2026 6:00:00 PM`,
            Action: ACTIONS[i % 3], Category: CATEGORIES[(index + i) % 3], 'Category ID': CATEGORY_ROWS[(index + i) % CATEGORY_ROWS.length]['Category ID'], 'Service Specification': `${CATEGORIES[(index + i) % 3]} spec`, 'Service Specification ID': SPECIFICATIONS[(index + i) % SPECIFICATIONS.length]['Specification ID'],
            Service: linkedService?.['Service Name'] || '', ServiceId: linkedService?.['DOM ID'] || '', Property: '', Configuration: '',
            Status: STATES[(index + i) % STATES.length], StatusId: '', Description: '',
        };
    });
}

function isServiceOrderItemId(id) {
    return ORDERS.some(order => itemsFor(order['Order ID']).some(item => item.ID === id));
}

function initializeServiceFromOrderItem(orderItemId) {
    for (const order of ORDERS) {
        const items = itemsFor(order['Order ID']);
        const item = items.find(candidate => candidate.ID === orderItemId);
        if (!item) continue;
        if (String(item.Action).toLowerCase() === 'delete') return { error: 'A service cannot be initialized for a delete order item.' };
        const specification = SPECIFICATIONS.find(candidate => candidate['Specification ID'] === item['Service Specification ID']);
        if (!specification) return { error: 'A service specification must be assigned before initializing a service.' };
        if (item.ServiceId && SERVICES.some(service => service['DOM ID'] === item.ServiceId)) return { serviceId: item.ServiceId };

        const category = CATEGORY_ROWS.find(candidate => candidate['Category ID'] === item['Category ID']);
        const highestServiceNumber = SERVICES.reduce((highest, service) => {
            const suffix = Number(String(service['Service ID'] || '').split('-').pop());
            return Number.isFinite(suffix) ? Math.max(highest, suffix) : highest;
        }, 0);
        const service = {
            'DOM ID': guid(8500 + SERVICES.length),
            'Service ID': `SERVICE-${String(highestServiceNumber + 1).padStart(5, '0')}`,
            'Service Name': item.Name || 'New service',
            'Service Status': 'New',
            'Service Start': item.Start || 'No Start Time',
            'Service End': item.End || 'No End Time',
            'Service Category': category?.['Category Name'] || item.Category || '',
            'Service Category Type': category?.['Category Type'] || '',
            'Service Logo': category?.['Category Icon'] || '',
            'Service Specification': specification.Name,
            'Alarm Level': '0',
        };
        SERVICES.push(service);
        item.Service = service['Service Name'];
        item.ServiceId = service['DOM ID'];
        EXTRA_ITEMS.set(order['Order ID'], items);
        return { serviceId: service['DOM ID'] };
    }
    return { error: 'The selected service order item could not be found.' };
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

// GQI configuration row -> UDAPI SpecificationConfigurationRecord (camelCase keys).
function toConfigurationRecord(row) {
    return Object.fromEntries(SERVICE_CONFIG_COLUMNS.map(column => [
        column === 'Parameter N/A' ? 'parameterNotApplicable' : column.split(' ').map((word, index) => index === 0 ? word.toLowerCase() : word[0] + word.slice(1).toLowerCase()).join(''),
        row[column] ?? '',
    ]));
}

function toIsoDate(value) {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? '' : date.toISOString();
}

const ORDER_NUMBERS = new Map();
let nextOrderItemSeed = 9500;
function orderIdFor(domId) {
    if (!ORDER_NUMBERS.has(domId)) ORDER_NUMBERS.set(domId, `ORDER-${String(ORDER_NUMBERS.size + 1).padStart(6, '0')}`);
    return ORDER_NUMBERS.get(domId);
}

const camelCase = value => String(value || '').replace(/\s+(\w)/g, (_, letter) => letter.toUpperCase()).replace(/^\w/, letter => letter.toLowerCase());
const udapiReference = id => id ? { Identifier: id } : null;

function toUdapiOrder(row) {
    const organizationId = ORGANIZATION_ROWS.find(organization => organization['Organization Name'] === row['Related Organization'])?.['Organization ID'] || null;
    const start = toIsoDate(row.Start);
    const end = toIsoDate(row.End);
    return {
        Identifier: row['Order ID'],
        OrderId: orderIdFor(row['Order ID']),
        Name: row.Name,
        Description: row.Description,
        Priority: row.Priority,
        ExternalID: row['External ID'],
        StatusName: camelCase(row.State),
        OrganizationId: organizationId,
        OrganizationName: row['Related Organization'] || null,
        CompletionInfo: start || end ? { RequestedStartDate: start || null, RequestedCompletedDate: end || null, CompletionDate: null } : null,
        CreatedAt: toIsoDate(row['Created At']) || null,
    };
}

function toUdapiOrderItem(row) {
    const start = toIsoDate(row.Start);
    const end = toIsoDate(row.End);
    return {
        Identifier: row.ID,
        Name: row.Name,
        Action: row.Action,
        Description: row.Description,
        StatusName: camelCase(row.Status),
        StartTime: start || null,
        EndTime: end || null,
        IndefiniteRuntime: !end,
        ServiceInfo: { ServiceCategoryId: udapiReference(row['Category ID']), SpecificationId: udapiReference(row['Service Specification ID']), ServiceId: udapiReference(row.ServiceId) },
        ServiceCategoryName: row.Category || null,
        ServiceSpecificationName: row['Service Specification'] || null,
        ServiceName: row.Service || null,
    };
}

// Applies an order item create/update request (camelCase keys) to a GQI-shaped item row.
function applyOrderItemRequest(item, body) {
    const has = key => Object.prototype.hasOwnProperty.call(body, key);
    if (has('name')) item.Name = body.name;
    if (has('action')) item.Action = body.action;
    if (has('description')) item.Description = body.description || '';
    if (has('startTime')) item.Start = body.startTime || 'No Start Time';
    if (has('endTime') || body.indefiniteRuntime) item.End = body.indefiniteRuntime ? 'No End Time' : body.endTime || 'No End Time';
    if (has('serviceCategoryId')) {
        const category = CATEGORY_ROWS.find(row => row['Category ID'] === body.serviceCategoryId);
        Object.assign(item, { 'Category ID': category?.['Category ID'] || '', Category: category?.['Category Name'] || '' });
    }
    if (has('serviceSpecificationId')) {
        const specification = SPECIFICATIONS.find(row => row['Specification ID'] === body.serviceSpecificationId);
        Object.assign(item, { 'Service Specification ID': specification?.['Specification ID'] || '', 'Service Specification': specification?.Name || '' });
    }
    if (has('serviceId')) {
        const service = SERVICES.find(row => row['DOM ID'] === body.serviceId);
        Object.assign(item, { ServiceId: service?.['DOM ID'] || '', Service: service?.['Service Name'] || '' });
    }
    return item;
}

function toUdapiService(row) {
    const organizationId = ORGANIZATION_ROWS.find(organization => organization['Organization Name'] === row['Service Organization'])?.['Organization ID'] || null;
    return {
        Identifier: row['DOM ID'],
        ServiceID: row['Service ID'],
        Name: row['Service Name'],
        StatusName: String(row['Service Status'] || '').toLowerCase(),
        StartTime: toIsoDate(row['Service Start']) || null,
        EndTime: toIsoDate(row['Service End']) || null,
        Icon: row['Service Logo'] || null,
        MonitoringService: row['Monitoring Service'] || null,
        CategoryName: row['Service Category'] || null,
        CategoryType: row['Service Category Type'] || null,
        ServiceSpecificationName: row['Service Specification'] || null,
        OrganizationId: organizationId,
        OrganizationName: organizationId ? row['Service Organization'] : null,
    };
}

// Applies a service create/update request (camelCase keys) to a GQI-shaped service row.
function applyServiceRequest(row, body) {
    const has = key => Object.prototype.hasOwnProperty.call(body, key);
    if (has('serviceId')) row['Service ID'] = body.serviceId;
    if (has('name')) row['Service Name'] = body.name;
    if (has('startTime')) row['Service Start'] = body.startTime || 'No Start Time';
    if (has('endTime')) row['Service End'] = body.endTime || 'No End Time';
    if (has('category')) {
        const category = CATEGORY_ROWS.find(candidate => candidate['Category Name'] === body.category?.name || candidate['Category ID'] === body.category?.id);
        Object.assign(row, { 'Service Category': category?.['Category Name'] || '', 'Service Category Type': category?.['Category Type'] || '', 'Service Logo': category?.['Category Icon'] || '' });
    }
    if (has('serviceSpecificationId')) row['Service Specification'] = SPECIFICATIONS.find(candidate => candidate['Specification ID'] === body.serviceSpecificationId)?.Name || '';
    if (has('organizationId')) row['Service Organization'] = ORGANIZATION_ROWS.find(candidate => candidate['Organization ID'] === body.organizationId)?.['Organization Name'] || '';
    if (has('monitoringService')) row['Monitoring Service'] = body.monitoringService || '';
    return row;
}

// Emulates the SLC_SM_UDAPI_ServiceManagement Run entry point for the routes used by the app.
function handleUdapiRequest(request) {
    const error = (status, title, detail) => ({ status, response: { error: [{ errorCode: title.replace(/\s/g, ''), detail, faultingNode: '', title }] } });
    const toCategory = row => ({ id: row['Category ID'], name: row['Category Name'], type: row['Category Type'], icon: row['Category Icon'] });
    const [resource, id, subResource, subId, subAction] = String(request.Route || '').replace(/^service-management\/v1\//i, '').split('/').map(decodeURIComponent);
    let body = {};
    try { body = request.Body ? JSON.parse(request.Body) : {}; } catch { return error(400, 'Invalid request', 'The request body is not valid JSON.'); }
    const now = new Date().toLocaleString('en-US');
    if (resource === 'monitoringService' && request.RequestMethod === 1) {
        return {
            status: 200,
            response: SERVICES.map(service => ({ Id: service['DOM ID'], Name: service['Service Name'] })),
        };
    }
    if (resource === 'serviceCategory') {
        const category = id ? CATEGORY_ROWS.find(row => row['Category ID'] === id) : null;
        if (id && !category) return error(404, 'Not found', `Service category '${id}' was not found.`);
        if (request.RequestMethod === 1) return { status: 200, response: category ? toCategory(category) : CATEGORY_ROWS.map(toCategory) };
        if (request.RequestMethod === 3 && !id) {
            if (!body.name?.trim() || !body.type?.trim()) return error(400, 'Invalid request', 'Name and type are required.');
            const created = { 'Category ID': guid(4700 + CATEGORY_ROWS.length), 'Category Name': body.name.trim(), 'Category Type': body.type.trim(), 'Category Icon': body.icon?.trim() || '' };
            CATEGORY_ROWS.push(created);
            return { status: 201, response: toCategory(created) };
        }
        if (request.RequestMethod === 5 && category) {
            if ('name' in body && !body.name?.trim()) return error(400, 'Invalid request', "'name' cannot be empty.");
            if ('type' in body && !body.type?.trim()) return error(400, 'Invalid request', "'type' cannot be empty.");
            if ('name' in body) category['Category Name'] = body.name.trim();
            if ('type' in body) category['Category Type'] = body.type.trim();
            if ('icon' in body) category['Category Icon'] = body.icon?.trim() || '';
            return { status: 200, response: toCategory(category) };
        }
        if (request.RequestMethod === 4 && category) {
            CATEGORY_ROWS = CATEGORY_ROWS.filter(row => row !== category);
            return { status: 204 };
        }
    }
    if (resource === 'serviceSpecification') {
        const index = id ? SPECIFICATIONS.findIndex(row => row['Specification ID'] === id) : -1;
        if (id && index < 0) return error(404, 'Not found', `Service specification '${id}' was not found.`);
        const toSpecification = row => ({
            Identifier: row['Specification ID'],
            Name: row.Name,
            Description: row.Description,
            CreatedAt: toIsoDate(row['Created At']),
            CreatedBy: row['Created By'],
            LastModified: toIsoDate(row['Last Modified']),
            LastModifiedBy: row['Last Modified By'],
            ServiceItems: Array.from({ length: Number(row['Service Item Count']) || 0 }, (_, itemIndex) => ({ ID: itemIndex, Label: `Item ${itemIndex + 1}` })),
        });
        if (subResource === 'configuration' && id) {
            if (request.RequestMethod === 2) {
                SERVICE_CONFIGURATION_OVERRIDES.set(id, configurationRowsFromPayload(id, body));
                Object.assign(SPECIFICATIONS[index], { 'Last Modified': now, 'Last Modified By': 'Sulejman' });
            }
            if ([1, 2].includes(request.RequestMethod)) return { status: 200, response: serviceConfigurationsFor(id).map(toConfigurationRecord) };
        }
        if (request.RequestMethod === 1 && !subResource) {
            if (!id) return { status: 200, response: SPECIFICATIONS.map(toSpecification) };
            const itemTypes = { Workflow: 0, Service: 1, SRMBooking: 2, 'SRM Booking': 2 };
            const ServiceItems = serviceItemsFor(id).map(row => ({ ServiceItemID: Number(row['Service Item ID']), Label: row.Label, Type: itemTypes[row['Service Item Type']] ?? 1, DefinitionReference: row['Definition Reference'], Script: row['Service Item Script'], ImplementationReference: row['Implementation Reference'] }));
            const specification = { ...toSpecification(SPECIFICATIONS[index]), ServiceItems };
            const fields = (request.QueryParameters?.fields || []).join(',').split(',').map(field => field.trim().toLowerCase()).filter(Boolean);
            return { status: 200, response: fields.length ? Object.fromEntries(Object.entries(specification).filter(([key]) => fields.includes(key.toLowerCase()))) : specification };
        }
        if (request.RequestMethod === 3 && !id) {
            if (!body.name?.trim()) return error(400, 'Invalid request', 'Name is required.');
            const created = { 'Specification ID': guid(4900 + SPECIFICATIONS.length), Name: body.name.trim(), Description: body.description || '', 'Created At': now, 'Created By': 'Sulejman', 'Last Modified': now, 'Last Modified By': 'Sulejman', 'Service Item Count': '0' };
            SPECIFICATIONS.push(created);
            return { status: 201, response: { Identifier: created['Specification ID'], Name: created.Name, Description: created.Description } };
        }
        if (request.RequestMethod === 5 && id) {
            const specification = SPECIFICATIONS[index];
            if ('name' in body) specification.Name = body.name;
            if ('description' in body) specification.Description = body.description;
            Object.assign(specification, { 'Last Modified': now, 'Last Modified By': 'Sulejman' });
            return { status: 200, response: { Identifier: id, Name: specification.Name, Description: specification.Description } };
        }
        if (request.RequestMethod === 4 && id) {
            SPECIFICATIONS.splice(index, 1);
            return { status: 204 };
        }
    }
    if (resource === 'serviceOrder') {
        if (!id && request.RequestMethod === 1) return { status: 200, response: ORDERS.map(toUdapiOrder) };
        const order = id ? ORDERS.find(row => orderIdFor(row['Order ID']) === id) : null;
        if (id && !order) return error(404, 'Not found', `Service order '${id}' does not exist.`);
        if (order && !subResource && request.RequestMethod === 4) {
            ORDERS = ORDERS.filter(candidate => candidate !== order);
            EXTRA_ITEMS.delete(order['Order ID']);
            return { status: 204 };
        }
        if (order && !subResource && request.RequestMethod === 5) {
            if (!['New', 'Acknowledged', 'Pending', 'Held'].includes(order.State)) return error(409, 'Conflict', `Service order '${id}' cannot be updated in status '${order.State}'. Allowed statuses: new, acknowledged, pending, held.`);
            if ('name' in body && !body.name?.trim()) return error(400, 'Invalid request', "'name' cannot be empty.");
            if ('name' in body) order.Name = body.name.trim();
            if ('description' in body) order.Description = body.description || '';
            if ('externalId' in body) order['External ID'] = body.externalId || '';
            if ('priority' in body) order.Priority = PRIORITIES.find(priority => priority.toLowerCase() === String(body.priority).toLowerCase()) || order.Priority;
            if ('organizationId' in body) order['Related Organization'] = ORGANIZATION_ROWS.find(row => row['Organization ID'] === body.organizationId)?.['Organization Name'] || '';
            if ('completionInfo' in body) Object.assign(order, { Start: body.completionInfo?.requestedStartDate || '', End: body.completionInfo?.requestedCompletedDate || '' });
            return { status: 200, response: toUdapiOrder(order) };
        }
        if (order && subResource === 'transition' && request.RequestMethod === 3) {
            const targetStates = {
                Inprogress: 'In Progress',
                Partial: 'Partially Failed',
                Assesscancellation: 'Assess Cancellation',
                Pendingcancellation: 'Pending Cancellation',
            };
            const target = targetStates[body.targetStatus] || body.targetStatus;
            const allowed = {
                New: ['Acknowledged', 'Rejected'],
                Acknowledged: ['In Progress', 'Rejected'],
                'In Progress': ['Completed', 'Failed', 'Partially Failed', 'Held', 'Pending'],
                Pending: ['Assess Cancellation', 'In Progress'],
                'Assess Cancellation': ['Pending Cancellation', 'Held', 'Pending'],
                'Pending Cancellation': ['Cancelled'],
                Held: ['In Progress', 'Assess Cancellation'],
            };
            if (!allowed[order.State]?.includes(target)) return error(409, 'Conflict', `Service order '${id}' cannot transition from '${order.State}' to '${target}'.`);
            order.State = target;
            return { status: 200, response: toUdapiOrder(order) };
        }
        if (order && subResource === 'item') {
            const items = [...itemsFor(order['Order ID'])];
            if (body.action !== undefined && !['add', 'modify', 'delete', 'nochange'].includes(String(body.action).toLowerCase())) return error(400, 'Invalid request', "'action' must be one of: add, modify, delete, noChange.");
            if (!subId && request.RequestMethod === 1) return { status: 200, response: items.map(toUdapiOrderItem) };
            if (!subId && request.RequestMethod === 3) {
                if (!['New', 'Acknowledged'].includes(order.State)) return error(409, 'Conflict', `Items cannot be added to service order '${id}' in status '${order.State}'.`);
                if (!body.name?.trim()) return error(400, 'Invalid request', "'name' is required.");
                const created = applyOrderItemRequest({ ID: guid(nextOrderItemSeed++), Name: '', Start: 'No Start Time', End: 'No End Time', Action: 'Add', Category: '', 'Category ID': '', 'Service Specification': '', 'Service Specification ID': '', Service: '', ServiceId: '', Property: '', Configuration: '', Status: 'New', StatusId: '', Description: '' }, body);
                EXTRA_ITEMS.set(order['Order ID'], [...items, created]);
                return { status: 201, response: toUdapiOrderItem(created) };
            }
            const item = subId ? items.find(row => row.ID === subId) : null;
            if (subId && !item) return error(404, 'Not found', `Service order item '${subId}' does not exist on service order '${id}'.`);
            if (item && subAction === 'configuration') {
                if (request.RequestMethod === 1) return { status: 200, response: serviceConfigurationsFor(subId, { includeProfiles: false }).map(toConfigurationRecord) };
                if (request.RequestMethod === 2) {
                    SERVICE_CONFIGURATION_OVERRIDES.set(subId, configurationRowsFromPayload(subId, body, { includeProfiles: false }));
                    return { status: 200, response: serviceConfigurationsFor(subId, { includeProfiles: false }).map(toConfigurationRecord) };
                }
            }
            if (item && subAction === 'initializeService' && request.RequestMethod === 3) {
                const result = initializeServiceFromOrderItem(subId);
                if (result.error) return error(400, 'Invalid request', result.error);
                const service = SERVICES.find(candidate => candidate['DOM ID'] === result.serviceId);
                return service
                    ? { status: 201, response: toUdapiService(service) }
                    : error(500, 'Internal server error', 'The initialized service could not be found.');
            }
            if (item && subAction === 'transition' && request.RequestMethod === 3) {
                const targetStates = {
                    Inprogress: 'In Progress',
                    Partial: 'Partially Failed',
                    Assesscancellation: 'Assess Cancellation',
                    Pendingcancellation: 'Pending Cancellation',
                };
                const target = targetStates[body.targetStatus] || body.targetStatus;
                const allowed = {
                    New: ['Acknowledged', 'Rejected'],
                    Acknowledged: ['In Progress', 'Rejected'],
                    'In Progress': ['Completed', 'Failed', 'Partially Failed', 'Held', 'Pending'],
                    Pending: ['Assess Cancellation', 'In Progress'],
                    'Assess Cancellation': ['Pending Cancellation', 'Held', 'Pending'],
                    'Pending Cancellation': ['Cancelled'],
                    Held: ['In Progress', 'Assess Cancellation'],
                };
                if (!allowed[item.Status]?.includes(target)) return error(409, 'Conflict', `Service order item '${subId}' cannot transition from '${item.Status}' to '${target}'.`);
                item.Status = target;
                EXTRA_ITEMS.set(order['Order ID'], items);
                return { status: 200, response: toUdapiOrderItem(item) };
            }
            if (item && [4, 5].includes(request.RequestMethod) && item.Status !== 'New') return error(409, 'Conflict', `Service order item '${subId}' cannot be changed in status '${item.Status}'.`);
            if (item && request.RequestMethod === 5) {
                if ('name' in body && !body.name?.trim()) return error(400, 'Invalid request', "'name' cannot be empty.");
                const index = items.indexOf(item);
                items[index] = applyOrderItemRequest({ ...item }, body);
                EXTRA_ITEMS.set(order['Order ID'], items);
                return { status: 200, response: toUdapiOrderItem(items[index]) };
            }
            if (item && request.RequestMethod === 4) {
                EXTRA_ITEMS.set(order['Order ID'], items.filter(row => row !== item));
                return { status: 204 };
            }
        }
    }
    if (resource === 'service') {
        if (!id && request.RequestMethod === 1) return { status: 200, response: SERVICES.map(toUdapiService) };
        if (!id && request.RequestMethod === 3) {
            if (!body.name?.trim()) return error(400, 'Invalid request', "'name' is required.");
            if (body.serviceId && !/^SERVICE-\d+$/.test(body.serviceId)) return error(400, 'Invalid request', "'serviceId' must use the format SERVICE-<number>.");
            if (SERVICES.some(row => row['Service ID'].toLowerCase() === String(body.serviceId || '').toLowerCase())) return error(409, 'Conflict', `Service ID '${body.serviceId}' is already in use.`);
            const created = applyServiceRequest({ 'DOM ID': guid(9500 + SERVICES.length), 'Service ID': `SERVICE-${400 + SERVICES.length}`, 'Service Status': 'New', 'Alarm Level': '0' }, body);
            SERVICES.unshift(created);
            return { status: 201, response: toUdapiService(created) };
        }
        const service = id ? SERVICES.find(row => row['Service ID'] === id) : null;
        if (!service) return error(404, 'Not found', `Service '${id}' does not exist.`);
        if (!subResource && request.RequestMethod === 4) {
            SERVICES.splice(SERVICES.indexOf(service), 1);
            EXTRA_ITEMS.delete(service['DOM ID']);
            SERVICE_CONFIGURATION_OVERRIDES.delete(service['DOM ID']);
            SERVICE_SCRIPT_OVERRIDES.delete(service['DOM ID']);
            return { status: 204 };
        }
        if (!subResource && request.RequestMethod === 5) {
            if ('name' in body && !body.name?.trim()) return error(400, 'Invalid request', "'name' cannot be empty.");
            return { status: 200, response: toUdapiService(applyServiceRequest(service, body)) };
        }
        if (subResource === 'transition' && request.RequestMethod === 3) {
            const target = SERVICE_STATES.find(state => state.toLowerCase() === String(body.targetStatus || '').toLowerCase());
            if (!target) return error(400, 'Invalid request', `'targetStatus' value '${body.targetStatus}' is not a valid service status.`);
            service['Service Status'] = target;
            return { status: 200, response: toUdapiService(service) };
        }
        if (subResource === 'relatedOrder' && request.RequestMethod === 1) {
            const rows = relatedOrderRowsForService(service['DOM ID']).map(row => ({
                OrderInstanceId: row['Order ID'],
                OrderId: orderIdFor(row['Order ID']),
                OrderName: row['Order Name'],
                ServiceOrderItemId: row['Service Order Item ID'],
                Action: row.Action,
                State: row.State,
            }));
            return { status: 200, response: rows };
        }
        if (subResource === 'configuration') {
            if (request.RequestMethod === 1) return { status: 200, response: serviceConfigurationsFor(service['DOM ID']).map(toConfigurationRecord) };
            if (request.RequestMethod === 2) {
                SERVICE_CONFIGURATION_OVERRIDES.set(service['DOM ID'], configurationRowsFromPayload(service['DOM ID'], body));
                return { status: 200, response: serviceConfigurationsFor(service['DOM ID']).map(toConfigurationRecord) };
            }
        }
        if (subResource === 'script') {
            const scripts = serviceScriptsFor(service['DOM ID']);
            const originalDescription = String(body.originalDescription || '').trim();
            const description = String(body.description || '').trim();
            const name = String(body.name || '').trim();
            const toScript = script => ({ Name: script['Script Name'], Description: script.Description });
            if (request.RequestMethod === 1) return { status: 200, response: scripts.map(toScript) };
            if (request.RequestMethod === 3) {
                if (!name || !description) return error(400, 'Invalid request', "'name' and 'description' are required.");
                if (scripts.some(script => script.Description.toLowerCase() === description.toLowerCase())) return error(409, 'Conflict', 'A service script with this description already exists.');
                const created = { 'Script Name': name, Description: description };
                scripts.push(created);
                SERVICE_SCRIPT_OVERRIDES.set(service['DOM ID'], scripts);
                return { status: 201, response: toScript(created) };
            }
            const descriptionToFind = request.RequestMethod === 5 ? originalDescription : description;
            const index = scripts.findIndex(script => script.Description.toLowerCase() === descriptionToFind.toLowerCase());
            if (index < 0) return error(404, 'Not found', 'The service script to update or remove was not found.');
            if (request.RequestMethod === 5) {
                if (!originalDescription || !name || !description) return error(400, 'Invalid request', "'originalDescription', 'name' and 'description' are required.");
                if (scripts.some((script, scriptIndex) => scriptIndex !== index && script.Description.toLowerCase() === description.toLowerCase())) return error(409, 'Conflict', 'A service script with this description already exists.');
                scripts[index] = { 'Script Name': name, Description: description };
                SERVICE_SCRIPT_OVERRIDES.set(service['DOM ID'], scripts);
                return { status: 200, response: toScript(scripts[index]) };
            }
            if (request.RequestMethod === 4) {
                scripts.splice(index, 1);
                SERVICE_SCRIPT_OVERRIDES.set(service['DOM ID'], scripts);
                return { status: 204 };
            }
        }
        if (subResource === 'duplicate' && request.RequestMethod === 3) {
            const serviceNumbers = SERVICES.map(candidate => Number(String(candidate['Service ID'] || '').match(/^SERVICE-(\d+)$/)?.[1] || 0));
            const nextServiceNumber = Math.max(0, ...serviceNumbers) + 1;
            const duplicate = {
                ...service,
                'DOM ID': guid(8000 + SERVICES.length),
                'Service ID': `SERVICE-${nextServiceNumber}`,
                'Service Name': `${service['Service Name']} (Copy)`,
                'Service Status': 'New',
                'Alarm Level': '0',
                'Monitoring Service': '',
            };
            SERVICES.unshift(duplicate);
            EXTRA_ITEMS.set(duplicate['DOM ID'], serviceItemsFor(service['DOM ID']).map(item => ({
                ...item,
                'Implementation Reference': '',
            })));
            SERVICE_CONFIGURATION_OVERRIDES.set(duplicate['DOM ID'], serviceConfigurationsFor(service['DOM ID']).map(row => ({ ...row })));
            SERVICE_SCRIPT_OVERRIDES.set(duplicate['DOM ID'], serviceScriptsFor(service['DOM ID']).map(row => ({ ...row })));
            return { status: 201, response: toUdapiService(duplicate) };
        }
        if (subResource === 'item') return handleServiceItemRequest(request, service, body, subId, error);
    }
    return error(405, 'Method not allowed', `Unsupported request ${request.RequestMethod} ${request.Route}.`);
}

const SERVICE_ITEM_TYPE_VALUES = { workflow: 0, service: 1, srmbooking: 2 };
const SERVICE_ITEM_TYPE_NAMES = ['Workflow', 'Service', 'SRM Booking'];

function toUdapiServiceItem(row) {
    return {
        ServiceItemID: Number(row['Service Item ID']),
        Label: row.Label,
        Type: SERVICE_ITEM_TYPE_VALUES[String(row['Service Item Type'] || 'Service').replace(/[\s_]/g, '').toLowerCase()] ?? 1,
        DefinitionReference: row['Definition Reference'] || '',
        ImplementationReference: row['Implementation Reference'] || '',
        Script: row['Service Item Script'] || '',
        Icon: '',
        ImplementationName: row['Implementation Reference Name'] || '',
        ImplementationState: row['Implementation State'] || '',
        MonitoringServiceState: row['Monitoring Service State'] || '',
    };
}

function handleServiceItemRequest(request, service, body, subId, error) {
    const serviceDomId = service['DOM ID'];
    const items = [...serviceItemsFor(serviceDomId)];
    const isLabelInUse = (label, except) => items.some(row => row !== except && row.Label.toLowerCase() === label.toLowerCase());
    const applyItemRequest = (row, request) => {
        if ('type' in request) {
            const type = SERVICE_ITEM_TYPE_VALUES[String(request.type || 'Service').replace(/[\s_]/g, '').toLowerCase()];
            if (type === undefined) return `'type' value '${request.type}' is not a valid service item type.`;
            row['Service Item Type'] = SERVICE_ITEM_TYPE_NAMES[type];
        }
        if ('label' in request) row.Label = String(request.label || '').trim();
        if ('definitionReference' in request) row['Definition Reference'] = request.definitionReference || '';
        if ('implementationReference' in request) row['Implementation Reference'] = request.implementationReference || '';
        if ('script' in request) row['Service Item Script'] = request.script || '';
        return null;
    };
    if (!subId && request.RequestMethod === 1) return { status: 200, response: items.map(toUdapiServiceItem) };
    if (!subId && request.RequestMethod === 3) {
        if (!String(body.label || '').trim()) return error(400, 'Invalid request', "'label' is required.");
        if (isLabelInUse(body.label.trim())) return error(409, 'Conflict', `Service item label '${body.label.trim()}' is already in use on service '${service['Service ID']}'.`);
        const created = { 'Service Item ID': String(items.reduce((max, row) => Math.max(max, Number(row['Service Item ID']) + 1), 0)), 'Service Item Type': 'Service', 'Implementation State': '', 'Monitoring Service State': '' };
        const invalid = applyItemRequest(created, body);
        if (invalid) return error(400, 'Invalid request', invalid);
        EXTRA_ITEMS.set(serviceDomId, [...items, created]);
        return { status: 201, response: toUdapiServiceItem(created) };
    }
    const item = subId ? items.find(row => row['Service Item ID'] === subId) : null;
    if (!item) return error(404, 'Not found', `Service item '${subId}' does not exist on service '${service['Service ID']}'.`);
    if (request.RequestMethod === 5) {
        if ('label' in body && !String(body.label || '').trim()) return error(400, 'Invalid request', "'label' cannot be empty.");
        if ('label' in body && isLabelInUse(body.label.trim(), item)) return error(409, 'Conflict', `Service item label '${body.label.trim()}' is already in use on service '${service['Service ID']}'.`);
        const updated = { ...item };
        const invalid = applyItemRequest(updated, body);
        if (invalid) return error(400, 'Invalid request', invalid);
        items[items.indexOf(item)] = updated;
        EXTRA_ITEMS.set(serviceDomId, items);
        return { status: 200, response: toUdapiServiceItem(updated) };
    }
    if (request.RequestMethod === 4) {
        EXTRA_ITEMS.set(serviceDomId, items.filter(row => row !== item));
        return { status: 204 };
    }
    return error(405, 'Method not allowed', `Unsupported request ${request.RequestMethod} ${request.Route}.`);
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
                    if (script.Name === 'SLC_SM_UDAPI_ServiceManagement') {
                        const raw = parameters.find(parameter => parameter.Name === 'Request')?.Value || '';
                        let request;
                        try { request = JSON.parse(raw); } catch { return send(res, 200, { d: { ErrorCode: -1, ErrorMessage: "The 'Request' script parameter is not a valid ApiRequest JSON.", Output: [] } }); }
                        const { status, response } = handleUdapiRequest(request);
                        return send(res, 200, { d: { ErrorCode: 0, Output: [{ Name: 'StatusCode', Value: String(status) }, { Name: 'Response', Value: response === undefined ? '' : JSON.stringify(response) }] } });
                    }
                    const values = Object.fromEntries(parameters.map(parameter => [parameter.Name, scriptParameter(parameters, parameter.Name)]));
                    if (script.Name === 'SLC_SM_IAS_Add Service Order' && values.Action === 'Add') {
                        const index = ORDERS.length + 100;
                        const year = 2026;
                        const number = String(index).padStart(6, '0');
                        ORDERS.unshift({ 'Order ID': guid(5000 + index), Name: `New service order ${number}`, Description: '', Priority: 'Medium', 'External ID': `SO-${year}-${number}`, 'Related Organization': '', State: 'New' });
                    } else if (script.Name === 'SLC_SM_AS_ManageServiceCategory' || script.Name === 'SLC_SM_AS_DeleteServiceCategory') {
                        const payload = values.Payload ? JSON.parse(values.Payload) : {};
                        if (values.Action === 'Add') {
                            CATEGORY_ROWS.push({ 'Category ID': guid(4700 + CATEGORY_ROWS.length), 'Category Name': payload.Name || 'New category', 'Category Type': payload.Type || 'Channel', 'Category Icon': payload.Icon || '' });
                        } else if (values.Action === 'Edit') {
                            const category = CATEGORY_ROWS.find(candidate => candidate['Category ID'] === values.domId);
                            if (category) Object.assign(category, { 'Category Name': payload.Name || category['Category Name'], 'Category Type': payload.Type || category['Category Type'], 'Category Icon': payload.Icon ?? category['Category Icon'] });
                        } else if (values.Action === 'Delete') {
                            CATEGORY_ROWS = CATEGORY_ROWS.filter(candidate => candidate['Category ID'] !== values.domId);
                        }
                    } else if (script.Name === 'SLC_SM_Create Service Inventory Item' && (values.Action === 'Add' || values.Action === 'Edit')) {
                        const payload = values.Payload ? JSON.parse(values.Payload) : {};
                        const category = CATEGORY_ROWS.find(candidate => candidate['Category Name'] === payload.Category);
                        if (values.Action === 'Add') {
                            const index = SERVICES.length + 300;
                            SERVICES.unshift({ 'DOM ID': guid(9000 + index), 'Service ID': payload.ServiceID || `SERVICE-${300 + index}`, 'Service Name': payload.Name || 'New service', 'Service Status': 'New', 'Service Start': payload.StartTime || 'No Start Time', 'Service End': payload.IndefiniteRuntime ? 'No End Time' : payload.EndTime || 'No End Time', 'Service Category': category?.['Category Name'] || '', 'Service Category Type': category?.['Category Type'] || '', 'Service Logo': category?.['Category Icon'] || '', 'Service Specification': payload.Specification || '', 'Alarm Level': '0' });
                        } else {
                            const service = SERVICES.find(candidate => candidate['DOM ID'] === values['DOM ID']);
                            if (service) Object.assign(service, { 'Service ID': payload.ServiceID || service['Service ID'], 'Service Name': payload.Name || service['Service Name'], 'Service Start': payload.StartTime || service['Service Start'], 'Service End': payload.IndefiniteRuntime ? 'No End Time' : payload.EndTime || service['Service End'], 'Service Category': category?.['Category Name'] || '', 'Service Category Type': category?.['Category Type'] || '', 'Service Logo': category?.['Category Icon'] || '', 'Service Specification': payload.Specification || '' });
                        }
                    } else if (script.Name === 'SLC_SM_Create Service Inventory Item' && values.Action === 'Duplicate') {
                        const source = SERVICES.find(service => service['DOM ID'] === values['DOM ID']);
                        if (!source) return send(res, 500, { Message: `Service ${values['DOM ID']} was not found.` });
                        const index = SERVICES.length + 200;
                        const duplicate = { ...source, 'DOM ID': guid(8000 + index), 'Service ID': `SERVICE-${200 + index}`, 'Service Name': `${source['Service Name']} (Copy)`, 'Service Status': 'New', 'Alarm Level': '0' };
                        SERVICES.unshift(duplicate);
                    } else if (script.Name === 'SLC_SM_Delete Service Order') {
                        ORDERS = ORDERS.filter(order => order['Order ID'] !== values['DOM ID']);
                        EXTRA_ITEMS.delete(values['DOM ID']);
                    } else if (script.Name === 'SLC_SM_IAS_Add Service Order Item' && values.Action === 'Add') {
                        const existing = [...itemsFor(values['DOM ID'])];
                        const itemNumber = existing.length + 1;
                        const payload = values.Payload ? JSON.parse(values.Payload) : {};
                        existing.push({ ID: guid(9000 + itemNumber), Name: payload.Name || `New order item ${itemNumber}`, Start: payload.StartTime || 'No Start Time', End: payload.EndTime || 'No End Time', Action: payload.ItemAction || 'Add', Category: '', 'Category ID': payload.CategoryId || '', 'Service Specification': '', 'Service Specification ID': payload.SpecificationId || '', Service: '', ServiceId: payload.ServiceId || '', Property: '', Configuration: '', Status: 'New', StatusId: '', Description: payload.Description || '' });
                        EXTRA_ITEMS.set(values['DOM ID'], existing);
                    } else if (script.Name === 'SLC_SM_IAS_Add Service Order Item' && values.Action === 'Edit') {
                        const existing = [...itemsFor(values['DOM ID'])];
                        const payload = values.Payload ? JSON.parse(values.Payload) : {};
                        const item = existing.find(candidate => candidate.ID === values['Service Order Item ID']);
                        if (item) {
                            Object.assign(item, { Name: payload.Name || item.Name, Description: payload.Description || '', Action: payload.ItemAction || item.Action, Start: payload.StartTime || item.Start, End: payload.EndTime || item.End, 'Category ID': payload.CategoryId || '', 'Service Specification ID': payload.SpecificationId || '', ServiceId: payload.ServiceId || '' });
                        }
                        EXTRA_ITEMS.set(values['DOM ID'], existing);
                    } else if (script.Name === 'SLC_SM_Delete Service Order Item') {
                        const existing = itemsFor(values['DOM ID']).filter(item => item.ID !== values['Service Order Item ID']);
                        EXTRA_ITEMS.set(values['DOM ID'], existing);
                    } else if (script.Name === 'SLC_SM_IAS_Add Service Item') {
                        const payload = values.Payload ? JSON.parse(values.Payload) : {};
                        const existing = [...(EXTRA_ITEMS.has(values['DOM ID']) ? EXTRA_ITEMS.get(values['DOM ID']) : serviceItemsFor(values['DOM ID']))];
                        const oldLabel = payload.ExistingLabel || values['Service Item Label'];
                        const index = existing.findIndex(item => item.Label === oldLabel);
                        const next = {
                            Actions: '',
                            Label: payload.Label || oldLabel || 'New service item',
                            'Service Item ID': index >= 0 ? existing[index]['Service Item ID'] : String(existing.length + 1),
                            'Service Item Type': payload.Type || 'Service',
                            'Definition Reference': payload.DefinitionReference || '',
                            'Service Item Script': payload.Script || '',
                            'Implementation Reference': payload.ImplementationReference || '',
                            'Implementation Reference Name': '',
                            'Implementation Reference Link': '',
                            'Implementation Reference Has Value': 'false',
                            'Implementation Reference Name Has Value': 'false',
                            'Implementation Reference Link Has Value': 'false',
                            'Implementation State': 'New',
                            'Implementation Reference Custom Link': '',
                            'Implementation Reference Custom Link Has Value': 'false',
                            'Monitoring Service State': 'Not monitored',
                            'Monitoring Service DMA ID/SID': '',
                            Log: '',
                        };
                        if (index >= 0) existing[index] = next; else existing.push(next);
                        EXTRA_ITEMS.set(values['DOM ID'], existing);
                    } else if (script.Name === 'SLC_SM_Delete Service Item') {
                        const existing = EXTRA_ITEMS.has(values['DOM ID']) ? EXTRA_ITEMS.get(values['DOM ID']) : serviceItemsFor(values['DOM ID']);
                        EXTRA_ITEMS.set(values['DOM ID'], existing.filter(item => item.Label !== values['Service Item Label']));
                    } else if (script.Name === 'ServiceOrder_StateTranstitions') {
                        const nextState = { Inprogress: 'In Progress', Partial: 'Partially Failed', Assesscancellation: 'Assess Cancellation', Pendingcancellation: 'Pending Cancellation' }[values.NextState] || values.NextState;
                        const order = ORDERS.find(candidate => candidate['Order ID'] === values.ServiceOrderReference);
                        if (order) order.State = nextState;
                    } else if (script.Name === 'ServiceOrderItem_StateTranstitions') {
                        const nextState = { Inprogress: 'In Progress', Partial: 'Partially Failed', Assesscancellation: 'Assess Cancellation', Pendingcancellation: 'Pending Cancellation' }[values.NextState] || values.NextState;
                        for (const order of ORDERS) {
                            const items = itemsFor(order['Order ID']);
                            const item = items.find(candidate => candidate.ID === values.Id);
                            if (item) {
                                item.Status = nextState;
                                EXTRA_ITEMS.set(order['Order ID'], items);
                            }
                        }
                    } else if (script.Name === 'Service_StateTransitions') {
                        const nextState = values.NextState;
                        const service = SERVICES.find(candidate => candidate['DOM ID'] === values.ServiceReference);
                        if (service) service['Service Status'] = nextState;
                    } else if (script.Name === 'SLC_SM_Create Service Inventory Item' && values.Action === 'AddItemSilent') {
                        const result = initializeServiceFromOrderItem(values['DOM ID']);
                        if (result.error) return send(res, 200, { d: { ErrorCode: 1, ErrorMessage: result.error, Output: [] } });
                    } else if (script.Name === 'SLC_SM_IAS_Manage Service Scripts') {
                        const existing = SERVICE_SCRIPT_OVERRIDES.has(values['Service Id'])
                            ? SERVICE_SCRIPT_OVERRIDES.get(values['Service Id'])
                            : serviceScriptsFor(values['Service Id']);
                        const action = String(values.Action || '').toLowerCase();
                        if (action === 'add') {
                            existing.push({ 'Script Name': values['Script Name'] || '', Description: values['Script Description'] || '' });
                        } else if (action === 'update') {
                            const index = existing.findIndex(item => item.Description === (values['Original Script Description'] || values['Script Description']));
                            if (index >= 0) existing[index] = { 'Script Name': values['Script Name'] || '', Description: values['Script Description'] || '' };
                        } else if (action === 'remove') {
                            const index = existing.findIndex(item => item.Description === (values['Original Script Description'] || values['Script Description']));
                            if (index >= 0) existing.splice(index, 1);
                        }
                        SERVICE_SCRIPT_OVERRIDES.set(values['Service Id'], existing);
                    } else if (script.Name === 'SLC_SM_IAS_Service_Order_Configuration') {
                        const payload = values.Payload ? JSON.parse(values.Payload) : {};
                        SERVICE_CONFIGURATION_OVERRIDES.set(values['DOM ID'], configurationRowsFromPayload(values['DOM ID'], payload, { includeProfiles: false }));
                    } else if (script.Name === 'SLC_SM_IAS_Service_Configuration' || script.Name === 'SLC_SM_IAS_Service_Spec_Configuration') {
                        const payload = values.Payload ? JSON.parse(values.Payload) : {};
                        if (payload.DeleteVersion) {
                            SERVICE_CONFIGURATION_OVERRIDES.delete(values['DOM ID']);
                        } else {
                            SERVICE_CONFIGURATION_OVERRIDES.set(values['DOM ID'], configurationRowsFromPayload(values['DOM ID'], payload));
                        }
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
                    const isMonitoringServices = descriptor.ScriptName === 'SLC_SM_GQIDS_Get Monitoring Services' && descriptor.TypeFullName === 'SLC_SM_GQIDS_Get_Monitoring_Services.GetMonitoringServices';
                    const isDocumentHubLogos = descriptor.ScriptName === 'SLC_SM_DS_GetDocumentHubLogos' && descriptor.TypeFullName === 'SLCSMDSGetDocumentHubLogos.SLCSMDSGetDocumentHubLogos';
                    const isServiceItems = descriptor.ScriptName === 'SLC_SM_GQIDS_Get Service Items';
                    const isServiceScripts = descriptor.ScriptName === 'SLC_SM_GQIDS_Get Service Scripts';
                    const isSpecifications = descriptor.ScriptName === 'SLC_SM_GQIDS_Get Service Specifications';
                    const isCategories = descriptor.ScriptName === 'SLC_SM_GQIDS_Get Service Categories';
                    const isConfigurations = descriptor.ScriptName === 'SLC_SM_GQIDS_Get Service Configurations';
                    const isOrganizations = descriptor.ScriptName === 'SLC_SM_GQIDS_Get Service Organizations' && descriptor.TypeFullName === 'SLC_SM_GQIDS_Get_Service_Organizations.GetOrganizations';
                    const isRelatedServiceOrders = descriptor.ScriptName === 'SLC_SM_GQIDS_Get Related Service Orders' && descriptor.TypeFullName === 'SLC_SM_GQIDS_Get_Related_Service_Orders.GetRelatedServiceOrders';
                    const requestedConfigurationType = String(args['Configuration Type'] || '').trim().toLowerCase();
                    const isOrderItemConfiguration = isConfigurations && (requestedConfigurationType ? requestedConfigurationType === 'orderitem' : isServiceOrderItemId(args['DOM ID']));
                    if (!isItems && !isServices && !isServiceItems && !isServiceScripts && !isSpecifications && !isCategories && !isConfigurations && !isOrganizations && !isRelatedServiceOrders && !isMonitoringServices && !isDocumentHubLogos && descriptor.ScriptName !== 'SLC_SM_GQIDS_Get Service Orders') return send(res, 500, { Message: `Unknown data source ${descriptor.ScriptName}` });
                    const id = `dev-session-${++sessionCount}`;
                    const columns = isItems ? ITEM_COLUMNS : isServices && !isMonitoringServices ? SERVICE_COLUMNS : isMonitoringServices ? MONITORING_SERVICE_COLUMNS : isDocumentHubLogos ? DOCUMENT_HUB_COLUMNS : isServiceItems ? SERVICE_ITEM_COLUMNS : isServiceScripts ? ['Script Name', 'Description'] : isSpecifications ? SPECIFICATION_COLUMNS : isCategories ? ['Category ID', 'Category Name', 'Category Type', 'Category Icon'] : isConfigurations ? SERVICE_CONFIG_COLUMNS : isOrganizations ? ORGANIZATION_COLUMNS : isRelatedServiceOrders ? ['Order ID', 'Order Name', 'Service Order Item ID', 'Action', 'State'] : ORDER_COLUMNS;
                    const rows = isServiceScripts ? serviceScriptsFor(args['Service Name']) : isItems ? itemsFor(args['DOM ID']) : isMonitoringServices ? SERVICES.map(service => ({ 'Monitoring Service ID': service['DOM ID'], 'Monitoring Service Name': service['Service Name'] })) : isDocumentHubLogos ? DOCUMENT_HUB_LOGOS : isServices ? SERVICES.map(service => ({ ...service, 'Related Service Orders': relatedOrdersForService(service['DOM ID']).join(', '), 'Related Service Order Count': relatedOrdersForService(service['DOM ID']).length })) : isServiceItems ? serviceItemsFor(args['DOM ID']) : isSpecifications ? SPECIFICATIONS : isCategories ? CATEGORY_ROWS : isConfigurations ? serviceConfigurationsFor(args['DOM ID'], { includeProfiles: !isOrderItemConfiguration }) : isOrganizations ? ORGANIZATION_ROWS : isRelatedServiceOrders ? relatedOrderRowsForService(args['Service ID']) : ORDERS;
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
