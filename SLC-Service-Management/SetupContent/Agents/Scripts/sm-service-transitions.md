---
name: sm-service-transitions
scriptName: SLC_SM_UDAPI_ServiceManagement
description: 'Returns the current status of a service and the statuses it can move to. Read-only: never sends a transition.'
sync: true
requiresUserValidation: false
inputArguments:
  - name: Request
	description: 'Stringified JSON { Route, RequestMethod, QueryParameters, RouteParameters, Body }. RequestMethod MUST be "Get". Body is an empty string.'
	example: '{"Route":"service-management/v1/service/SERVICE-12/transition","RequestMethod":"Get","QueryParameters":{},"RouteParameters":{"id":"SERVICE-12"},"Body":""}'
---

# sm-service-transitions

> **Read-only tool.** Only `Get`. A `Post` to this route changes the service and is forbidden for this agent.

## Route
`GET service-management/v1/service/{id}/transition`

## When to use
- "What state is service X in?", "Why can't service X be retired?", "What can happen next to this service?".

## Output
JSON with the current status and `allowedStatuses`. 404 when the service does not exist.
