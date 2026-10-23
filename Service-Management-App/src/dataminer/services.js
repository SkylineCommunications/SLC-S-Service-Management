import { formatDataMinerDateTime, getDataMinerTimestamp } from './dateTime.js';
import {
    addInventoryServiceItem,
    addServiceScript,
    createInventoryService,
    deleteInventoryService,
    deleteInventoryServiceItem,
    deleteServiceScript,
    duplicateInventoryService,
    fetchMonitoringServiceList,
    fetchServiceRecord,
    fetchServiceConfigurationList,
    fetchServiceItemList,
    fetchServiceList,
    fetchServiceCursorPage,
    fetchServiceOverview,
    fetchServiceScriptList,
    saveServiceConfiguration,
    transitionInventoryService,
    updateInventoryService,
    updateInventoryServiceItem,
    updateServiceScript,
} from './udapi.js';

export {
    addInventoryServiceItem,
    addServiceScript,
    createInventoryService,
    deleteInventoryService,
    deleteInventoryServiceItem,
    deleteServiceScript,
    duplicateInventoryService,
    saveServiceConfiguration,
    transitionInventoryService,
    updateInventoryService,
    updateInventoryServiceItem,
    updateServiceScript,
};

export function isUdapiService(service) {
    return Boolean(String(service?.serviceId ?? service?.id ?? '').trim());
}

export function serviceApiId(service) {
    return String(service?.serviceId ?? service?.id ?? '').trim();
}

export const SERVICE_STATUSES = ['New', 'Designed', 'Reserved', 'Active', 'Terminated', 'Retired'];
export const SERVICE_TRANSITIONS = {
    new: [{ label: 'Design', next: 'Designed' }, { label: 'Retire', next: 'Retired' }],
    designed: [{ label: 'Reserve', next: 'Reserved' }, { label: 'Retire', next: 'Retired' }],
    reserved: [{ label: 'Activate', next: 'Active' }, { label: 'Retire', next: 'Retired' }],
    terminated: [{ label: 'Activate', next: 'Active' }, { label: 'Retire', next: 'Retired' }],
    active: [{ label: 'Terminate', next: 'Terminated' }],
};
export const SERVICE_INVENTORY_SCRIPTS = {
    // Runs the service item's own (interactive) script, e.g. a Booking Manager wizard, and stores the resulting reference.
    launchItemScript: {
        name: 'Launch Interactive Subscript',
        interactive: true,
        parameterDefinitions: [{ id: 3, name: 'DOM ID' }, { id: 4, name: 'Item Label' }],
    },
};
const ACTIVE_STATUSES = new Set(['active', 'reserved', 'designed']);
const SERVICE_LIST_FIELDS = [
    'Identifier',
    'ServiceID',
    'Name',
    'StatusName',
    'StartTime',
    'EndTime',
    'Icon',
    'MonitoringService',
    'CategoryId',
    'CategoryName',
    'CategoryType',
    'ServiceSpecificationId',
    'ServiceSpecificationName',
].join(',');

export function isActiveService(service) {
    return ACTIVE_STATUSES.has(service.status.trim().toLowerCase());
}

function udapiTime(value) {
    return getDataMinerTimestamp(value) > 0 ? formatDataMinerDateTime(value) : '';
}

function mapUdapiService(service) {
    const status = String(service.StatusName ?? '');
    return {
        id: service.Identifier ?? '',
        serviceId: service.ServiceID ?? '',
        name: service.Name ?? '',
        status: status ? status[0].toUpperCase() + status.slice(1) : '',
        start: udapiTime(service.StartTime),
        end: udapiTime(service.EndTime),
        category: service.CategoryName ?? '',
        categoryType: service.CategoryType ?? '',
        logo: service.Icon ?? '',
        specification: service.ServiceSpecificationName ?? '',
        specificationId: service.ServiceSpecificationId?.Identifier ?? service.ServiceSpecificationId ?? '',
        organization: service.OrganizationName ?? '',
        organizationId: service.OrganizationId ?? '',
        linkedService: service.MonitoringService ?? '',
    };
}

export async function fetchServices() {
    return (await fetchServiceList(SERVICE_LIST_FIELDS)).map(mapUdapiService);
}

export async function fetchServicePage(options = {}) {
    const page = await fetchServiceCursorPage({ ...options, fields: SERVICE_LIST_FIELDS });
    return {
        ...page,
        items: page.items.map(mapUdapiService),
    };
}

export async function fetchServiceOverviewData(options = {}) {
    const overview = await fetchServiceOverview(options);
    return {
        ...overview,
        items: overview.items.map(mapUdapiService),
    };
}

export async function fetchServiceDetails(serviceId) {
    const service = await fetchServiceRecord(serviceId, 'OrganizationId,OrganizationName');
    return service ? mapUdapiService(service) : null;
}

export async function fetchMonitoringServices() {
    const services = await fetchMonitoringServiceList();
    return services
        .map(service => ({ id: service.id ?? service.Id ?? '', name: service.name ?? service.Name ?? '' }))
        .filter(service => service.name.trim());
}

export async function fetchServiceItems(serviceId) {
    return (await fetchServiceItemList(serviceId)).map(mapUdapiServiceItem);
}

// SDM ServiceItemTypesEnum (serialized as a number by the UDAPI) -> display value of the Get Service Items data source.
const SERVICE_ITEM_TYPES = { 0: 'Workflow', 1: 'Service', 2: 'SRM Booking' };

export function mapUdapiServiceItem(item) {
    const pick = (...names) => names.map(name => item?.[name]).find(value => value !== undefined && value !== null);
    const rawType = pick('Type', 'type');
    const id = pick('ServiceItemID', 'serviceItemID');
    return {
        id: id === undefined ? '' : String(id),
        label: String(pick('Label', 'label') ?? ''),
        type: rawType === undefined ? 'Service' : SERVICE_ITEM_TYPES[rawType] ?? String(rawType),
        definition: String(pick('DefinitionReference', 'definitionReference') ?? ''),
        implementationReference: String(pick('ImplementationReference', 'implementationReference') ?? ''),
        script: String(pick('Script', 'script') ?? ''),
        implementation: String(pick('ImplementationName') ?? ''),
        implementationState: String(pick('ImplementationState') ?? ''),
        monitoringState: String(pick('MonitoringServiceState') ?? ''),
    };
}

export async function fetchInventoryServiceItems(service) {
    const id = serviceApiId(service);
    return id ? fetchServiceItems(id) : [];
}

export async function fetchServiceScripts(serviceId) {
    const scripts = await fetchServiceScriptList(serviceId);
    return scripts
        .map(script => ({ name: script.Name ?? script.name ?? '', description: script.Description ?? script.description ?? '' }))
        .filter(script => script.name.trim() || script.description.trim());
}

const CONFIGURATION_COLUMNS = {
    versionId: 'Version ID',
    versionName: 'Version Name',
    versionDescription: 'Version Description',
    versionStart: 'Version Start',
    versionEnd: 'Version End',
    isActive: 'Is Active',
    recordType: 'Record Type',
    configurationId: 'Configuration ID',
    parameterId: 'Parameter ID',
    parameterValueId: 'Parameter Value ID',
    parameterName: 'Parameter Name',
    parameterType: 'Parameter Type',
    parameterValue: 'Parameter Value',
    mandatory: 'Mandatory',
    profileId: 'Profile ID',
    profileName: 'Profile Name',
    profileDefinitionId: 'Profile Definition ID',
    profileDefinitionName: 'Profile Definition Name',
    profileReusable: 'Profile Reusable',
    profileMandatory: 'Profile Mandatory',
    nestedProfileId: 'Nested Profile ID',
    nestedProfileName: 'Nested Profile Name',
    nestedProfileDefinitionId: 'Nested Profile Definition ID',
    nestedProfileDefinitionName: 'Nested Profile Definition Name',
    parameterLabel: 'Parameter Label',
    parameterIsLinked: 'Parameter Is Linked',
    parameterUnit: 'Parameter Unit',
    parameterUnitId: 'Parameter Unit ID',
    parameterStart: 'Parameter Start',
    parameterEnd: 'Parameter End',
    parameterStepSize: 'Parameter Step Size',
    parameterDecimals: 'Parameter Decimals',
    parameterOptions: 'Parameter Options',
    parameterFixed: 'Parameter Fixed',
    parameterNotApplicable: 'Parameter N/A',
    exposeAtOrder: 'Expose At Order',
    mandatoryAtOrder: 'Mandatory At Order',
    mandatoryAtService: 'Mandatory At Service',
};

export function mapUdapiConfigurationRecord(record) {
    const lowerCased = Object.fromEntries(Object.entries(record ?? {}).map(([key, value]) => [key.toLowerCase(), value]));
    return Object.fromEntries(Object.entries(CONFIGURATION_COLUMNS).map(([key, column]) => [column, lowerCased[key.toLowerCase()] ?? '']));
}

export function mapUdapiConfigurationRecords(records) {
    return mapServiceConfigurationRows((records ?? []).map(mapUdapiConfigurationRecord));
}

export async function fetchServiceConfigurations(serviceId) {
    return mapUdapiConfigurationRecords(await fetchServiceConfigurationList(serviceId));
}

// Groups rows with the columns of the Get_ServiceConfigurations data source into versions, profiles and available items.
export function mapServiceConfigurationRows(rows) {
    const versions = new Map();
    const availableParameters = [];
    const availableProfiles = [];
    const availableNestedProfiles = [];
    const parameterDetails = row => ({
        label: row['Parameter Label'] ?? '',
        isLinked: String(row['Parameter Is Linked'] ?? '').toLowerCase() === 'true',
        unit: row['Parameter Unit'] ?? '',
        unitId: row['Parameter Unit ID'] ?? '',
        start: row['Parameter Start'] ?? '',
        end: row['Parameter End'] ?? '',
        step: row['Parameter Step Size'] ?? '',
        decimals: row['Parameter Decimals'] ?? '',
        fixed: String(row['Parameter Fixed'] ?? '').toLowerCase() === 'true',
        notApplicable: String(row['Parameter N/A'] ?? '').toLowerCase() === 'true',
        exposeAtOrder: String(row['Expose At Order'] ?? '').toLowerCase() === 'true',
        mandatoryAtOrder: String(row['Mandatory At Order'] ?? '').toLowerCase() === 'true',
        mandatoryAtService: String(row['Mandatory At Service'] ?? '').toLowerCase() === 'true',
    });
    const parseDiscreteOptions = (raw, parameterName) => {
        if (!raw) return [];
        let parsed = raw;
        if (!Array.isArray(raw)) {
            try {
                parsed = JSON.parse(String(raw));
            } catch (error) {
                throw new Error(`Discrete options for configuration parameter "${parameterName || 'unknown'}" are invalid: ${error.message}`);
            }
        }
        if (!Array.isArray(parsed)) {
            throw new Error(`Discrete options for configuration parameter "${parameterName || 'unknown'}" are not an array.`);
        }
        return parsed.map((option, index) => {
            if (!option || typeof option !== 'object') {
                throw new Error(`Discrete option ${index + 1} for configuration parameter "${parameterName || 'unknown'}" is invalid.`);
            }
            const value = String(option.Value ?? option.value ?? '').trim();
            if (!value) {
                throw new Error(`Discrete option ${index + 1} for configuration parameter "${parameterName || 'unknown'}" has no value.`);
            }
            return { id: String(option.Id ?? option.id ?? value), value };
        });
    };
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
                exposeAtOrder: fields.exposeAtOrder ?? false,
                mandatoryAtOrder: fields.mandatoryAtOrder ?? false,
                mandatoryAtService: fields.mandatoryAtService ?? false,
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
            profile.exposeAtOrder ||= fields.exposeAtOrder ?? false;
            profile.mandatoryAtOrder ||= fields.mandatoryAtOrder ?? false;
            profile.mandatoryAtService ||= fields.mandatoryAtService ?? false;
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
                options: parseDiscreteOptions(row['Parameter Options'], row['Parameter Name']),
                ...parameterDetails(row),
                mandatory: String(row['Profile Mandatory']).toLowerCase() === 'true',
            });
        }
    };
    rows.forEach(row => {
        const recordType = row['Record Type'] ?? '';
        if (recordType === 'AvailableParameter') {
            addUnique(availableParameters, { id: row['Parameter ID'] ?? '', name: row['Parameter Name'] ?? '', type: row['Parameter Type'] ?? '', options: parseDiscreteOptions(row['Parameter Options'], row['Parameter Name']), ...parameterDetails(row) });
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
                exposeAtOrder: String(row['Expose At Order']).toLowerCase() === 'true',
                mandatoryAtOrder: String(row['Mandatory At Order']).toLowerCase() === 'true',
                mandatoryAtService: String(row['Mandatory At Service']).toLowerCase() === 'true',
            });
            const nested = ensureProfile(version, nestedProfileId, {
                name: row['Nested Profile Name'] ?? '',
                definitionId: row['Nested Profile Definition ID'] ?? '',
                definitionName: row['Nested Profile Definition Name'] ?? '',
                reusable: availableNestedProfiles.some(item => item.id === nestedProfileId && item.reusable),
                mandatory: String(row['Profile Mandatory']).toLowerCase() === 'true',
                exposeAtOrder: String(row['Expose At Order']).toLowerCase() === 'true',
                mandatoryAtOrder: String(row['Mandatory At Order']).toLowerCase() === 'true',
                mandatoryAtService: String(row['Mandatory At Service']).toLowerCase() === 'true',
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
                options: parseDiscreteOptions(row['Parameter Options'], row['Parameter Name']),
                ...parameterDetails(row),
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
