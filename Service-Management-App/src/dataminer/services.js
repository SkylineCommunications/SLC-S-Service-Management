import { adHocDataSourceQuery, runQuery } from './gqi.js';
import { formatDataMinerDateTime } from './dateTime.js';

const SERVICES_SOURCE = {
    scriptName: 'SLC_SM_GQIDS_Get Services',
    libraryName: 'GetServices',
    typeFullName: 'SLC_SM_GQIDS_Get_Services.GetServices',
    typeName: 'GetServices',
};
const SERVICE_ITEMS_SOURCE = {
    scriptName: 'SLC_SM_GQIDS_Get Service Items',
    libraryName: 'GetServiceItems',
    typeFullName: 'SLC_SM_GQIDS_Get_Service_Items.EventManagerGetMultipleSections',
    typeName: 'EventManagerGetMultipleSections',
};
const SERVICE_SCRIPTS_SOURCE = {
    scriptName: 'SLC_SM_GQIDS_Get Service Scripts',
    libraryName: 'Get Service Scripts',
    typeFullName: 'SLCSMGQIDSGetServiceScripts.GetServiceScripts',
    typeName: 'GetServiceScripts',
};

export const SERVICE_STATUSES = ['New', 'Designed', 'Reserved', 'Active', 'Terminated', 'Retired'];
export const SERVICE_INVENTORY_SCRIPTS = {
    edit: {
        name: 'SLC_SM_Create Service Inventory Item',
        interactive: true,
        parameterDefinitions: [{ id: 1, name: 'DOM ID' }, { id: 2, name: 'Action' }],
    },
    duplicate: {
        name: 'SLC_SM_Create Service Inventory Item',
        interactive: true,
        parameterDefinitions: [{ id: 1, name: 'DOM ID' }, { id: 2, name: 'Action' }],
    },
    delete: {
        name: 'SLC_SM_Delete_Service',
        interactive: true,
        parameterDefinitions: [{ id: 10, name: 'DOM ID' }],
    },
};
const ACTIVE_STATUSES = new Set(['active', 'reserved', 'designed']);

export function isActiveService(service) {
    return ACTIVE_STATUSES.has(service.status.trim().toLowerCase());
}

function time(value) {
    return value && !/^No (Start|End) Time$/i.test(value) ? formatDataMinerDateTime(value) : '';
}

export async function fetchServices() {
    const rows = await runQuery(adHocDataSourceQuery(SERVICES_SOURCE));
    return rows.map(row => ({
        id: row['DOM ID'] ?? '',
        serviceId: row['Service ID'] ?? '',
        name: row['Service Name'] ?? '',
        status: row['Service Status'] ?? '',
        start: time(row['Service Start']),
        end: time(row['Service End']),
        category: row['Service Category'] ?? '',
        categoryType: row['Service Category Type'] ?? '',
        logo: row['Service Logo'] ?? '',
        specification: row['Service Specification'] ?? '',
        alarmLevel: row['Alarm Level'] ?? '',
    }));
}

export async function fetchServiceItems(serviceId) {
    const rows = await runQuery(adHocDataSourceQuery(SERVICE_ITEMS_SOURCE, { 'DOM ID': serviceId }));
    return rows.map(row => ({
        id: row['Service Item ID'] ?? '',
        label: row.Label ?? '',
        type: row['Service Item Type'] ?? '',
        definition: row['Definition Reference'] ?? '',
        implementation: row['Implementation Reference Name'] ?? '',
        implementationState: row['Implementation State'] ?? '',
        monitoringState: row['Monitoring Service State'] ?? '',
    }));
}

export async function fetchServiceScripts(serviceId) {
    const rows = await runQuery(adHocDataSourceQuery(SERVICE_SCRIPTS_SOURCE, { 'Service Name': serviceId }));
    return rows.map(row => ({ name: row['Script Name'] ?? '', description: row.Description ?? '' }));
}
