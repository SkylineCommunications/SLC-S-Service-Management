import http from 'k6/http';

// SM_API_URL is the User-Defined API base, e.g. https://dma.example.com/api/custom
// Resolved on first use, not at import: 'k6 inspect' (used by run-k6-action to validate scripts) does not see the step's env vars.
let baseUrl;

function resolveBaseUrl() {
    if (baseUrl !== undefined) return baseUrl;
    const dmaUrl = (__ENV.DMA_URL || '').replace(/\/+$/, '');
    const resolved = (__ENV.SM_API_URL || (dmaUrl ? `${dmaUrl}/api/custom` : '')).replace(/\/+$/, '');
    if (!resolved) throw new Error('Set SM_API_URL (https://<host>/api/custom) or DMA_URL.');
    if (!__ENV.SM_API_KEY) throw new Error('Set SM_API_KEY to a token that is allowed on the Service Management API.');
    baseUrl = resolved;
    return baseUrl;
}
const ROUTE_PREFIX = 'service-management/v1';

function queryString(query) {
    const pairs = Object.entries(query || {}).map(([key, value]) => `${encodeURIComponent(key)}=${encodeURIComponent(value)}`);
    return pairs.length ? `?${pairs.join('&')}` : '';
}

export function get(route, { query, token = __ENV.SM_API_KEY, expected = 200, name = route } = {}) {
    return http.get(`${resolveBaseUrl()}/${ROUTE_PREFIX}/${route}${queryString(query)}`, {
        headers: token ? { Authorization: `Bearer ${token}` } : {},
        responseCallback: http.expectedStatuses(expected),
        tags: { name },
        timeout: '60s',
    });
}

export function json(response) {
    try {
        return response.json();
    } catch (e) {
        return undefined;
    }
}
