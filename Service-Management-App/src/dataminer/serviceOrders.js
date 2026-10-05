import { adHocDataSourceQuery, runQuery } from './gqi.js';
import { formatDataMinerDateTime } from './dateTime.js';

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

// Lifecycle order of SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.StatusesEnum (descriptions as returned by GQI).
export const ORDER_STATES = ['New', 'Acknowledged', 'In Progress', 'Held', 'Pending', 'Assess Cancellation', 'Pending Cancellation', 'Partially Failed', 'Failed', 'Rejected', 'Completed', 'Cancelled'];
export const PRIORITIES = ['High', 'Medium', 'Low'];

export const SERVICE_ORDER_SCRIPTS = {
    addOrder: {
        name: 'SLC_SM_IAS_Add Service Order',
        interactive: true,
        parameterDefinitions: [{ id: 10, name: 'DOM ID' }, { id: 11, name: 'Action' }],
    },
    deleteOrder: {
        name: 'SLC_SM_Delete Service Order',
        interactive: true,
        parameterDefinitions: [{ id: 10, name: 'DOM ID' }],
    },
    addItem: {
        name: 'SLC_SM_IAS_Add Service Order Item',
        interactive: true,
        parameterDefinitions: [{ id: 10, name: 'DOM ID' }, { id: 11, name: 'Service Order Item ID' }, { id: 12, name: 'Action' }],
    },
    deleteItem: {
        name: 'SLC_SM_Delete Service Order Item',
        interactive: true,
        parameterDefinitions: [{ id: 10, name: 'DOM ID' }, { id: 11, name: 'Service Order Item ID' }],
    },
};

const CLOSED_STATES = new Set(['completed', 'cancelled', 'rejected', 'failed']);

export function isOpenOrder(order) { return !CLOSED_STATES.has(order.state.trim().toLowerCase()); }

export async function fetchServiceOrders() {
    const rows = await runQuery(SERVICE_ORDERS_QUERY);
    return rows.map(row => ({ id: row['Order ID'] ?? '', name: row['Name'] ?? '', description: row['Description'] ?? '', priority: row['Priority'] ?? '', externalId: row['External ID'] ?? '', organization: row['Related Organization'] ?? '', state: row['State'] ?? '', start: formatDataMinerDateTime(row.Start) }));
}

export async function fetchServiceOrderItems(orderId) {
    const rows = await runQuery(adHocDataSourceQuery(SERVICE_ORDER_ITEMS_SOURCE, { 'DOM ID': orderId }));
    // The data source writes placeholder text when an item has no start or end time.
    const time = value => (!value || /^No (Start|End) Time$/i.test(value) ? '' : formatDataMinerDateTime(value));
    return rows.map(row => ({ id: row['ID'] ?? '', name: row['Name'] ?? '', action: row['Action'] ?? '', category: row['Category'] ?? '', specification: row['Service Specification'] ?? '', service: row['Service'] ?? '', start: time(row['Start']), end: time(row['End']), status: row['Status'] ?? '', description: row['Description'] ?? '' }));
}
