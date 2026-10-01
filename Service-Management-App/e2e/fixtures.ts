import type { BrowserContext, Page } from '@playwright/test';

export const APP_URL = '/public/Service-Management-App/index.html';
export const CONNECTION_GUID = '00000000-1111-4222-8333-444444444444';
export async function setSessionCookie(context: BrowserContext): Promise<void> { await context.addCookies([{ name: 'DMAConnection', value: CONNECTION_GUID, domain: 'localhost', path: '/' }]); }
export async function mockDataMinerApi(page: Page): Promise<void> { await page.route('**/API/v1/Json.asmx/IsConnectionAlive', async route => { await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ d: true }) }); }); }