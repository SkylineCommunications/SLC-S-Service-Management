import { chromium, request, type APIRequestContext, type BrowserContext, type Page } from '@playwright/test';

const APP_FOLDER = process.env.APP_FOLDER || 'service-management';
export const APP_ROOT_URL = `/public/${APP_FOLDER}/index.html`;

type Cookie = Awaited<ReturnType<BrowserContext['cookies']>>[number];

export interface DataMinerSession {
    connection: string;
    // All cookies from the signed-in browser: the dataminer.services relay needs its own B2C session cookies, not only DMAConnection.
    cookies: Cookie[];
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
    let cookies: Cookie[] = [];
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
        const signedIn = page.waitForURL(url => url.origin === dmaOrigin && !url.pathname.startsWith('/auth'), { timeout: 45_000 });
        const rejected = page.locator('.error.pageLevel, [role="alert"]').filter({ hasText: /incorrect|invalid|locked|disabled/i }).first()
            .waitFor({ timeout: 45_000 })
            .then(
                () => { throw new Error('dataminer.services rejected the DMA_USERNAME / DMA_PASSWORD credentials.'); },
                () => new Promise<never>(() => undefined));
        try {
            await Promise.race([signedIn, rejected]);
        } finally {
            // Playwright attaches a page snapshot to failures; never leave the password in it.
            // After a successful sign-in the B2C field no longer exists, so don't wait for it (actions have no timeout by default).
            await passwordField.fill('', { timeout: 1_000 }).catch(() => undefined);
        }
        connection = await waitForConnectionCookie(context, page);
        cookies = await context.cookies();
    } finally {
        await browser.close();
    }

    if (!connection) throw new Error('Sign-in finished but DataMiner did not set a DMAConnection cookie.');
    const api = await request.newContext({ baseURL: dmaUrl(), ignoreHTTPSErrors });
    return { connection, cookies, api };
}

// The cookie can be set by the app page after the redirect, so poll briefly and report what exists when it never shows up.
async function waitForConnectionCookie(context: BrowserContext, page: Page): Promise<string> {
    const deadline = Date.now() + 20_000;
    let names: string[] = [];
    while (Date.now() < deadline) {
        const cookies = await context.cookies();
        const cookie = cookies.find(c => c.name === 'DMAConnection');
        if (cookie) return cookie.value;
        names = cookies.map(c => `${c.name}@${c.domain}`);
        await page.waitForTimeout(500);
    }
    throw new Error(`Sign-in finished at ${new URL(page.url()).origin}${new URL(page.url()).pathname} but no DMAConnection cookie was set. Cookies: ${names.join(', ') || 'none'}.`);
}

export async function disconnect(session: DataMinerSession | undefined): Promise<void> {
    if (!session) return;
    await session.api.post('/API/v1/Json.asmx/LogOut', { data: { connection: session.connection } }).catch(() => undefined);
    await session.api.dispose();
}

// Restores the signed-in browser's cookies: the relay session for dataminer.services and the DMAConnection cookie the app reads.
export async function useSession(context: BrowserContext, session: DataMinerSession): Promise<void> {
    await context.addCookies(session.cookies);
}
