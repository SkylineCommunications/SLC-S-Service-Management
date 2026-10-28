import { check, sleep } from 'k6';
import { get } from '../lib/udapi.js';

// Not matched by the CI glob (tests/k6/*.js); run manually: k6 run tests/k6/load/udapi-read-load.js
// UDAPI triggers are queued above the agent's concurrency limit (min. 4), so keep VUs modest on shared systems.
export const options = {
    scenarios: {
        browse: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: [
                { duration: '30s', target: Number(__ENV.VUS || 4) },
                { duration: '2m', target: Number(__ENV.VUS || 4) },
                { duration: '30s', target: 0 },
            ],
        },
    },
    thresholds: {
        http_req_failed: ['rate<0.01'],
        'http_req_duration{name:service}': ['p(95)<5000'],
        'http_req_duration{name:serviceOrder}': ['p(95)<5000'],
        'http_req_duration{name:serviceSpecification}': ['p(95)<5000'],
        'http_req_duration{name:serviceCategory}': ['p(95)<3000'],
    },
};

const ROUTES = ['service', 'serviceOrder', 'serviceSpecification', 'serviceCategory'];

export default function () {
    const route = ROUTES[Math.floor(Math.random() * ROUTES.length)];
    const response = get(route, { query: { limit: 25 } });
    check(response, { [`GET ${route} returns 200`]: (r) => r.status === 200 });
    sleep(1);
}
