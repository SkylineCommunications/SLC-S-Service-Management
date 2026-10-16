---
name: sm-orders-list
scriptName: SLC_SM_UDAPI_ServiceManagement
description: 'Lists service orders with optional filters (status, priority, external ID, organization, requested start date range). Read-only.'
sync: true
requiresUserValidation: false
inputArguments:
  - name: Request
	description: 'Stringified JSON { Route, RequestMethod, QueryParameters, RouteParameters, Body }. RequestMethod MUST be "Get". QueryParameters values are arrays of strings. Body is an empty string.'
	example: '{"Route":"service-management/v1/serviceOrder","RequestMethod":"Get","QueryParameters":{"status":["inProgress,held"],"limit":["25"]},"RouteParameters":{},"Body":""}'
---

# sm-orders-list

> **Read-only tool.** Always send `"RequestMethod": "Get"` and an empty `Body`.

## Route
`GET service-management/v1/serviceOrder`

## Query parameters (all optional, values are string arrays)
| Name | Meaning |
|---|---|
| `status` | Comma-separated: `new`, `acknowledged`, `inProgress`, `held`, `pending`, `assessCancellation`, `pendingCancellation`, `completed`, `partiallyFailed`, `failed`, `rejected`, `cancelled`. |
| `priority` | `high`, `medium` or `low`. |
| `externalId` | External (customer/OSS) order reference. |
| `organizationId` | Organization ID (UUID). |
| `requestedStartDate.gte`, `requestedStartDate.lte` | Requested start date range, Unix time in milliseconds. |
| `offset`, `limit` | Paging. Send a limit (e.g. 25). |
| `sort` | Sort expression. |
| `fields` | Comma-separated properties to return. |

## When to use
- "Which orders are still open?", "Show held orders", "Find the order with external ID ABC".

## Output
JSON array of orders. 400 = invalid filter.
