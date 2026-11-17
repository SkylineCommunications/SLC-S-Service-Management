import { formatDataMinerDateTime } from './dateTime.js';
import { fetchDocumentHubLogos } from './documentHub.js';
import { mapUdapiConfigurationRecords, mapUdapiServiceItem } from './services.js';
import {
    addCatalogSpecificationItem,
    createCatalogCategory,
    createCatalogSpecification,
    deleteCatalogCategory,
    deleteCatalogSpecification,
    deleteCatalogSpecificationItem,
    fetchCatalogCategories,
    fetchCatalogSpecification,
    fetchCatalogSpecificationConfiguration,
    fetchCatalogSpecificationCursorPage,
    fetchCatalogSpecificationOverview,
    fetchCatalogSpecificationList,
    saveCatalogSpecificationConfiguration,
    updateCatalogCategory,
    updateCatalogSpecification,
    updateCatalogSpecificationItem,
} from './udapi.js';

// UDAPI specification records use the SDM property names; created/modified come from the DOM metadata.
function mapUdapiSpecification(record) {
    const pick = (...names) => names.map(name => record?.[name]).find(value => value !== undefined && value !== null) ?? '';
    const items = pick('ServiceItems', 'serviceItems');
    return {
        id: String(pick('Identifier', 'identifier', 'Id', 'id')),
        name: String(pick('Name', 'name')),
        description: String(pick('Description', 'description')),
        createdAt: formatDataMinerDateTime(pick('CreatedAt', 'createdAt')),
        createdBy: String(pick('CreatedBy', 'createdBy')),
        lastModified: formatDataMinerDateTime(pick('LastModified', 'lastModified')),
        lastModifiedBy: String(pick('LastModifiedBy', 'lastModifiedBy')),
        itemCount: Array.isArray(items) ? items.filter(isRealServiceItem).length : 0,
    };
}

// Specifications always store at least one empty Service Items section (required by the DOM definition).
function isRealServiceItem(item) {
    const label = item?.Label ?? item?.label;
    const id = item?.ServiceItemID ?? item?.serviceItemID;
    return (typeof label === 'string' && label.trim() !== '') || (id !== undefined && id !== null);
}

export async function fetchSpecificationItems(specificationId) {
    const specification = await fetchCatalogSpecification(specificationId, 'ServiceItems');
    const items = specification?.ServiceItems ?? specification?.serviceItems;
    return (Array.isArray(items) ? items : [])
        .filter(isRealServiceItem)
        .sort((a, b) => Number(a?.ServiceItemID ?? Number.MAX_SAFE_INTEGER) - Number(b?.ServiceItemID ?? Number.MAX_SAFE_INTEGER))
        .map(mapUdapiServiceItem);
}

export async function fetchServiceSpecifications() {
    return (await fetchCatalogSpecificationList()).map(mapUdapiSpecification);
}

export async function fetchServiceSpecificationPage(options = {}) {
    const page = await fetchCatalogSpecificationCursorPage(options);
    return {
        ...page,
        items: page.items.map(mapUdapiSpecification),
    };
}

export async function fetchServiceSpecificationOverview(options = {}) {
    const overview = await fetchCatalogSpecificationOverview(options);
    return {
        ...overview,
        items: overview.items.map(mapUdapiSpecification),
    };
}

export async function fetchServiceCategories() {
    return fetchCatalogCategories();
}

// Configuration record property (UDAPI) -> Get_ServiceConfigurations column (GQI).
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

function mapUdapiConfigurationRecord(record) {
    const lowerCased = Object.fromEntries(Object.entries(record ?? {}).map(([key, value]) => [key.toLowerCase(), value]));
    return Object.fromEntries(Object.entries(CONFIGURATION_COLUMNS).map(([key, column]) => [column, lowerCased[key.toLowerCase()] ?? '']));
}

export async function fetchServiceSpecificationConfigurations(specificationId) {
    const records = await fetchCatalogSpecificationConfiguration(specificationId);
    return mapUdapiConfigurationRecords(records);
}

export {
    addCatalogSpecificationItem,
    createCatalogCategory,
    createCatalogSpecification,
    deleteCatalogCategory,
    deleteCatalogSpecification,
    deleteCatalogSpecificationItem,
    fetchDocumentHubLogos,
    saveCatalogSpecificationConfiguration,
    updateCatalogCategory,
    updateCatalogSpecification,
    updateCatalogSpecificationItem,
};
