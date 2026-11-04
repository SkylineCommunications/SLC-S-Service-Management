---
name: sm-service-get
scriptName: SLC_SM_UDAPI_ServiceManagement
description: 'Returns one service by ID (format SERVICE-<number>). Use full=true for service items, profiles and configuration versions. Read-only.'
sync: true
requiresUserValidation: false
inputArguments:
  - name: Request
	description: 'Stringified JSON { Route, RequestMethod, QueryParameters, RouteParameters, Body }. RequestMethod MUST be "Get". QueryParameters values are arrays of strings. Body is an empty string.'
	example: '{"Route":"service-management/v1/service/SERVICE-12","RequestMethod":"Get","QueryParameters":{"full":["true"]},"RouteParameters":{"id":"SERVICE-12"},"Body":""}'
---

# sm-service-get

> **Read-only tool.** Always send `"RequestMethod": "Get"` and an empty `Body`.

## Route
`GET service-management/v1/service/{id}`. The `id` has the format `SERVICE-<number>`; put it in the route and in `RouteParameters.id`.

## Query parameters
| Name | Meaning |
|---|---|
| `full` | `true` returns the complete service: category, items, relationships, profiles, active configuration and configuration versions. |
| `fields` | Comma-separated properties to return. |

## When to use
- Details of one service after resolving it with `sm-services-list`.
- Engineer questions about service items, configuration values and profiles (`full=true`).

## Output
The service JSON. 404 when the ID does not exist or has the wrong format.
