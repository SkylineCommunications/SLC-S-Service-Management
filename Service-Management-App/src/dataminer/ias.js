import { DataMinerApiError, DataMinerAuthError, launchInteractiveAutomationScript, detachAutomationScript } from './gqi.js';
import { getConnectionFromCookie, redirectToAuth } from './session.js';

// Headless Interactive Automation Script (IAS) client.
//
// The app renders the script's dialog itself instead of relying on a DataMiner-hosted IAS renderer:
//   1. HTTP Json.asmx/ExecuteAutomationScript (RequireInteractive, no FindInteractiveClient, WaitForScript false) -> script ID
//   2. WS   LaunchAutomationScript { scriptID }       -> attaches this WebSocket to the script's UI events
//   3. WS   DMAAutomationScriptUIUpdate               -> dialog layout (Rows/Cells/Components) + AsyncCalls
//   4. WS   DMAAutomationScriptUIDropDownUpdate × N   -> dropdown options that are loaded asynchronously
//   5. WS   ContinueAutomationScript { scriptId, values } -> button click / WantsOnChange re-render
//   6. WS   DMAAutomationScriptBasicUpdate Completed  -> script finished
// Closing the dialog before completion aborts the script with Json.asmx/DetachAutomationScript.
const GROUP_SEPARATOR = '\x1d';
const KEEP_ALIVE_MS = 30000;
const CONNECT_TIMEOUT_MS = 15000;
const DIALOG_TIMEOUT_MS = 60000;

let sharedSocket = null;

function webSocketUrl() { return `${location.protocol === 'https:' ? 'wss:' : 'ws:'}//${location.host}/API/v1/WebSocket.ashx`; }

// One WebSocket per app session, opened on first IAS launch and re-created only after it closes.
function openSocket(connection) {
    const listeners = new Set();
    let nextId = 1;
    let keepAlive = null;
    const ws = new WebSocket(webSocketUrl());
    const channel = {
        connection,
        nextId: () => nextId++,
        send: (method, parameters, id = nextId++) => { ws.send(JSON.stringify({ ClientSubscriptionID: id, Method: method, Parameters: parameters })); return id; },
        subscribe: listener => { listeners.add(listener); return () => listeners.delete(listener); },
        isOpen: () => ws.readyState === WebSocket.OPEN,
    };
    const ready = new Promise((resolve, reject) => {
        let bound = false;
        const timeout = window.setTimeout(() => { reject(new DataMinerApiError('The DataMiner WebSocket did not respond.')); ws.close(); }, CONNECT_TIMEOUT_MS);
        ws.addEventListener('open', () => ws.send(JSON.stringify({ ClientSubscriptionID: channel.nextId(), Method: 'SetConnectionID', Parameters: { connectionID: connection } })));
        ws.addEventListener('message', event => {
            let message;
            try { message = JSON.parse(event.data); } catch { return; }
            if (!bound) {
                if (message?.Type === 'String') {
                    bound = true;
                    window.clearTimeout(timeout);
                    keepAlive = window.setInterval(() => { if (ws.readyState === WebSocket.OPEN) ws.send(JSON.stringify({ ClientSubscriptionID: 0, Method: 'Ping', Parameters: {} })); }, KEEP_ALIVE_MS);
                    resolve(channel);
                } else if (message?.Type === 'WebSocketError') {
                    window.clearTimeout(timeout);
                    reject(new DataMinerApiError(message?.Data?.Message || 'DataMiner rejected the WebSocket session.'));
                    ws.close();
                }
                return;
            }
            for (const listener of [...listeners]) listener(message);
        });
        ws.addEventListener('close', () => {
            window.clearTimeout(timeout);
            window.clearInterval(keepAlive);
            if (sharedSocket?.ws === ws) sharedSocket = null;
            if (!bound) reject(new DataMinerApiError('Could not open the DataMiner WebSocket connection.'));
            for (const listener of [...listeners]) listener({ Type: '__closed' });
        });
    });
    sharedSocket = { ws, ready };
    ready.catch(() => { if (sharedSocket?.ws === ws) sharedSocket = null; });
    return ready;
}

function getSocket(connection) {
    if (sharedSocket && sharedSocket.ws.readyState <= WebSocket.OPEN) return sharedSocket.ready;
    return openSocket(connection);
}

function text(value) { return value === null || value === undefined ? '' : String(value); }

function parseOption(option) {
    if (option === null || option === undefined) return null;
    if (typeof option !== 'object') return { key: String(option), label: String(option) };
    const label = text(option.DisplayValue ?? option.Value ?? option.Key);
    const key = text(option.Key ?? option.Value ?? option.DisplayValue);
    return key || label ? { key: key || label, label: label || key } : null;
}

function parseOptions(options) { return Array.isArray(options) ? options.map(parseOption).filter(Boolean) : []; }

function parseComponent(raw) {
    return {
        type: text(raw?.Type || 'static').toLowerCase(),
        destVar: raw?.DestVar ? String(raw.DestVar) : null,
        text: text(raw?.Text),
        value: text(raw?.Value ?? raw?.InitialValue),
        className: text(raw?.Class).toLowerCase(),
        debugTag: text(raw?.DebugTag),
        enabled: raw?.IsEnabled !== false,
        visible: raw?.IsVisible !== false,
        readOnly: raw?.IsReadOnly === true,
        multiline: raw?.IsMultiline === true,
        wantsOnChange: raw?.WantsOnChange === true,
        wantsOnFocusLost: raw?.WantsOnFocusLost === true,
        placeholder: text(raw?.Placeholder ?? raw?.PlaceHolder),
        tooltip: text(raw?.TooltipText ?? raw?.Tooltip),
        validationState: text(raw?.ValidationState),
        validationText: text(raw?.ValidationText),
        options: parseOptions(raw?.Options),
        includeTime: raw?.IncludeTime !== false,
        clientTime: raw?.ReturnClientTimeInfo === true,
    };
}

export function parseLayout(layout) {
    return {
        title: text(layout?.Title),
        rows: (layout?.Rows || []).map(row => (row?.Cells || []).map(cell => ({
            colSpan: Math.max(1, Number(cell?.ColSpan) || 1),
            rowSpan: Math.max(1, Number(cell?.RowSpan) || 1),
            components: (cell?.Components || []).map(parseComponent),
        }))),
    };
}

export function layoutComponents(layout) { return layout ? layout.rows.flatMap(row => row.flatMap(cell => cell.components)) : []; }

/** Builds the ContinueAutomationScript values string: the trigger first, then every component that has a DestVar. */
export function buildIasValues(layout, values, trigger, kind = 'change') {
    const parts = [`${kind === 'focuslost' ? '_onfocuslost' : '_onchange'}=${trigger}`];
    for (const component of layoutComponents(layout)) {
        if (!component.destVar) continue;
        const value = component.type === 'button'
            ? (kind === 'change' && component.destVar === trigger ? component.destVar : '')
            : text(values[component.destVar] ?? component.value);
        parts.push(`${component.destVar}=${value}`);
        if (component.type === 'time' && component.clientTime && value) parts.push(`${component.destVar}_DateTimeComponentClientDateTime=${value}`);
    }
    return parts.join(GROUP_SEPARATOR);
}

function isProgressLayout(type, layout) {
    if (!type || type === 'InteractiveUI') return false;
    return !layoutComponents(layout).some(component => component.destVar);
}

/**
 * One interactive run of an Automation script. Subscribe to receive state snapshots:
 * starting -> interactive (layout, pendingAsync, busy) -> done | failed | closed.
 */
export class IasSession {
    constructor(script, parameterValues = {}) {
        this.script = script;
        this.parameterValues = parameterValues;
        this.state = { phase: 'starting' };
        this.listeners = new Set();
        this.subscriptionIds = new Set();
        this.dropdownOptions = new Map();
        this.layout = null;
        this.layoutVersion = 0;
        this.pendingAsync = 0;
        this.scriptId = null;
        this.channel = null;
        this.unsubscribe = null;
        this.timeout = null;
        this.finished = false;
    }

    subscribe(listener) {
        this.listeners.add(listener);
        listener(this.state);
        return () => this.listeners.delete(listener);
    }

    setState(state) {
        this.state = state;
        for (const listener of [...this.listeners]) listener(state);
    }

    armTimeout(message) {
        window.clearTimeout(this.timeout);
        this.timeout = window.setTimeout(() => this.fail(message), DIALOG_TIMEOUT_MS);
    }

    async start() {
        try {
            const connection = getConnectionFromCookie();
            if (!connection) { redirectToAuth(); throw new DataMinerAuthError(); }
            this.channel = await getSocket(connection);
            if (this.finished) return;
            this.unsubscribe = this.channel.subscribe(message => this.onMessage(message));
            this.armTimeout('The script did not show a dialog in time.');
            this.scriptId = await launchInteractiveAutomationScript(this.script, this.parameterValues);
            if (this.finished) { this.detach(); return; }
            this.subscriptionIds.add(this.channel.send('LaunchAutomationScript', { connection: this.channel.connection, scriptID: this.scriptId }));
        } catch (error) {
            if (error instanceof DataMinerAuthError) { this.finish({ phase: 'closed' }); return; }
            this.fail(error?.message || 'The interactive script could not be launched.');
        }
    }

    isOwnMessage(message) {
        const id = message?.ClientSubscriptionID;
        return id === undefined || id === null || this.subscriptionIds.has(id);
    }

    onMessage(message) {
        if (this.finished) return;
        const data = message?.Data || {};
        switch (message?.Type) {
            case '__closed':
                this.fail('The connection to DataMiner was interrupted.');
                return;
            case 'WebSocketError':
                if (this.isOwnMessage(message)) this.fail(data.Message || 'DataMiner reported an error while running the script.');
                return;
            case 'DMAAutomationScriptBasicUpdate': {
                if (!this.isOwnMessage(message)) return;
                const type = text(data.Type);
                const result = text(data.Data);
                if (type === 'Started') return;
                if (type === 'Completed') {
                    if (/fail|error|exception|abort/i.test(result) && !/succe/i.test(result)) this.finish({ phase: 'failed', error: result });
                    else this.finish({ phase: 'done', result });
                } else if (type === 'Detached') this.finish({ phase: 'closed' });
                else this.finish({ phase: 'failed', error: result || `The script stopped (${type || 'unknown state'}).` });
                return;
            }
            case 'DMAAutomationScriptUIUpdate': {
                if (!this.isOwnMessage(message)) return;
                const layout = parseLayout(data.Layout);
                if (isProgressLayout(text(data.Type), layout)) {
                    this.setState({ phase: 'progress', title: layout.title, message: layoutComponents(layout).map(component => component.text).filter(Boolean).join(' ') || 'Working…' });
                    return;
                }
                window.clearTimeout(this.timeout);
                this.dropdownOptions.clear();
                for (const component of layoutComponents(layout)) if (component.destVar && component.options.length) this.dropdownOptions.set(component.destVar, component.options);
                this.layout = layout;
                this.layoutVersion += 1;
                this.pendingAsync = Math.max(0, Number(data.AsyncCalls) || 0);
                this.emitLayout(false);
                return;
            }
            case 'DMAAutomationScriptUIDropDownUpdate': {
                if (!this.isOwnMessage(message)) return;
                const info = data.Info || {};
                if (!info.DropDownID) return;
                const previous = Number(info.Page) > 0 ? this.dropdownOptions.get(info.DropDownID) || [] : [];
                this.dropdownOptions.set(info.DropDownID, [...previous, ...parseOptions(info.Options)]);
                if (this.pendingAsync > 0) this.pendingAsync -= 1;
                this.emitLayout(this.state.busy === true);
                return;
            }
            default:
        }
    }

    emitLayout(busy) {
        if (!this.layout) return;
        for (const component of layoutComponents(this.layout)) if (component.destVar && this.dropdownOptions.has(component.destVar)) component.options = this.dropdownOptions.get(component.destVar);
        this.setState({ phase: 'interactive', layout: { ...this.layout }, version: this.layoutVersion, pendingAsync: this.pendingAsync, busy });
    }

    /** Sends the full form. `trigger` is the DestVar of the clicked button or the WantsOnChange component that changed. */
    submit(values, trigger, kind = 'change') {
        if (this.finished || !this.layout || this.state.phase !== 'interactive' || this.state.busy || this.pendingAsync > 0) return false;
        const payload = buildIasValues(this.layout, values, trigger, kind);
        this.emitLayout(true);
        this.armTimeout('DataMiner did not answer the dialog action in time.');
        try {
            this.subscriptionIds.add(this.channel.send('ContinueAutomationScript', { connection: this.channel.connection, scriptId: this.scriptId, values: payload }));
        } catch {
            this.fail('The dialog action could not be sent to DataMiner.');
            return false;
        }
        return true;
    }

    detach() {
        if (this.scriptId === null || this.scriptId === undefined) return;
        detachAutomationScript(this.scriptId).catch(() => { /* best effort */ });
    }

    /** Closes the dialog. A script that is still waiting for input is aborted on DataMiner. */
    close() {
        if (this.finished) return;
        const shouldDetach = this.state.phase !== 'done' && this.state.phase !== 'failed';
        this.finish({ phase: 'closed' });
        if (shouldDetach) this.detach();
    }

    fail(error) {
        if (this.finished) return;
        const shouldDetach = this.scriptId !== null;
        this.finish({ phase: 'failed', error });
        if (shouldDetach) this.detach();
    }

    finish(state) {
        this.finished = true;
        window.clearTimeout(this.timeout);
        this.unsubscribe?.();
        this.unsubscribe = null;
        this.setState(state);
    }
}
