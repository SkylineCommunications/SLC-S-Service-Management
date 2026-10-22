import { callDataMinerApi, DataMinerApiError } from './transport.js';
import { getConnectionFromCookie } from './session.js';

const INTERNAL_API = '/API/v1/Internal.asmx';
const TYPE_QUERY = 'Skyline.DataMiner.Web.Common.v1.DMAGenericInterfaceQuery';
const TYPE_CHOSEN_OPTION = 'Skyline.DataMiner.Web.Common.v1.DMAGenericInterfaceQueryChosenOption';
const TYPE_PRIMITIVE = 'Skyline.DataMiner.Web.Common.v1.DMAPrimitiveValue';
const QUERY_VERSION = 65536;

async function callInternal(method, body) {
    return callDataMinerApi(`${INTERNAL_API}/${method}`, body);
}

async function closeSession(sessionID) {
    const connection = getConnectionFromCookie();
    if (!connection) return;
    try {
        await fetch(`${INTERNAL_API}/CloseQuerySession`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'same-origin',
            body: JSON.stringify({ connection, sessionID }),
        });
    } catch {
        // Best effort: query cleanup must not replace the original query error.
    }
}

function stringOption(id, value) {
    const isNull = value === null || value === undefined;
    return {
        __type: TYPE_CHOSEN_OPTION,
        ID: id,
        Type: 'string',
        Value: {
            __type: TYPE_PRIMITIVE,
            Type: 'string',
            IsNull: isNull,
            Value: isNull ? null : String(value),
        },
    };
}

/** Builds the "Custom data" query for a GQI ad hoc data source. */
export function adHocDataSourceQuery({ scriptName, libraryName, typeFullName, typeName }, args = {}) {
    const descriptor = JSON.stringify({
        DataSourceInterface: 'Skyline.DataMiner.Analytics.GenericInterface.IGQIDataSource',
        ScriptName: scriptName,
        LibraryName: libraryName,
        TypeFullName: typeFullName,
        TypeName: typeName,
    });
    return {
        __type: TYPE_QUERY,
        ID: 'Custom data',
        Version: QUERY_VERSION,
        Options: [stringOption('Data source', descriptor)],
        Child: {
            __type: TYPE_QUERY,
            ID: 'Custom data arguments',
            Version: QUERY_VERSION,
            Options: [{
                __type: TYPE_CHOSEN_OPTION,
                ID: 'Arguments',
                Type: null,
                Value: Object.entries(args).map(([name, value]) => stringOption(name, value)),
            }],
            Child: null,
        },
    };
}

function cellValue(cell) {
    const value = cell?.Value ?? cell?.DisplayValue;
    return value === null || value === undefined ? '' : String(value);
}

/** Runs a GQI query, reads every page and returns rows as objects keyed by column name. */
export async function runQuery(query, { pageSize = 500, maxPages = 1000 } = {}) {
    const options = {
        EnableUpdates: false,
        OptimizationType: 1,
        FetchLocal: true,
        UseDynamicUnits: false,
        TimeZoneID: Intl.DateTimeFormat().resolvedOptions().timeZone,
        LanguageTag: navigator.language || 'en-US',
    };
    const session = await callInternal('OpenQuerySession', { query, options });
    if (!session?.ID) throw new DataMinerApiError('DataMiner did not return a GQI query session.');

    try {
        const columns = (session.Columns || []).map(column => column?.Name);
        const rows = [];
        for (let pageIndex = 0; pageIndex < maxPages; pageIndex++) {
            const page = await callInternal('GetNextQuerySessionPage', { sessionID: session.ID, pageSize });
            for (const row of page?.Rows || []) {
                rows.push(Object.fromEntries(columns.map((name, index) => [name, cellValue(row?.Cells?.[index])])));
            }
            if (page?.IsLast !== false) return rows;
        }
        throw new DataMinerApiError(`GQI query returned more than ${maxPages} pages.`);
    } finally {
        closeSession(session.ID);
    }
}

export { DataMinerApiError };
