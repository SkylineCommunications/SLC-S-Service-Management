import { adHocDataSourceQuery, runQuery } from './gqi.js';
import { formatDataMinerDateTime } from './dateTime.js';
import { fetchServiceConfigurations } from './services.js';

export const CATALOG_SCRIPTS = {
    createSpecification: {
        name: 'SLC_SM_IAS_Add Service Specification',
        interactive: false,
        parameterDefinitions: [{ id: 10, name: 'DOM ID' }, { id: 11, name: 'Action' }, { id: 20, name: 'Payload' }],
    },
    deleteSpecification: {
        name: 'SLC_SM_Delete Service Specification',
        interactive: false,
        parameterDefinitions: [{ id: 10, name: 'DOM ID' }, { id: 99, name: 'Skip Confirmation' }],
    },
    manageCategories: {
        name: 'SLC_SM_AS_ManageServiceCategory',
        interactive: false,
        parameterDefinitions: [{ id: 2, name: 'domId' }, { id: 3, name: 'Action' }, { id: 20, name: 'Payload' }],
    },
    saveConfiguration: {
        name: 'SLC_SM_IAS_Service_Spec_Configuration',
        interactive: false,
        parameterDefinitions: [{ id: 10, name: 'DOM ID' }, { id: 20, name: 'Payload' }],
    },
};

const SERVICE_SPECIFICATIONS_SOURCE = {
    scriptName: 'SLC_SM_GQIDS_Get Service Specifications',
    libraryName: 'GetServiceSpecifications',
    typeFullName: 'SLC_SM_GQIDS_Get_Service_Specifications.GetServiceSpecifications',
    typeName: 'GetServiceSpecifications',
};
const SERVICE_CATEGORIES_SOURCE = {
    scriptName: 'SLC_SM_GQIDS_Get Service Categories',
    libraryName: 'GetServiceCategories',
    typeFullName: 'SLC_SM_GQIDS_Get_Service_Categories.GetServiceCategories',
    typeName: 'GetServiceCategories',
};
const DOCUMENT_HUB_LOGOS_SOURCE = {
    scriptName: 'SLC_SM_DS_GetDocumentHubLogos',
    libraryName: 'SLCSMDSGetDocumentHubLogos',
    typeFullName: 'SLCSMDSGetDocumentHubLogos.SLCSMDSGetDocumentHubLogos',
    typeName: 'SLCSMDSGetDocumentHubLogos',
};

export async function fetchServiceSpecifications() {
    const rows = await runQuery(adHocDataSourceQuery(SERVICE_SPECIFICATIONS_SOURCE));
    return rows.map(row => ({
        id: row['Specification ID'] ?? '',
        name: row.Name ?? '',
        description: row.Description ?? '',
        createdAt: formatDataMinerDateTime(row['Created At']),
        createdBy: row['Created By'] ?? '',
        lastModified: formatDataMinerDateTime(row['Last Modified']),
        lastModifiedBy: row['Last Modified By'] ?? '',
        itemCount: Number(row['Service Item Count'] ?? 0),
    }));
}

export async function fetchServiceCategories() {
    const rows = await runQuery(adHocDataSourceQuery(SERVICE_CATEGORIES_SOURCE));
    return rows.map(row => ({ id: row['Category ID'] ?? '', name: row['Category Name'] ?? '', type: row['Category Type'] ?? '', icon: row['Category Icon'] ?? '' }));
}

export async function fetchDocumentHubLogos() {
    const rows = await runQuery(adHocDataSourceQuery(DOCUMENT_HUB_LOGOS_SOURCE, { 'Bucket Name': '', 'Name Filter': '' }));
    return rows
        .map(row => ({ id: row.Icon ?? row['File Name'] ?? '', name: row['File Name'] ?? '', icon: row.Icon ?? '' }))
        .filter(logo => logo.name.trim() && logo.icon.trim());
}

export function fetchServiceSpecificationConfigurations(specificationId) {
    return fetchServiceConfigurations(specificationId, 'Specification');
}
