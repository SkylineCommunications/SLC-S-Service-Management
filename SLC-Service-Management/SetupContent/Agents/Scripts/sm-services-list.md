---
name: sm-services-list
scriptName: SLC_SM_UDAPI_ServiceManagement
description: 'Lists services in the Service Management inventory, with optional filters (name, status, category, organization, specification, configuration parameter, date range). Read-only.'
sync: true
requiresUserValidation: false
inputArguments:
  - name: Request
	description: 'Stringified JSON { Route, RequestMethod, QueryParameters, RouteParameters, Body }. RequestMethod MUST be "Get". QueryParameters values are arrays of strings. Body is an empty string.'
	example: '{"Route":"service-management/v1/service","RequestMethod":"Get","QueryParameters":{"name":["Internet"],"status":["active"],"limit":["25"]},"RouteParameters":{},"Body":""}'
---

# sm-services-list

> **Read-only tool.** Always send `"RequestMethod": "Get"` and an empty `Body`.

## Route
`GET service-management/v1/service`

## Query parameters (all optional, values are string arrays)
| Name | Meaning |
|---|---|
| `name` | Filter on service name. |
| `status` | Comma-separated lowercase statuses (e.g. `new,active`). |
| `categoryId` | Service category ID (see `sm-categories-list`). |
| `organizationId` | Organization ID (UUID). |
| `serviceSpecificationId` | Specification ID (see `sm-specifications-list`). |
| `parameter` | Configuration filter `parameter:value`. The parameter is an ID or label; separate several accepted values with `\|`. Add one array entry per criterion. |
| `profileDefinition` | Comma-separated profile definition IDs. |
| `startDate`, `endDate` | Unix time in milliseconds. |
| `offset`, `limit` | Paging. Default limit 100; send a smaller limit (e.g. 25). |
| `sort` | Sort expression. |
| `fields` | Comma-separated properties to return (e.g. `id,name,status`). |

## When to use
- "Which services does customer X have?", "Show active services", "Which services use 10G?" (`parameter` = `Bandwidth:10G`).
- First step to resolve a service name to its ID (`SERVICE-<number>`).

## Output
JSON array of services. 400 = invalid filter: read the message and fix the parameters.
