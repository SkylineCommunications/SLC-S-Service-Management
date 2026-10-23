import { getConnectionFromCookie, redirectToAuth } from './session.js';

const API_ROOT = '/API/v1';

export class DataMinerAuthError extends Error {
    constructor() {
        super('The DataMiner session has expired.');
        this.name = 'DataMinerAuthError';
    }
}

export class DataMinerApiError extends Error {
    constructor(message) {
        super(message);
        this.name = 'DataMinerApiError';
    }
}

function isNoConnection(payload) {
    return typeof payload?.ExceptionType === 'string'
        && payload.ExceptionType.includes('NoConnectionWebApiException');
}

export async function callDataMinerApi(path, body, { timeoutMs = 90000 } = {}) {
    const connection = getConnectionFromCookie();
    if (!connection) {
        redirectToAuth();
        throw new DataMinerAuthError();
    }

    let response;
    let payload;
    const controller = new AbortController();
    const timeout = window.setTimeout(() => controller.abort(), timeoutMs);
    try {
        response = await fetch(path, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'same-origin',
            body: JSON.stringify({ connection, ...body }),
            signal: controller.signal,
        });
        payload = await response.json().catch(() => null);
    } catch (error) {
        if (error?.name === 'AbortError') {
            throw new DataMinerApiError(`DataMiner did not complete the request within ${Math.round(timeoutMs / 1000)} seconds.`);
        }

        throw new DataMinerApiError('DataMiner could not be reached. Check your network connection.');
    } finally {
        window.clearTimeout(timeout);
    }

    if (response.status === 401 || response.status === 403 || isNoConnection(payload)) {
        redirectToAuth();
        throw new DataMinerAuthError();
    }

    if (!response.ok) {
        throw new DataMinerApiError(payload?.Message || `${path} failed (HTTP ${response.status}).`);
    }

    return payload?.d;
}

export { API_ROOT };
