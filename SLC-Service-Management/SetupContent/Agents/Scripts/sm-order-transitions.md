---
name: sm-order-transitions
scriptName: SLC_SM_UDAPI_ServiceManagement
description: 'Returns the current status of a service order and the statuses it can move to. Read-only: never sends a transition.'
sync: true
requiresUserValidation: false
inputArguments:
  - name: Request
	description: 'Stringified JSON { Route, RequestMethod, QueryParameters, RouteParameters, Body }. RequestMethod MUST be "Get". Body is an empty string.'
	example: '{"Route":"service-management/v1/serviceOrder/<order-uuid>/transition","RequestMethod":"Get","QueryParameters":{},"RouteParameters":{"id":"<order-uuid>"},"Body":""}'
---

# sm-order-transitions

> **Read-only tool.** Only `Get`. A `Post` changes the order and is forbidden for this agent.

## Route
`GET service-management/v1/serviceOrder/{id}/transition`

## When to use
- "Why is order X stuck?", "What are the next possible steps for order X?".

## Output
JSON with the current status and the allowed target statuses. 404 when the order does not exist.
