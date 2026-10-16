---
name: sm-specifications-list
scriptName: SLC_SM_UDAPI_ServiceManagement
description: 'Lists service specifications (the catalog of service designs), optionally filtered by name. Read-only.'
sync: true
requiresUserValidation: false
inputArguments:
  - name: Request
	description: 'Stringified JSON { Route, RequestMethod, QueryParameters, RouteParameters, Body }. RequestMethod MUST be "Get". QueryParameters values are arrays of strings. Body is an empty string.'
	example: '{"Route":"service-management/v1/serviceSpecification","RequestMethod":"Get","QueryParameters":{"name":["Internet"],"limit":["25"]},"RouteParameters":{},"Body":""}'
---

# sm-specifications-list

> **Read-only tool.** Always send `"RequestMethod": "Get"` and an empty `Body`.

## Route
`GET service-management/v1/serviceSpecification`

## Query parameters
`name`, `offset`, `limit` (max 500; send 25 by default), `sort`, `fields`.

## When to use
- "Which service types can we offer?", or to resolve a specification name to its ID.
