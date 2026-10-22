import { adHocDataSourceQuery, runQuery } from './gqi.js';
import { formatDataMinerDateTime, getDataMinerTimestamp } from './dateTime.js';
import { mapUdapiConfigurationRecords } from './services.js';
import {
    addServiceOrderItem,
    createServiceOrder,
    deleteServiceOrder,
    deleteServiceOrderItem,
    fetchRelatedServiceOrderList,
    fetchServiceOrderItemConfigurationList,
    fetchServiceOrderItemList,
    fetchServiceOrderList,
    initializeServiceFromOrderItem,
    saveServiceOrderItemConfiguration,
    transitionServiceOrder,
    transitionServiceOrderItem,
    updateServiceOrder,
    updateServiceOrderItem,
} from './udapi.js';

export {
    addServiceOrderItem,
    createServiceOrder,
    deleteServiceOrder,
    deleteServiceOrderItem,
    initializeServiceFromOrderItem,
    saveServiceOrderItemConfiguration,
    transitionServiceOrder,
    transitionServiceOrderItem,
    updateServiceOrder,
    updateServiceOrderItem,
};

const SERVICE_ORGANIZATIONS_SOURCE = {
    scriptName: 'SLC_SM_GQIDS_Get Service Organizations',
    libraryName: 'GetServiceOrganizations',
    typeFullName: 'SLC_SM_GQIDS_Get_Service_Organizations.GetOrganizations',
    typeName: 'GetOrganizations',
};

// Lifecycle order of SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.StatusesEnum (descriptions as returned by GQI).
export const ORDER_STATES = ['New', 'Acknowledged', 'In Progress', 'Held', 'Pending', 'Assess Cancellation', 'Pending Cancellation', 'Partially Failed', 'Failed', 'Rejected', 'Completed', 'Cancelled'];
export const PRIORITIES = ['High', 'Medium', 'Low'];

const ORDER_TRANSITIONS = {
    new: [{ label: 'Acknowledge', next: 'Acknowledged' }, { label: 'Reject', next: 'Rejected' }],
    acknowledged: [{ label: 'Initialize', next: 'Inprogress' }, { label: 'Reject', next: 'Rejected' }],
    'in progress': [{ label: 'Complete', next: 'Completed' }, { label: 'Failed', next: 'Failed' }, { label: 'Partially Failed', next: 'Partial' }, { label: 'Issue', next: 'Held' }, { label: 'Information Missing', next: 'Pending' }],
    pending: [{ label: 'Request Cancellation', next: 'Assesscancellation' }, { label: 'In Progress', next: 'Inprogress' }],
    'assess cancellation': [{ label: 'Confirm Cancellation', next: 'Pendingcancellation' }, { label: 'Issue', next: 'Held' }, { label: 'Information Missing', next: 'Pending' }],
    'pending cancellation': [{ label: 'Cancel', next: 'Cancelled' }],
    held: [{ label: 'In Progress', next: 'Inprogress' }, { label: 'Request Cancellation', next: 'Assesscancellation' }],
};
const ORDER_ITEM_TRANSITIONS = {
    new: [{ label: 'Acknowledge', next: 'Acknowledged' }, { label: 'Reject', next: 'Rejected' }],
    acknowledged: [{ label: 'Initialize', next: 'Inprogress' }, { label: 'Reject', next: 'Rejected' }],
    'in progress': [{ label: 'Complete', next: 'Completed' }, { label: 'Failed', next: 'Failed' }, { label: 'Partially Failed', next: 'Partial' }, { label: 'Issue', next: 'Held' }, { label: 'Information Missing', next: 'Pending' }],
    pending: [{ label: 'Request Cancellation', next: 'Assesscancellation' }, { label: 'In Progress', next: 'Inprogress' }],
    'assess cancellation': [{ label: 'Confirm Cancellation', next: 'Pendingcancellation' }, { label: 'Issue', next: 'Held' }, { label: 'Information Missing', next: 'Pending' }],
    'pending cancellation': [{ label: 'Cancel', next: 'Cancelled' }],
    held: [{ label: 'In Progress', next: 'Inprogress' }, { label: 'Request Cancellation', next: 'Assesscancellation' }],
};

function transitionStateKey(state) { return String(state || '').trim().toLowerCase(); }
export function getOrderTransitions(state) { return ORDER_TRANSITIONS[transitionStateKey(state)] || []; }
export function getOrderItemTransitions(state) { return ORDER_ITEM_TRANSITIONS[transitionStateKey(state)] || []; }

export async function fetchServiceOrderItemConfigurations(orderId, itemId) {
    return mapUdapiConfigurationRecords(await fetchServiceOrderItemConfigurationList(orderId, itemId));
}

const CLOSED_STATES = new Set(['completed', 'cancelled', 'rejected', 'failed']);

export function isOpenOrder(order) { return !CLOSED_STATES.has(order.state.trim().toLowerCase()); }

// Order of the Serviceorder(item)_Behavior StatusesEnum, used when the UDAPI returns the status as a number.
const UDAPI_STATUSES = ['New', 'Acknowledged', 'In Progress', 'Completed', 'Rejected', 'Failed', 'Partially Failed', 'Held', 'Pending', 'Assess Cancellation', 'Pending Cancellation', 'Cancelled'];

function enumLabel(value, labels) {
    if (typeof value === 'number') return labels[value] ?? '';
    const text = String(value ?? '').trim().replace(/([a-z])([A-Z])/g, '$1 $2');
    return text ? text[0].toUpperCase() + text.slice(1) : '';
}

function udapiDate(value) { return getDataMinerTimestamp(value) > 0 ? value : ''; }

function udapiReference(reference) {
    const id = String(reference?.Identifier ?? '');
    return /^0{8}-(0{4}-){3}0{12}$/.test(id) ? '' : id;
}

function mapUdapiOrder(order) {
    const completion = order.CompletionInfo ?? {};
    return {
        id: order.Identifier ?? '',
        orderId: order.OrderId ?? '',
        name: order.Name ?? '',
        description: order.Description ?? '',
        priority: enumLabel(order.Priority, PRIORITIES),
        externalId: order.ExternalID ?? '',
        organization: order.OrganizationName ?? '',
        organizationId: order.OrganizationId ?? '',
        state: enumLabel(order.StatusName ?? order.Status, UDAPI_STATUSES),
        start: formatDataMinerDateTime(udapiDate(completion.RequestedStartDate)),
        end: formatDataMinerDateTime(udapiDate(completion.RequestedCompletedDate)),
        completionDate: udapiDate(completion.CompletionDate) || null,
        createdAt: formatDataMinerDateTime(udapiDate(order.CreatedAt)),
        createdAtTimestamp: getDataMinerTimestamp(udapiDate(order.CreatedAt)),
    };
}

function mapUdapiOrderItem(item) {
    const info = item.ServiceInfo ?? {};
    return {
        id: item.Identifier ?? '',
        name: item.Name ?? '',
        action: item.Action ?? '',
        category: item.ServiceCategoryName ?? '',
        categoryId: udapiReference(info.ServiceCategoryId),
        specification: item.ServiceSpecificationName ?? '',
        specificationId: udapiReference(info.SpecificationId),
        service: item.ServiceName ?? '',
        serviceId: udapiReference(info.ServiceId),
        start: formatDataMinerDateTime(udapiDate(item.StartTime)),
        end: item.IndefiniteRuntime ? '' : formatDataMinerDateTime(udapiDate(item.EndTime)),
        status: enumLabel(item.StatusName ?? item.Status, UDAPI_STATUSES),
        description: item.Description ?? '',
    };
}

export async function fetchServiceOrders() {
    return (await fetchServiceOrderList()).map(mapUdapiOrder);
}

export async function fetchRelatedServiceOrders(serviceId) {
    const rows = await fetchRelatedServiceOrderList(serviceId);
    return rows.map(row => ({
        order: { id: row.OrderInstanceId ?? row.orderInstanceId ?? row.OrderId ?? row.orderId ?? '', name: row.OrderName ?? row.orderName ?? '' },
        item: { id: row.ServiceOrderItemId ?? row.serviceOrderItemId ?? '', action: row.Action ?? row.action ?? '', status: row.State ?? row.state ?? '' },
    }));
}

export async function fetchServiceOrganizations() {
    const rows = await runQuery(adHocDataSourceQuery(SERVICE_ORGANIZATIONS_SOURCE));
    return rows
        .map(row => ({ id: row['Organization ID'] ?? '', name: row['Organization Name'] ?? '' }))
        .filter(organization => organization.name.trim())
        .sort((left, right) => left.name.localeCompare(right.name, undefined, { sensitivity: 'base' }));
}

export async function fetchServiceOrderItems(order) {
    if (!order?.orderId) throw new Error('The service order does not have a UDAPI order ID.');
    return (await fetchServiceOrderItemList(order.orderId)).map(mapUdapiOrderItem);
}
