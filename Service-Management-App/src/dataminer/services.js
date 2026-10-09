import { adHocDataSourceQuery, runQuery } from './gqi.js';
import { formatDataMinerDateTime } from './dateTime.js';

const SERVICES_SOURCE = {
    scriptName: 'SLC_SM_GQIDS_Get Services',
    libraryName: 'GetServices',
    typeFullName: 'SLC_SM_GQIDS_Get_Services.GetServices',
    typeName: 'GetServices',
};
const MONITORING_SERVICES_SOURCE = {
    scriptName: 'SLC_SM_GQIDS_Get Monitoring Services',
    libraryName: 'GetMonitoringServices',
    typeFullName: 'SLC_SM_GQIDS_Get_Monitoring_Services.GetMonitoringServices',
    typeName: 'GetMonitoringServices',
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
const SERVICE_CONFIGURATIONS_SOURCE = {
    scriptName: 'SLC_SM_GQIDS_Get Service Configurations',
    libraryName: 'GetServiceConfigurations',
    typeFullName: 'SLC_SM_GQIDS_Get_Service_Configurations.GetServiceConfigurations',
    typeName: 'GetServiceConfigurations',
};

export const SERVICE_STATUSES = ['New', 'Designed', 'Reserved', 'Active', 'Terminated', 'Retired'];
export const SERVICE_TRANSITIONS = {
    new: [{ label: 'Design', next: 'Designed' }, { label: 'Retire', next: 'Retired' }],
    designed: [{ label: 'Reserve', next: 'Reserved' }, { label: 'Retire', next: 'Retired' }],
    reserved: [{ label: 'Activate', next: 'Active' }, { label: 'Retire', next: 'Retired' }],
    terminated: [{ label: 'Activate', next: 'Active' }, { label: 'Retire', next: 'Retired' }],
    active: [{ label: 'Terminate', next: 'Terminated' }],
};
export const SERVICE_INVENTORY_SCRIPTS = {
    create: {
        name: 'SLC_SM_Create Service Inventory Item',
        interactive: false,
        parameterDefinitions: [{ id: 1, name: 'DOM ID' }, { id: 2, name: 'Action' }, { id: 20, name: 'Payload' }],
    },
    edit: {
        name: 'SLC_SM_Create Service Inventory Item',
        interactive: false,
        parameterDefinitions: [{ id: 1, name: 'DOM ID' }, { id: 2, name: 'Action' }, { id: 20, name: 'Payload' }],
    },
    duplicate: {
        name: 'SLC_SM_Create Service Inventory Item',
        interactive: false,
        parameterDefinitions: [{ id: 1, name: 'DOM ID' }, { id: 2, name: 'Action' }, { id: 20, name: 'Payload' }],
    },
    initializeFromOrderItem: {
        name: 'SLC_SM_Create Service Inventory Item',
        interactive: false,
        parameterDefinitions: [{ id: 1, name: 'DOM ID' }, { id: 2, name: 'Action' }, { id: 20, name: 'Payload' }],
    },
    addItem: {
        name: 'SLC_SM_IAS_Add Service Item',
        interactive: false,
        parameterDefinitions: [{ id: 10, name: 'DOM ID' }, { id: 11, name: 'Service Item Label' }, { id: 12, name: 'Action' }, { id: 20, name: 'Payload' }],
    },
    editItem: {
        name: 'SLC_SM_IAS_Add Service Item',
        interactive: false,
        parameterDefinitions: [{ id: 10, name: 'DOM ID' }, { id: 11, name: 'Service Item Label' }, { id: 12, name: 'Action' }, { id: 20, name: 'Payload' }],
    },
    delete: {
        name: 'SLC_SM_Delete_Service',
        interactive: false,
        parameterDefinitions: [{ id: 10, name: 'DOM ID' }, { id: 99, name: 'Skip Confirmation' }],
    },
    deleteItem: {
        name: 'SLC_SM_Delete Service Item',
        interactive: false,
        parameterDefinitions: [{ id: 1, name: 'Service Item Label' }, { id: 10, name: 'DOM ID' }, { id: 99, name: 'Skip Confirmation' }],
    },
    transition: {
        name: 'Service_StateTransitions',
        interactive: false,
        parameterDefinitions: [{ id: 2, name: 'ServiceReference' }, { id: 3, name: 'PreviousState' }, { id: 4, name: 'NextState' }],
    },
};
export const SERVICE_CONFIGURATION_SCRIPTS = {
    save: {
        name: 'SLC_SM_IAS_Service_Configuration',
        interactive: false,
        parameterDefinitions: [{ id: 10, name: 'DOM ID' }, { id: 20, name: 'Payload' }],
    },
};
export const SERVICE_SCRIPT_SCRIPTS = {
    manage: {
        name: 'SLC_SM_IAS_Manage Service Scripts',
        interactive: false,
        parameterDefinitions: [
            { id: 2, name: 'Action' },
            { id: 3, name: 'Service Id' },
            { id: 4, name: 'Script Name' },
            { id: 5, name: 'Script Description' },
            { id: 6, name: 'Original Script Description' },
            { id: 20, name: 'Payload' },
        ],
    },
};
const ACTIVE_STATUSES = new Set(['active', 'reserved', 'designed']);

export function isActiveService(service) {
    return ACTIVE_STATUSES.has(service.status.trim().toLowerCase());
}

function time(value) {
    return value && !/^No (Start|End) Time$/i.test(value) ? formatDataMinerDateTime(value) : '';
}

export async function fetchServices(domId = '') {
    const args = domId ? { 'DOM ID': domId } : {};
    const rows = await runQuery(adHocDataSourceQuery(SERVICES_SOURCE, args));
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
        organization: row['Service Organization'] ?? '',
        linkedService: row['Monitoring Service'] ?? '',
        relatedOrders: String(row['Related Service Orders'] ?? '').split(',').map(value => value.trim()).filter(Boolean),
        relatedOrderCount: Number(row['Related Service Order Count'] ?? 0),
    }));
}

export async function fetchMonitoringServices() {
    const rows = await runQuery(adHocDataSourceQuery(MONITORING_SERVICES_SOURCE));
    return rows.map(row => ({ id: row['Monitoring Service ID'] ?? '', name: row['Monitoring Service Name'] ?? '' })).filter(service => service.name.trim());
}

export async function fetchServiceItems(serviceId) {
    const rows = await runQuery(adHocDataSourceQuery(SERVICE_ITEMS_SOURCE, { 'DOM ID': serviceId }));
    return rows.map(row => ({
        id: row['Service Item ID'] ?? '',
        label: row.Label ?? '',
        type: row['Service Item Type'] ?? '',
        definition: row['Definition Reference'] ?? '',
        implementationReference: row['Implementation Reference'] ?? '',
        script: row['Service Item Script'] ?? '',
        implementation: row['Implementation Reference Name'] ?? '',
        implementationState: row['Implementation State'] ?? '',
        monitoringState: row['Monitoring Service State'] ?? '',
    }));
}

export async function fetchServiceScripts(serviceId) {
    const rows = await runQuery(adHocDataSourceQuery(SERVICE_SCRIPTS_SOURCE, { 'Service Name': serviceId }));
    return rows
        .map(row => ({ name: row['Script Name'] ?? '', description: row.Description ?? '' }))
        .filter(script => script.name.trim() || script.description.trim());
}

export async function fetchServiceConfigurations(serviceId, configurationType = 'Service') {
    const rows = await runQuery(adHocDataSourceQuery(SERVICE_CONFIGURATIONS_SOURCE, { 'DOM ID': serviceId, 'Configuration Type': configurationType }));
    const versions = new Map();
    const availableParameters = [];
    const availableProfiles = [];
    const availableNestedProfiles = [];
    const addUnique = (items, item) => {
        if (item.id && !items.some(existing => existing.id === item.id)) items.push(item);
    };
    const ensureProfile = (version, id, fields) => {
        if (!id) return null;
        let profile = version.profiles.get(id);
        if (!profile) {
            profile = {
                id,
                name: fields.name ?? '',
                definitionId: fields.definitionId ?? '',
                definitionName: fields.definitionName ?? '',
                reusable: fields.reusable ?? false,
                mandatory: fields.mandatory ?? false,
                parameters: [],
                nestedProfiles: [],
            };
            version.profiles.set(id, profile);
        }
        else {
            profile.name ||= fields.name ?? '';
            profile.definitionId ||= fields.definitionId ?? '';
            profile.definitionName ||= fields.definitionName ?? '';
            profile.reusable ||= fields.reusable ?? false;
            profile.mandatory ||= fields.mandatory ?? false;
        }
        return profile;
    };
    const addProfileParameter = (profile, row) => {
        const parameterId = row['Parameter ID'] ?? '';
        if (parameterId && !profile.parameters.some(parameter => parameter.id === parameterId)) {
            profile.parameters.push({
                id: parameterId,
                valueId: row['Parameter Value ID'] ?? '',
                name: row['Parameter Name'] ?? '',
                type: row['Parameter Type'] ?? '',
                value: row['Parameter Value'] ?? '',
                mandatory: String(row['Profile Mandatory']).toLowerCase() === 'true',
            });
        }
    };
    rows.forEach(row => {
        const recordType = row['Record Type'] ?? '';
        if (recordType === 'AvailableParameter') {
            addUnique(availableParameters, { id: row['Parameter ID'] ?? '', name: row['Parameter Name'] ?? '', type: row['Parameter Type'] ?? '' });
            return;
        }
        if (recordType === 'AvailableProfile') {
            addUnique(availableProfiles, { id: row['Profile Definition ID'] ?? '', name: row['Profile Definition Name'] ?? '' });
            return;
        }
        if (recordType === 'AvailableNestedProfile') {
            addUnique(availableNestedProfiles, {
                id: row['Nested Profile ID'] ?? '',
                name: row['Nested Profile Name'] ?? '',
                definitionId: row['Nested Profile Definition ID'] ?? '',
                definitionName: row['Nested Profile Definition Name'] ?? '',
                reusable: String(row['Profile Reusable']).toLowerCase() === 'true',
            });
            return;
        }
        const id = row['Version ID'] ?? '';
        if (!id) return;
        if (!versions.has(id)) {
            versions.set(id, {
                id,
                name: row['Version Name'] ?? '',
                description: row['Version Description'] ?? '',
                start: formatDataMinerDateTime(row['Version Start']),
                end: formatDataMinerDateTime(row['Version End']),
                active: String(row['Is Active']).toLowerCase() === 'true',
                parameters: [],
                profiles: new Map(),
                nestedProfileIds: new Set(),
            });
        }
        const version = versions.get(id);
        const profileId = row['Profile ID'] ?? '';
        const nestedProfileId = row['Nested Profile ID'] ?? '';
        if (nestedProfileId) {
            const parent = ensureProfile(version, profileId, {
                name: row['Profile Name'] ?? '',
                definitionId: row['Profile Definition ID'] ?? '',
                definitionName: row['Profile Definition Name'] ?? '',
                reusable: String(row['Profile Reusable']).toLowerCase() === 'true',
                mandatory: String(row['Profile Mandatory']).toLowerCase() === 'true',
            });
            const nested = ensureProfile(version, nestedProfileId, {
                name: row['Nested Profile Name'] ?? '',
                definitionId: row['Nested Profile Definition ID'] ?? '',
                definitionName: row['Nested Profile Definition Name'] ?? '',
                reusable: availableNestedProfiles.some(item => item.id === nestedProfileId && item.reusable),
                mandatory: String(row['Profile Mandatory']).toLowerCase() === 'true',
            });
            if (parent && nested) {
                if (!parent.nestedProfiles.some(item => item.id === nested.id)) parent.nestedProfiles.push(nested);
                version.nestedProfileIds.add(nested.id);
                addProfileParameter(nested, row);
            }
            return;
        }
        if (profileId) {
            const profile = ensureProfile(version, profileId, {
                name: row['Profile Name'] ?? '',
                definitionId: row['Profile Definition ID'] ?? '',
                definitionName: row['Profile Definition Name'] ?? '',
                reusable: String(row['Profile Reusable']).toLowerCase() === 'true',
                mandatory: String(row['Profile Mandatory']).toLowerCase() === 'true',
            });
            addProfileParameter(profile, row);
            return;
        }
        const parameterId = row['Parameter ID'] ?? '';
        if (parameterId && !version.parameters.some(parameter => parameter.id === parameterId)) {
            version.parameters.push({
                id: parameterId,
                configurationId: row['Configuration ID'] ?? '',
                valueId: row['Parameter Value ID'] ?? '',
                name: row['Parameter Name'] ?? '',
                type: row['Parameter Type'] ?? '',
                value: row['Parameter Value'] ?? '',
                mandatory: String(row.Mandatory).toLowerCase() === 'true',
            });
        }
    });
    const reusableProfileIds = new Set(availableNestedProfiles.filter(profile => profile.reusable).map(profile => profile.id));
    const visitedProfiles = new Set();
    const markReusableProfiles = profile => {
        if (!profile || visitedProfiles.has(profile.id)) return;
        visitedProfiles.add(profile.id);
        if (reusableProfileIds.has(profile.id)) profile.reusable = true;
        (profile.nestedProfiles || []).forEach(markReusableProfiles);
    };
    versions.forEach(version => version.profiles.forEach(markReusableProfiles));
    return {
        versions: [...versions.values()].map(({ profiles, nestedProfileIds, ...version }) => ({ ...version, profiles: [...profiles.values()].filter(profile => !nestedProfileIds.has(profile.id)) })),
        availableParameters,
        availableProfiles,
        availableNestedProfiles,
    };
}
