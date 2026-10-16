---
name: sm-specification-get
scriptName: SLC_SM_UDAPI_ServiceManagement
description: 'Returns one service specification by ID, including service items, relationships, configuration parameters and profiles. Read-only.'
sync: true
requiresUserValidation: false
inputArguments:
  - name: Request
	description: 'Stringified JSON { Route, RequestMethod, QueryParameters, RouteParameters, Body }. RequestMethod MUST be "Get". Body is an empty string.'
	example: '{"Route":"service-management/v1/serviceSpecification/<spec-uuid>","RequestMethod":"Get","QueryParameters":{},"RouteParameters":{"id":"<spec-uuid>"},"Body":""}'
---

# sm-specification-get

> **Read-only tool.** Always send `"RequestMethod": "Get"` and an empty `Body`.

## Route
`GET service-management/v1/serviceSpecification/{id}`. Optional `fields`.

## When to use
- Engineer questions: "What does specification X define?", "Which configuration parameters are mandatory?".
