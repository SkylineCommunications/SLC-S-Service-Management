---
name: sm-service-configuration-versions
scriptName: SLC_SM_UDAPI_ServiceManagement
description: 'Lists the configuration versions of a service (newest first, active one included), or returns one version. Read-only.'
sync: true
requiresUserValidation: false
inputArguments:
  - name: Request
	description: 'Stringified JSON { Route, RequestMethod, QueryParameters, RouteParameters, Body }. RequestMethod MUST be "Get". Body is an empty string.'
	example: '{"Route":"service-management/v1/service/SERVICE-12/configurationVersion","RequestMethod":"Get","QueryParameters":{},"RouteParameters":{"id":"SERVICE-12"},"Body":""}'
---

# sm-service-configuration-versions

> **Read-only tool.** Always send `"RequestMethod": "Get"`. Never `Post` (that creates a version).

## Routes
- `GET service-management/v1/service/{id}/configurationVersion`: all versions, latest start date first. The active version is always included.
- `GET service-management/v1/service/{id}/configurationVersion/{versionId}`: one version (also set `RouteParameters.versionId`).

## When to use
- Engineer questions: "What changed in the configuration of service X?", "Which configuration is active?".

## Output
JSON array of versions with start/end dates and configuration values. There is no creation timestamp. 404 when the service or version does not exist.
