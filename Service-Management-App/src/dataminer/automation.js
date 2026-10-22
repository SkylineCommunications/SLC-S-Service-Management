import { callDataMinerApi, DataMinerApiError, DataMinerAuthError } from './transport.js';

const JSON_API = '/API/v1/Json.asmx';

function encodeScriptParameter(value) {
    if (value === null || value === undefined) return 'null';
    if (value === '') return '[]';
    return JSON.stringify([String(value)]);
}

function buildAutomationScript({ name, folder = 'Service Management', interactive = false, hasFindInteractiveClient = interactive, parameterDefinitions = [] }, values = {}) {
    if (!name) throw new DataMinerApiError('No Automation script name was provided.');

    return {
        __type: 'Skyline.DataMiner.Web.Common.v1.DMAAutomationScript',
        Name: name,
        Folder: folder,
        Description: '',
        Settings: {
            __type: 'Skyline.DataMiner.Web.Common.v1.DMAAutomationScriptSettings',
            RequireInteractive: interactive,
            HasFindInteractiveClient: hasFindInteractiveClient,
        },
        Parameters: parameterDefinitions.map(parameter => ({
            __type: 'Skyline.DataMiner.Web.Common.v1.DMAAutomationScriptParameter',
            ParameterId: parameter.id,
            Values: null,
            MemoryFile: '',
            Name: parameter.name,
            Value: parameter.raw ? String(values[parameter.name] ?? '') : encodeScriptParameter(values[parameter.name]),
        })),
        Dummies: [],
        MemoryFiles: [],
    };
}

function automationScriptOptions(waitForScript) {
    return {
        __type: 'Skyline.DataMiner.Web.Common.v1.DMAAutomationScriptOptions',
        WaitForScript: waitForScript,
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
    };
}

export async function executeAutomationScript({ name, folder = 'Service Management', interactive = false, parameterDefinitions = [] }, values = {}) {
    const script = buildAutomationScript({ name, folder, interactive, parameterDefinitions }, values);
    const result = await callDataMinerApi(`${JSON_API}/ExecuteAutomationScriptWithOutput`, {
        script,
        scriptOptions: automationScriptOptions(true),
    });
    const errorCode = Number(result?.ErrorCode);
    if (!result || (Number.isFinite(errorCode) && errorCode !== 0)) {
        throw new DataMinerApiError(result?.ErrorMessage || result?.Message || `Automation script "${name}" failed${Number.isFinite(errorCode) ? ` (error ${errorCode})` : '.'}`);
    }
    return result;
}

export function readScriptOutputs(result) {
    const entries = result?.ScriptOutput ?? result?.Output ?? result?.Outputs ?? [];
    if (!Array.isArray(entries)) return typeof entries === 'object' && entries !== null ? { ...entries } : {};
    return Object.fromEntries(entries.map(entry => [entry?.Key ?? entry?.Name, entry?.Value]).filter(([key]) => key));
}

export async function launchInteractiveAutomationScript({ name, folder = 'Service Management', parameterDefinitions = [] }, values = {}) {
    const script = buildAutomationScript({ name, folder, interactive: true, hasFindInteractiveClient: false, parameterDefinitions }, values);
    const result = await callDataMinerApi(`${JSON_API}/ExecuteAutomationScript`, {
        script,
        scriptOptions: automationScriptOptions(false),
    });
    const scriptId = typeof result === 'object' && result !== null ? result.ScriptID ?? result.ScriptId : result;
    if (scriptId === null || scriptId === undefined || scriptId === '' || !Number.isFinite(Number(scriptId))) {
        throw new DataMinerApiError(`DataMiner did not return a script ID for "${name}".`);
    }
    return Number(scriptId);
}

export async function detachAutomationScript(scriptID) {
    await callDataMinerApi(`${JSON_API}/DetachAutomationScript`, { scriptID }, { timeoutMs: 15000 });
}

export { DataMinerApiError, DataMinerAuthError };
