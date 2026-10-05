import { adHocDataSourceQuery, runQuery } from './gqi.js';
import { formatDataMinerDateTime } from './dateTime.js';

export const CATALOG_SCRIPTS = {
    deleteSpecification: {
        name: 'SLC_SM_Delete Service Specification',
        interactive: true,
        parameterDefinitions: [{ id: 10, name: 'DOM ID' }],
    },
};

const SERVICE_SPECIFICATIONS_SOURCE = {
    scriptName: 'SLC_SM_GQIDS_Get Service Specifications',
    libraryName: 'GetServiceSpecifications',
    typeFullName: 'SLC_SM_GQIDS_Get_Service_Specifications.GetServiceSpecifications',
    typeName: 'GetServiceSpecifications',
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
