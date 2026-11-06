---
name: sm-order-items
scriptName: SLC_SM_UDAPI_ServiceManagement
description: 'Lists the items of a service order, or returns one item. Read-only.'
sync: true
requiresUserValidation: false
inputArguments:
  - name: Request
	description: 'Stringified JSON { Route, RequestMethod, QueryParameters, RouteParameters, Body }. RequestMethod MUST be "Get". Body is an empty string.'
	example: '{"Route":"service-management/v1/serviceOrder/<order-uuid>/item","RequestMethod":"Get","QueryParameters":{},"RouteParameters":{"id":"<order-uuid>"},"Body":""}'
---

# sm-order-items

> **Read-only tool.** Always send `"RequestMethod": "Get"`. Never `Post`, `Patch` or `Delete` on item routes.

## Routes
- `GET service-management/v1/serviceOrder/{id}/item`: all items of the order.
- `GET service-management/v1/serviceOrder/{id}/item/{itemId}`: one item (`itemId` is a UUID; also set `RouteParameters.itemId`).

## When to use
- "What does order X contain?", "Which item of order X failed?", "Which service does this item create or modify?".

## Output
JSON array (or object) of items with action (`add`, `modify`, `delete`, `noChange`), status and linked specification/service.
