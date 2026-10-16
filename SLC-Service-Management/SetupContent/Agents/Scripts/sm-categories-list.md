---
name: sm-categories-list
scriptName: SLC_SM_UDAPI_ServiceManagement
description: 'Lists service categories, or returns one category by ID. Read-only.'
sync: true
requiresUserValidation: false
inputArguments:
  - name: Request
	description: 'Stringified JSON { Route, RequestMethod, QueryParameters, RouteParameters, Body }. RequestMethod MUST be "Get". QueryParameters values are arrays of strings. Body is an empty string.'
	example: '{"Route":"service-management/v1/serviceCategory","RequestMethod":"Get","QueryParameters":{"limit":["100"]},"RouteParameters":{},"Body":""}'
---

# sm-categories-list

> **Read-only tool.** Always send `"RequestMethod": "Get"` and an empty `Body`.

## Routes
- `GET service-management/v1/serviceCategory`: query parameters `type`, `offset`, `limit`.
- `GET service-management/v1/serviceCategory/{id}`: one category.

## When to use
- Resolve a category name to the `categoryId` used by `sm-services-list`, or answer "Which categories exist?".
