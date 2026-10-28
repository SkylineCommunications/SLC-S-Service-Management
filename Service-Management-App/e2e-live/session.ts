import { request, type APIRequestContext, type BrowserContext } from '@playwright/test';

const APP_FOLDER = process.env.APP_FOLDER || 'service-management';
export const APP_ROOT_URL = `/public/${APP_FOLDER}/index.html`;

export interface DataMinerSession {
    connection: string;
    api: APIRequestContext;
}

function dmaUrl(): string {
    return (process.env.DMA_URL ?? '').replace(/\/+$/, '');
}

export async function connect(): Promise<DataMinerSession> {
    const login = process.env.DMA_USERNAME;
    const password = process.env.DMA_PASSWORD;
    if (!login || !password) throw new Error('Set DMA_USERNAME and DMA_PASSWORD for the live Playwright tests.');

    const api = await request.newContext({ baseURL: dmaUrl(), ignoreHTTPSErrors: process.env.DMA_IGNORE_HTTPS_ERRORS === 'true' });
    const response = await api.post('/API/v1/Json.asmx/ConnectApp', {
        data: { host: '', login, password, clientAppName: 'Service Management live e2e', clientAppVersion: '1.0', clientComputerName: 'playwright' },
    });
    if (!response.ok()) {
        await api.dispose();
        throw new Error(`ConnectApp failed with HTTP ${response.status()}.`);
    }

    const connection = (await response.json())?.d;
    if (typeof connection !== 'string' || !connection) {
        await api.dispose();
        throw new Error('ConnectApp did not return a connection ID.');
    }

    return { connection, api };
}

export async function disconnect(session: DataMinerSession | undefined): Promise<void> {
    if (!session) return;
    await session.api.post('/API/v1/Json.asmx/LogOut', { data: { connection: session.connection } }).catch(() => undefined);
    await session.api.dispose();
}

// The app reads the connection GUID from the DMAConnection cookie that DataMiner's /auth/ page normally sets.
export async function useSession(context: BrowserContext, session: DataMinerSession): Promise<void> {
    await context.addCookies([{ name: 'DMAConnection', value: session.connection, url: dmaUrl(), sameSite: 'Strict' }]);
}
