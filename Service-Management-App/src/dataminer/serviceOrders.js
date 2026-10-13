import { adHocDataSourceQuery, runQuery } from './gqi.js';
import { formatDataMinerDateTime, getDataMinerTimestamp } from './dateTime.js';
import { fetchServiceConfigurations } from './services.js';

// Ad hoc data source "Get_ServiceOrders" (SLC_SM_GQIDS_Get Service Orders). The descriptor values mirror the
// deployed Automation script: <Name>, the Exe "libraryName" param, and the C# namespace + class of the IGQIDataSource.
const SERVICE_ORDERS_QUERY = adHocDataSourceQuery({
    scriptName: 'SLC_SM_GQIDS_Get Service Orders',
    libraryName: 'GetServiceOrders',
    typeFullName: 'SLC_SM_GQIDS_Get_Service_Orders.GetServiceOrders',
    typeName: 'GetServiceOrders',
});

// Ad hoc data source "Get_ServiceOrderItems" (SLC_SM_GQIDS_Get Service Order Items); note the "_1" namespace suffix.
const SERVICE_ORDER_ITEMS_SOURCE = {
    scriptName: 'SLC_SM_GQIDS_Get Service Order Items',
    libraryName: 'GetServiceOrderItems',
    typeFullName: 'SLC_SM_GQIDS_Get_Service_Order_Items_1.EventManagerGetMultipleSections',
    typeName: 'EventManagerGetMultipleSections',
};
const RELATED_SERVICE_ORDERS_SOURCE = {
    scriptName: 'SLC_SM_GQIDS_Get Related Service Orders',
    libraryName: 'GetRelatedServiceOrders',
    typeFullName: 'SLC_SM_GQIDS_Get_Related_Service_Orders.GetRelatedServiceOrders',
    typeName: 'GetRelatedServiceOrders',
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

export const SERVICE_ORDER_SCRIPTS = {
    createOrder: {
        name: 'SLC_SM_IAS_Add Service Order',
        interactive: false,
        parameterDefinitions: [{ id: 10, name: 'DOM ID' }, { id: 11, name: 'Action' }, { id: 20, name: 'Payload' }],
    },
    deleteOrder: {
        name: 'SLC_SM_Delete Service Order',
        interactive: false,
        parameterDefinitions: [{ id: 10, name: 'DOM ID' }, { id: 99, name: 'Skip Confirmation' }],
    },
    addItem: {
        name: 'SLC_SM_IAS_Add Service Order Item',
        interactive: false,
        parameterDefinitions: [{ id: 10, name: 'DOM ID' }, { id: 11, name: 'Service Order Item ID' }, { id: 12, name: 'Action' }, { id: 20, name: 'Payload' }],
    },
    deleteItem: {
        name: 'SLC_SM_Delete Service Order Item',
        interactive: false,
        parameterDefinitions: [{ id: 10, name: 'DOM ID' }, { id: 11, name: 'Service Order Item ID' }, { id: 99, name: 'Skip Confirmation' }],
    },
    saveItemConfiguration: {
        name: 'SLC_SM_IAS_Service_Order_Configuration',
        interactive: false,
        parameterDefinitions: [{ id: 10, name: 'DOM ID' }, { id: 20, name: 'Payload' }],
    },
    transitionOrder: {
        name: 'ServiceOrder_StateTranstitions',
        interactive: false,
        parameterDefinitions: [{ id: 2, name: 'PreviousState' }, { id: 3, name: 'NextState' }, { id: 4, name: 'ServiceOrderReference' }, { id: 5, name: 'Skip Confirmation' }, { id: 6, name: 'Cancellation Reason' }],
    },
    transitionOrderItem: {
        name: 'ServiceOrderItem_StateTranstitions',
        interactive: false,
        parameterDefinitions: [{ id: 1, name: 'Id' }, { id: 2, name: 'PreviousState' }, { id: 3, name: 'NextState' }, { id: 4, name: 'Skip Confirmation' }],
    },
};

export function fetchServiceOrderItemConfigurations(orderItemId) {
    return fetchServiceConfigurations(orderItemId, 'OrderItem');
}

const CLOSED_STATES = new Set(['completed', 'cancelled', 'rejected', 'failed']);

export function isOpenOrder(order) { return !CLOSED_STATES.has(order.state.trim().toLowerCase()); }

export async function fetchServiceOrders() {
    const rows = await runQuery(SERVICE_ORDERS_QUERY);
    return rows.map(row => ({ id: row['Order ID'] ?? '', name: row['Name'] ?? '', description: row['Description'] ?? '', priority: row['Priority'] ?? '', externalId: row['External ID'] ?? '', organization: row['Related Organization'] ?? '', state: row['State'] ?? '', start: formatDataMinerDateTime(row.Start), end: formatDataMinerDateTime(row.End), createdAt: formatDataMinerDateTime(row['Created At']), createdAtTimestamp: getDataMinerTimestamp(row['Created At']) }));
}

export async function fetchRelatedServiceOrders(serviceId) {
    const rows = await runQuery(adHocDataSourceQuery(RELATED_SERVICE_ORDERS_SOURCE, { 'Service ID': serviceId }));
    return rows.map(row => ({
        order: { id: row['Order ID'] ?? '', name: row['Order Name'] ?? '' },
        item: { id: row['Service Order Item ID'] ?? '', action: row.Action ?? '', status: row.State ?? '' },
    }));
}

export async function fetchServiceOrganizations() {
    const rows = await runQuery(adHocDataSourceQuery(SERVICE_ORGANIZATIONS_SOURCE));
    return rows
        .map(row => ({ id: row['Organization ID'] ?? '', name: row['Organization Name'] ?? '' }))
        .filter(organization => organization.name.trim())
        .sort((left, right) => left.name.localeCompare(right.name, undefined, { sensitivity: 'base' }));
}

export async function fetchServiceOrderItems(orderId) {
    const rows = await runQuery(adHocDataSourceQuery(SERVICE_ORDER_ITEMS_SOURCE, { 'DOM ID': orderId }));
    // The data source writes placeholder text when an item has no start or end time.
    const time = value => (!value || /^No (Start|End) Time$/i.test(value) ? '' : formatDataMinerDateTime(value));
    return rows.map(row => ({ id: row['ID'] ?? '', name: row['Name'] ?? '', action: row['Action'] ?? '', category: row['Category'] ?? '', categoryId: row['Category ID'] ?? '', specification: row['Service Specification'] ?? '', specificationId: row['Service Specification ID'] ?? '', service: row['Service'] ?? '', serviceId: row.ServiceId ?? '', start: time(row['Start']), end: time(row['End']), status: row['Status'] ?? '', description: row['Description'] ?? '' }));
}
