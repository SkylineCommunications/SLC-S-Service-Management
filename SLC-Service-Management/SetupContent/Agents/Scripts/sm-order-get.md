---
name: sm-order-get
scriptName: SLC_SM_UDAPI_ServiceManagement
description: 'Returns one service order by ID (UUID). Read-only.'
sync: true
requiresUserValidation: false
inputArguments:
  - name: Request
	description: 'Stringified JSON { Route, RequestMethod, QueryParameters, RouteParameters, Body }. RequestMethod MUST be "Get". Body is an empty string.'
	example: '{"Route":"service-management/v1/serviceOrder/<order-uuid>","RequestMethod":"Get","QueryParameters":{},"RouteParameters":{"id":"<order-uuid>"},"Body":""}'
---

# sm-order-get

> **Read-only tool.** Always send `"RequestMethod": "Get"` and an empty `Body`.

## Route
`GET service-management/v1/serviceOrder/{id}`. Set the ID in the route and in `RouteParameters.id`. Optional `fields`.

## When to use
- Details of one order after resolving it with `sm-orders-list`.

## Output
The order JSON. 404 when it does not exist.
