---
name: sm-service-configuration-versions
scriptName: SLC_SM_UDAPI_ServiceManagement
description: 'Returns the configuration versions of a service with their parameter values and profiles as flat records. Read-only.'
sync: true
requiresUserValidation: false
inputArguments:
  - name: Request
	description: 'Stringified JSON { Route, RequestMethod, QueryParameters, RouteParameters, Body }. RequestMethod MUST be "Get". Body is an empty string.'
	example: '{"Route":"service-management/v1/service/SERVICE-12/configuration","RequestMethod":"Get","QueryParameters":{},"RouteParameters":{"id":"SERVICE-12"},"Body":""}'
---

# sm-service-configuration-versions

> **Read-only tool.** Always send `"RequestMethod": "Get"`. Never `Put` (that saves a version).

## Route
- `GET service-management/v1/service/{id}/configuration`: all versions of the service. Group the records by `VersionId`.

## When to use
- Engineer questions: "What changed in the configuration of service X?", "Which configuration is active?".

## Output
JSON array of flat records. `RecordType` tells what a record is: `Version` (one per version, with `VersionName`, `VersionDescription`, `VersionStart`, `VersionEnd` and `IsActive`), `Parameter` (`ParameterName`, `ParameterValue`, `ParameterUnit`), `Profile`/`NestedProfile` (`ProfileName`, `ProfileDefinitionName`) and `ProfileParameter`/`NestedProfileParameter`. Ignore `AvailableProfile`/`AvailableNestedProfile` records; they are editor options, not configuration. There is no creation timestamp. A version with `VersionId` `SpecificationTemplate` is the unsaved specification template, not a stored version. 404 when the service does not exist.
