import { executeAutomationScript, readScriptOutputs } from './automation.js';

// Runs the Service Management UDAPI routes through the UDAPI script with the user's session; the script returns the HTTP status and body as outputs.
const UDAPI_SCRIPT = {
    name: 'SLC_SM_UDAPI_ServiceManagement',
    folder: 'Service Management/UDAPI',
    interactive: false,
    parameterDefinitions: [{ id: 2, name: 'Request', raw: true }],
};
const ROUTE_PREFIX = 'service-management/v1/';
// Skyline.DataMiner.Net.Apps.UserDefinableApis.RequestMethod
const REQUEST_METHODS = { GET: 1, PUT: 2, POST: 3, DELETE: 4, PATCH: 5 };

export class UdapiError extends Error {
    constructor(message, status = 0) {
        super(message);
        this.name = 'UdapiError';
        this.status = status;
    }
}

function parseJson(text) {
    if (typeof text !== 'string' || !text.trim()) return null;
    try {
        return JSON.parse(text);
    } catch {
        return null;
    }
}

function errorMessage(payload, status) {
    const errors = payload?.error ?? payload?.errors;
    const details = (Array.isArray(errors) ? errors[0]?.detail || errors[0]?.title : null) || payload?.message || payload?.Message;
    return details || `Service Management API request failed with HTTP ${status}.`;
}

function toQueryParameters(query = {}) {
    return Object.fromEntries(Object.entries(query)
        .filter(([, value]) => value !== undefined && value !== null && value !== '')
        .map(([key, value]) => [key, (Array.isArray(value) ? value : [value]).map(String)]));
}

async function callServiceManagementUdapi(route, { method = 'GET', body, query } = {}) {
    const requestMethod = REQUEST_METHODS[method.toUpperCase()];
    if (!requestMethod) throw new UdapiError(`Unsupported HTTP method ${method}.`);
    const request = {
        Route: `${ROUTE_PREFIX}${String(route).replace(/^\/+|\/+$/g, '')}`,
        RequestMethod: requestMethod,
        Body: body === undefined ? '' : JSON.stringify(body),
        QueryParameters: toQueryParameters(query),
        RouteParameters: {},
    };
    const result = await executeAutomationScript(UDAPI_SCRIPT, { Request: JSON.stringify(request) });
    const outputs = readScriptOutputs(result);
    const status = Number(outputs.StatusCode);
    if (!Number.isFinite(status) || status <= 0) {
        throw new UdapiError('The Service Management API script did not return a status code.');
    }
    const payload = parseJson(outputs.Response);
    if (status >= 400) throw new UdapiError(errorMessage(payload, status), status);
    return payload;
}

function value(row, ...names) {
    for (const name of names) {
        if (row?.[name] !== undefined && row?.[name] !== null) return row[name];
    }
    return '';
}

function mapUdapiCategory(row) {
    return {
        id: value(row, 'id', 'Id', 'identifier', 'Identifier'),
        name: value(row, 'name', 'Name'),
        type: value(row, 'type', 'Type'),
        icon: value(row, 'icon', 'Icon'),
    };
}

export async function fetchCatalogCategories() {
    const payload = await callServiceManagementUdapi('serviceCategory');
    return (Array.isArray(payload) ? payload : []).map(mapUdapiCategory);
}

export async function fetchCatalogSpecificationList() {
    const payload = await callServiceManagementUdapi('serviceSpecification');
    return Array.isArray(payload) ? payload : [];
}

export async function fetchCatalogSpecificationCursorPage({ limit = 100, after = '', before = '', fields = '', includeTotalCount = true, includeMetrics = false } = {}) {
    const payload = await callServiceManagementUdapi('serviceSpecification', {
        query: { pagination: 'cursor', limit, after, before, fields, includeTotalCount, includeMetrics },
    });
    if (!payload || !Array.isArray(payload.items)) {
        throw new UdapiError('The Service Specification API returned an invalid cursor page.');
    }
    return payload;
}

export async function fetchCatalogSpecificationOverview({ limit = 6 } = {}) {
    const payload = await callServiceManagementUdapi('serviceSpecification/overview', { query: { limit } });
    if (!payload || !Array.isArray(payload.items)) {
        throw new UdapiError('The Service Specification API returned an invalid overview response.');
    }
    return payload;
}

export async function fetchCatalogSpecification(id, fields) {
    if (!id) throw new UdapiError('No service specification was selected.');
    return callServiceManagementUdapi(`serviceSpecification/${encodeURIComponent(id)}`, { query: { fields } });
}

export async function fetchCatalogSpecificationConfiguration(id) {
    if (!id) throw new UdapiError('No service specification was selected.');
    const payload = await callServiceManagementUdapi(`serviceSpecification/${encodeURIComponent(id)}/configuration`);
    return Array.isArray(payload) ? payload : [];
}

// The body uses the same contract as the Payload of the SLC_SM_IAS_Service_Spec_Configuration script.
export function saveCatalogSpecificationConfiguration(id, payload) {
    if (!id) return Promise.reject(new UdapiError('No service specification was selected.'));
    return callServiceManagementUdapi(`serviceSpecification/${encodeURIComponent(id)}/configuration`, { method: 'PUT', body: payload ?? {} });
}

export function createCatalogSpecification(payload) {
    return callServiceManagementUdapi('serviceSpecification', {
        method: 'POST',
        body: { name: payload.Name, description: payload.Description, icon: payload.Icon },
    });
}

export function updateCatalogSpecification(id, payload) {
    if (!id) return Promise.reject(new UdapiError('No service specification was selected.'));
    const body = {};
    for (const [source, target] of [
        ['Name', 'name'],
        ['Description', 'description'],
        ['Icon', 'icon'],
        ['ServiceItems', 'serviceItems'],
        ['ServiceItemRelationships', 'serviceItemRelationships'],
    ]) {
        const value = payload[source] ?? payload[target];
        if (value !== undefined) body[target] = value;
    }
    return callServiceManagementUdapi(`serviceSpecification/${encodeURIComponent(id)}`, {
        method: 'PATCH',
        body,
    });
}

export function deleteCatalogSpecification(id) {
    return callServiceManagementUdapi(`serviceSpecification/${encodeURIComponent(id)}`, { method: 'DELETE' });
}

export function createCatalogCategory(payload) {
    return callServiceManagementUdapi('serviceCategory', {
        method: 'POST',
        body: { name: payload.Name, type: payload.Type, icon: payload.Icon },
    });
}

export function deleteCatalogCategory(id) {
    return callServiceManagementUdapi(`serviceCategory/${encodeURIComponent(id)}`, { method: 'DELETE' });
}

export function updateCatalogCategory(id, payload) {
    if (!id) return Promise.reject(new UdapiError('No service category was selected.'));
    return callServiceManagementUdapi(`serviceCategory/${encodeURIComponent(id)}`, {
        method: 'PATCH',
        body: { name: payload.Name, type: payload.Type, icon: payload.Icon ?? '' },
    });
}

export function addCatalogSpecificationItem(id, body) {
    if (!id) return Promise.reject(new UdapiError('No service specification was selected.'));
    return callServiceManagementUdapi(`serviceSpecification/${encodeURIComponent(id)}/item`, { method: 'POST', body });
}

export function updateCatalogSpecificationItem(id, itemId, body) {
    if (!id) return Promise.reject(new UdapiError('No service specification was selected.'));
    if (itemId === undefined || itemId === null || itemId === '') return Promise.reject(new UdapiError('No service specification item was selected.'));
    return callServiceManagementUdapi(`serviceSpecification/${encodeURIComponent(id)}/item/${encodeURIComponent(itemId)}`, { method: 'PATCH', body });
}

export function deleteCatalogSpecificationItem(id, itemId) {
    if (!id) return Promise.reject(new UdapiError('No service specification was selected.'));
    if (itemId === undefined || itemId === null || itemId === '') return Promise.reject(new UdapiError('No service specification item was selected.'));
    return callServiceManagementUdapi(`serviceSpecification/${encodeURIComponent(id)}/item/${encodeURIComponent(itemId)}`, { method: 'DELETE' });
}

function serviceOrderRoute(orderId, ...segments) {
    if (!orderId) throw new UdapiError('No service order was selected.');
    return ['serviceOrder', orderId, ...segments].map(encodeURIComponent).join('/');
}

export async function fetchServiceOrderList() {
    const payload = await callServiceManagementUdapi('serviceOrder');
    return Array.isArray(payload) ? payload : [];
}

export async function fetchServiceOrderCursorPage({ limit = 100, after = '', before = '', includeTotalCount = true, includeMetrics = false } = {}) {
    const payload = await callServiceManagementUdapi('serviceOrder', {
        query: { pagination: 'cursor', limit, after, before, includeTotalCount, includeMetrics },
    });
    if (!payload || !Array.isArray(payload.items)) {
        throw new UdapiError('The Service Order API returned an invalid cursor page.');
    }
    return payload;
}

export async function fetchServiceOrderOverview({ limit = 6 } = {}) {
    const payload = await callServiceManagementUdapi('serviceOrder/overview', { query: { limit } });
    if (!payload || !Array.isArray(payload.items)) {
        throw new UdapiError('The Service Order API returned an invalid overview response.');
    }
    return payload;
}

export async function createServiceOrder(body) {
    return callServiceManagementUdapi('serviceOrder', { method: 'POST', body });
}

export async function deleteServiceOrder(orderId) {
    return callServiceManagementUdapi(serviceOrderRoute(orderId), { method: 'DELETE' });
}

export async function transitionServiceOrder(orderId, targetStatus, cancellationReason) {
    const body = { targetStatus };
    if (cancellationReason !== undefined) body.cancellationInfo = { reason: cancellationReason };
    return callServiceManagementUdapi(serviceOrderRoute(orderId, 'transition'), { method: 'POST', body });
}

export async function fetchServiceOrderItemList(orderId) {
    const payload = await callServiceManagementUdapi(serviceOrderRoute(orderId, 'item'));
    return Array.isArray(payload) ? payload : [];
}

export async function transitionServiceOrderItem(orderId, itemId, targetStatus) {
    return callServiceManagementUdapi(serviceOrderRoute(orderId, 'item', itemId, 'transition'), { method: 'POST', body: { targetStatus } });
}

export async function fetchServiceOrderItemConfigurationList(orderId, itemId) {
    const payload = await callServiceManagementUdapi(serviceOrderRoute(orderId, 'item', itemId, 'configuration'));
    return Array.isArray(payload) ? payload : [];
}

export async function saveServiceOrderItemConfiguration(orderId, itemId, body) {
    return callServiceManagementUdapi(serviceOrderRoute(orderId, 'item', itemId, 'configuration'), { method: 'PUT', body });
}

export async function initializeServiceFromOrderItem(orderId, itemId) {
    return callServiceManagementUdapi(serviceOrderRoute(orderId, 'item', itemId, 'initializeService'), { method: 'POST', body: {} });
}

export async function updateServiceOrder(orderId, body) {
    return callServiceManagementUdapi(serviceOrderRoute(orderId), { method: 'PATCH', body });
}

export async function addServiceOrderItem(orderId, body) {
    return callServiceManagementUdapi(serviceOrderRoute(orderId, 'item'), { method: 'POST', body });
}

export async function updateServiceOrderItem(orderId, itemId, body) {
    return callServiceManagementUdapi(serviceOrderRoute(orderId, 'item', itemId), { method: 'PATCH', body });
}

export async function deleteServiceOrderItem(orderId, itemId) {
    return callServiceManagementUdapi(serviceOrderRoute(orderId, 'item', itemId), { method: 'DELETE' });
}

export async function fetchRelatedServiceOrderList(serviceId) {
    const payload = await callServiceManagementUdapi(serviceRoute(serviceId, 'relatedOrder'));
    return Array.isArray(payload) ? payload : [];
}

export async function fetchServiceScriptList(serviceId) {
    const payload = await callServiceManagementUdapi(serviceRoute(serviceId, 'script'));
    return Array.isArray(payload) ? payload : [];
}

export async function addServiceScript(serviceId, body) {
    return callServiceManagementUdapi(serviceRoute(serviceId, 'script'), { method: 'POST', body });
}

export async function updateServiceScript(serviceId, body) {
    return callServiceManagementUdapi(serviceRoute(serviceId, 'script'), { method: 'PATCH', body });
}

export async function deleteServiceScript(serviceId, description) {
    return callServiceManagementUdapi(serviceRoute(serviceId, 'script'), { method: 'DELETE', body: { description } });
}

export async function fetchMonitoringServiceList() {
    const payload = await callServiceManagementUdapi('monitoringService');
    return Array.isArray(payload) ? payload : [];
}

function serviceRoute(serviceId, ...segments) {
    if (!serviceId) throw new UdapiError('No service was selected.');
    return ['service', serviceId, ...segments].map(encodeURIComponent).join('/');
}

export async function fetchServiceList(fields) {
    const payload = await callServiceManagementUdapi('service', { query: { fields } });
    return Array.isArray(payload) ? payload : [];
}

export async function fetchServiceCursorPage({ limit = 100, after = '', before = '', fields = '', includeTotalCount = true, includeMetrics = false } = {}) {
    const payload = await callServiceManagementUdapi('service', {
        query: { pagination: 'cursor', limit, after, before, fields, includeTotalCount, includeMetrics },
    });
    if (!payload || !Array.isArray(payload.items)) {
        throw new UdapiError('The Service API returned an invalid cursor page.');
    }
    return payload;
}

export async function fetchServiceOverview({ limit = 6 } = {}) {
    const payload = await callServiceManagementUdapi('service/overview', { query: { limit } });
    if (!payload || !Array.isArray(payload.items)) {
        throw new UdapiError('The Service API returned an invalid overview response.');
    }
    return payload;
}

export async function fetchServiceRecord(serviceId, fields) {
    return callServiceManagementUdapi(serviceRoute(serviceId), { query: { fields } });
}

export async function fetchServiceConfigurationList(serviceId) {
    const payload = await callServiceManagementUdapi(serviceRoute(serviceId, 'configuration'));
    return Array.isArray(payload) ? payload : [];
}

export async function saveServiceConfiguration(serviceId, body) {
    return callServiceManagementUdapi(serviceRoute(serviceId, 'configuration'), { method: 'PUT', body });
}

export async function duplicateInventoryService(serviceId) {
    return callServiceManagementUdapi(serviceRoute(serviceId, 'duplicate'), { method: 'POST', body: {} });
}

export async function deleteInventoryService(serviceId) {
    return callServiceManagementUdapi(serviceRoute(serviceId), { method: 'DELETE' });
}

export async function createInventoryService(body) {
    return callServiceManagementUdapi('service', { method: 'POST', body });
}

export async function updateInventoryService(serviceId, body) {
    return callServiceManagementUdapi(serviceRoute(serviceId), { method: 'PATCH', body });
}

export async function transitionInventoryService(serviceId, targetStatus) {
    return callServiceManagementUdapi(serviceRoute(serviceId, 'transition'), { method: 'POST', body: { targetStatus } });
}

export async function fetchServiceItemList(serviceId) {
    const payload = await callServiceManagementUdapi(serviceRoute(serviceId, 'item'));
    return Array.isArray(payload) ? payload : [];
}

export async function addInventoryServiceItem(serviceId, body) {
    return callServiceManagementUdapi(serviceRoute(serviceId, 'item'), { method: 'POST', body });
}

export async function updateInventoryServiceItem(serviceId, itemId, body) {
    return callServiceManagementUdapi(serviceRoute(serviceId, 'item', itemId), { method: 'PATCH', body });
}

export async function deleteInventoryServiceItem(serviceId, itemId) {
    return callServiceManagementUdapi(serviceRoute(serviceId, 'item', itemId), { method: 'DELETE' });
}
