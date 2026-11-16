import { chromium, request, type APIRequestContext, type BrowserContext } from '@playwright/test';

const APP_FOLDER = process.env.APP_FOLDER || 'service-management';
export const APP_ROOT_URL = `/public/${APP_FOLDER}/index.html`;

export interface DataMinerSession {
    connection: string;
    api: APIRequestContext;
}

function dmaUrl(): string {
    return (process.env.DMA_URL ?? '').replace(/\/+$/, '');
}

// dataminer.services systems sign users in through Azure AD B2C, so the credentials cannot be posted to ConnectApp.
// Signs in through the browser once and reuses the DMAConnection cookie that DataMiner sets afterwards.
export async function connect(): Promise<DataMinerSession> {
    const login = process.env.DMA_USERNAME;
    const password = process.env.DMA_PASSWORD;
    if (!login || !password) throw new Error('Set DMA_USERNAME and DMA_PASSWORD for the live Playwright tests.');

    const ignoreHTTPSErrors = process.env.DMA_IGNORE_HTTPS_ERRORS === 'true';
    const browser = await chromium.launch();
    let connection: string | undefined;
    try {
        const context = await browser.newContext({ ignoreHTTPSErrors });
        const page = await context.newPage();
        await page.goto(`${dmaUrl()}${APP_ROOT_URL}`);

        await page.waitForURL(/b2clogin\.com/, { timeout: 60_000 });
        const signInButtons = '#next, #continue, button[type="submit"]';
        await page.locator('#signInName, #email, input[type="email"]').first().fill(login);
        const passwordField = page.locator('input[type="password"]');
        if (!(await passwordField.isVisible().catch(() => false))) {
            await page.locator(signInButtons).first().click();
        }
        await passwordField.fill(password);
        await page.locator(signInButtons).first().click();

        const dmaOrigin = new URL(dmaUrl()).origin;
        await page.waitForURL(url => url.origin === dmaOrigin && !url.pathname.startsWith('/auth'), { timeout: 60_000 });
        connection = (await context.cookies(dmaUrl())).find(cookie => cookie.name === 'DMAConnection')?.value;
    } finally {
        await browser.close();
    }

    if (!connection) throw new Error('Sign-in finished but DataMiner did not set a DMAConnection cookie.');
    const api = await request.newContext({ baseURL: dmaUrl(), ignoreHTTPSErrors });
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
