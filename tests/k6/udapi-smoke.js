import { check, group } from 'k6';
import { get, json } from './lib/udapi.js';

// Read-only: safe to run against any DataMiner with the Service Management API installed.
export const options = {
    vus: 1,
    iterations: 1,
    thresholds: {
        checks: ['rate==1'],
        'http_req_duration{expected_response:true}': ['p(95)<10000'],
    },
};

const MISSING_ID = '6f1c2a4e-0000-4000-8000-000000000000';

const LIST_ROUTES = [
    { route: 'serviceCategory', query: { limit: 5 } },
    { route: 'serviceSpecification', query: { limit: 5 } },
    { route: 'service', query: { limit: 5 } },
    { route: 'serviceOrder', query: { limit: 5 } },
    { route: 'monitoringService' },
    { route: 'plannedChange', query: { date: Math.floor(Date.now() / 1000) } },
];

const OVERVIEW_ROUTES = ['serviceSpecification/overview', 'service/overview', 'serviceOrder/overview'];

function expectJson(response, label, status = 200) {
    check(response, {
        [`${label} returns ${status}`]: (r) => r.status === status,
        [`${label} returns JSON`]: (r) => json(r) !== undefined,
    });
}

export default function () {
    group('lists', () => {
        for (const { route, query } of LIST_ROUTES) {
            expectJson(get(route, { query }), `GET ${route}`);
        }
    });

    group('overviews', () => {
        for (const route of OVERVIEW_ROUTES) {
            expectJson(get(route, { query: { limit: 3 } }), `GET ${route}`);
        }
    });

    group('validation', () => {
        expectJson(get('serviceCategory', { query: { limit: 0 }, expected: 400 }), 'GET serviceCategory?limit=0', 400);
        expectJson(get('serviceCategory/not-a-uuid', { expected: 400, name: 'serviceCategory/{id}' }), 'GET serviceCategory/{invalid id}', 400);
        expectJson(get(`serviceCategory/${MISSING_ID}`, { expected: 404, name: 'serviceCategory/{id}' }), 'GET serviceCategory/{unknown id}', 404);
        expectJson(get('plannedChange', { expected: 400 }), 'GET plannedChange without date', 400);
    });

    group('authentication', () => {
        const noToken = get('serviceCategory', { token: '', expected: 401 });
        check(noToken, { 'request without token returns 401': (r) => r.status === 401 });

        const badToken = get('serviceCategory', { token: 'invalid-token', expected: 401 });
        check(badToken, { 'request with invalid token returns 401': (r) => r.status === 401 });
    });
}
