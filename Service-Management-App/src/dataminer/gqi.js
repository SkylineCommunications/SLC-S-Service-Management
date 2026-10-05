import { getConnectionFromCookie, redirectToAuth } from './session.js';

// GQI over the DataMiner Web Services v1 JSON API (same origin).
//
// Transport: the synchronous query-session methods on /API/v1/Internal.asmx, the same HTTP calls
// DataMiner Dashboards / Low-Code Apps use to page through GQI results:
//   1. POST Internal.asmx/OpenQuerySession        { connection, query, options }  -> d: { ID, Columns: [{ Name, ... }] }
//   2. POST Internal.asmx/GetNextQuerySessionPage { connection, sessionID, pageSize } -> d: { Rows: [{ Cells: [{ Value, DisplayValue }] }], IsLast }
//      (repeated until IsLast is true)
//   3. POST Internal.asmx/CloseQuerySession       { connection, sessionID }  (always, also on error)
// This is preferred over OpenQuerySessionAsync for one-shot fetches: the async variant delivers pages through a
// WebSocket event queue (SetConnectionID/GetEvents), which adds a second channel that can fail inside the iframe.
//
// Ad hoc data sources are selected through the built-in "Custom data" node. Its "Data source" option holds a JSON
// descriptor of the IGQIDataSource class (script name, script libraryName, namespace-qualified class name); its
// "Custom data arguments" child node carries the input arguments as chosen options keyed by argument name.
const INTERNAL_API = '/API/v1/Internal.asmx';
const TYPE_QUERY = 'Skyline.DataMiner.Web.Common.v1.DMAGenericInterfaceQuery';
const TYPE_CHOSEN_OPTION = 'Skyline.DataMiner.Web.Common.v1.DMAGenericInterfaceQueryChosenOption';
const TYPE_PRIMITIVE = 'Skyline.DataMiner.Web.Common.v1.DMAPrimitiveValue';
const QUERY_VERSION = 65536;

export class DataMinerAuthError extends Error { constructor() { super('The DataMiner session has expired.'); this.name = 'DataMinerAuthError'; } }
export class DataMinerApiError extends Error { constructor(message) { super(message); this.name = 'DataMinerApiError'; } }

function isNoConnection(payload) { return typeof payload?.ExceptionType === 'string' && payload.ExceptionType.includes('NoConnectionWebApiException'); }

async function callApi(path, body) {
    const connection = getConnectionFromCookie();
    if (!connection) { redirectToAuth(); throw new DataMinerAuthError(); }
    let response;
    try { response = await fetch(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, credentials: 'same-origin', body: JSON.stringify({ connection, ...body }) }); }
    catch { throw new DataMinerApiError('DataMiner could not be reached. Check your network connection.'); }
    if (response.status === 401 || response.status === 403) { redirectToAuth(); throw new DataMinerAuthError(); }
    const payload = await response.json().catch(() => null);
    if (isNoConnection(payload)) { redirectToAuth(); throw new DataMinerAuthError(); }
    if (!response.ok) throw new DataMinerApiError(payload?.Message || `${path} failed (HTTP ${response.status}).`);
    return payload?.d;
}

async function callInternal(method, body) { return callApi(`${INTERNAL_API}/${method}`, body); }

async function closeSession(sessionID) {
    // Best effort: never redirect or surface errors while cleaning up.
    const connection = getConnectionFromCookie();
    if (!connection) return;
    try { await fetch(`${INTERNAL_API}/CloseQuerySession`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, credentials: 'same-origin', body: JSON.stringify({ connection, sessionID }) }); } catch { /* ignore */ }
}

function stringOption(id, value) {
    const isNull = value === null || value === undefined;
    return { __type: TYPE_CHOSEN_OPTION, ID: id, Type: 'string', Value: { __type: TYPE_PRIMITIVE, Type: 'string', IsNull: isNull, Value: isNull ? null : String(value) } };
}

function encodeScriptParameter(value) {
    if (value === null || value === undefined) return 'null';
    if (value === '') return '[]';
    return JSON.stringify([String(value)]);
}

/**
 * Executes a deployed DataMiner Automation script through the JSON API.
 * `parameterDefinitions` must include every parameter declared by the script XML.
 */
export async function executeAutomationScript({ name, folder = 'Service Management', interactive = false, parameterDefinitions = [] }, values = {}) {
    if (!name) throw new DataMinerApiError('No Automation script name was provided.');

    const script = {
        __type: 'Skyline.DataMiner.Web.Common.v1.DMAAutomationScript',
        Name: name,
        Folder: folder,
        Description: '',
        Settings: {
            __type: 'Skyline.DataMiner.Web.Common.v1.DMAAutomationScriptSettings',
            RequireInteractive: interactive,
            HasFindInteractiveClient: interactive,
        },
        Parameters: parameterDefinitions.map(parameter => ({
            __type: 'Skyline.DataMiner.Web.Common.v1.DMAAutomationScriptParameter',
            ParameterId: parameter.id,
            Values: null,
            MemoryFile: '',
            Name: parameter.name,
            Value: encodeScriptParameter(values[parameter.name]),
        })),
        Dummies: [],
        MemoryFiles: [],
    };
    const result = await callApi('/API/v1/Json.asmx/ExecuteAutomationScriptWithOutput', {
        script,
        scriptOptions: {
            __type: 'Skyline.DataMiner.Web.Common.v1.DMAAutomationScriptOptions',
            WaitForScript: true,
            CheckSets: true,
            LockElements: false,
            ForceLockElements: false,
            WaitWhenLocked: true,
            IsInUse: false,
            AskForConfirmation: false,
            GenerateStartedInfoEvent: true,
            customSuccessMessage: null,
            hideSuccessPopup: true,
            skipPresetsIfComplete: true,
            hidePresets: true,
            popupIsMinimizable: false,
            popupType: 1,
            ClientTimeZone: { Type: 0, Info: null },
        },
    });
    const errorCode = Number(result?.ErrorCode);
    if (!result || (Number.isFinite(errorCode) && errorCode !== 0)) {
        throw new DataMinerApiError(result?.ErrorMessage || result?.Message || `Automation script "${name}" failed${Number.isFinite(errorCode) ? ` (error ${errorCode})` : '.'}`);
    }
    return result;
}

/** Builds the "Custom data" query for an ad hoc data source. `args` maps GQI string argument names to static values. */
export function adHocDataSourceQuery({ scriptName, libraryName, typeFullName, typeName }, args = {}) {
    const descriptor = JSON.stringify({ DataSourceInterface: 'Skyline.DataMiner.Analytics.GenericInterface.IGQIDataSource', ScriptName: scriptName, LibraryName: libraryName, TypeFullName: typeFullName, TypeName: typeName });
    return {
        __type: TYPE_QUERY, ID: 'Custom data', Version: QUERY_VERSION,
        Options: [stringOption('Data source', descriptor)],
        Child: { __type: TYPE_QUERY, ID: 'Custom data arguments', Version: QUERY_VERSION, Options: [{ __type: TYPE_CHOSEN_OPTION, ID: 'Arguments', Type: null, Value: Object.entries(args).map(([name, value]) => stringOption(name, value)) }], Child: null },
    };
}

function cellValue(cell) { const value = cell?.Value ?? cell?.DisplayValue; return value === null || value === undefined ? '' : String(value); }

/** Runs a GQI query, reads every page and returns rows as objects keyed by column name. */
export async function runQuery(query, { pageSize = 500, maxPages = 1000 } = {}) {
    const options = { EnableUpdates: false, OptimizationType: 1, FetchLocal: true, UseDynamicUnits: false, TimeZoneID: Intl.DateTimeFormat().resolvedOptions().timeZone, LanguageTag: navigator.language || 'en-US' };
    const session = await callInternal('OpenQuerySession', { query, options });
    if (!session?.ID) throw new DataMinerApiError('DataMiner did not return a GQI query session.');
    try {
        const columns = (session.Columns || []).map(column => column?.Name);
        const rows = [];
        for (let pageIndex = 0; pageIndex < maxPages; pageIndex++) {
            const page = await callInternal('GetNextQuerySessionPage', { sessionID: session.ID, pageSize });
            for (const row of page?.Rows || []) rows.push(Object.fromEntries(columns.map((name, index) => [name, cellValue(row?.Cells?.[index])])));
            if (page?.IsLast !== false) return rows;
        }
        throw new DataMinerApiError(`GQI query returned more than ${maxPages} pages.`);
    } finally { closeSession(session.ID); }
}
