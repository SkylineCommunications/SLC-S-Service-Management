# Service Management regression tests

## Scope and execution model

The regression tests are DataMiner Automation scripts named `SLC_SM_RT_*`, not locally runnable unit tests. They use `SLC_SM_TestLibrary` to create fixtures, run test cases, clean up, and return a per-case JSON report in the `report` script output.

They run through QAOps. The `SLC-Service-Management-QAOps` Test Package (`.dmtest`) includes the shared test library and projects matched by `Tests\Regressions Tests\*\*.csproj`, plus the product `.dmapp` and the `Tests\SLC_SM_RT.QAOps.IntegrationTests` MSTest project. On a clean QAOps DataMiner it installs the scripts, deploys the product, and runs one MSTest test per script. A test passes only when every test case in the script's report succeeded.

## Prerequisites

- A QAOps target (RC, Main or Feature) meeting the Service Management product minimum version (10.6.8 or later).
- The Catalog token in `skyline__sdk__dataminertoken` to build the package, and a QAOps token to run it.
- Run one suite at a time; the tests mutate DOM data.

The RT fixtures are created by the tests and use unique names/IDs; manual seed data is not expected. Cleanup runs as part of the test flow.

## Build and run

```powershell
dotnet build "SLC-Service-Management-QAOps\SLC-Service-Management-QAOps.csproj" -c Release
dataminer-qaops test-run-and-wait -t 01KTRNHRHWQJ9SMESYJAFP8SE9 -c <configuration ID> --token "<QAOps token>" `
  --test-packages "<full path>\SLC-Service-Management-QAOps.<version>.dmtest" -rf "<full path>\results.json" -tags "<unique tag>"
```

Configuration IDs: RC `01KTRNMVWEZX5EAW3GRDB744G4`, Main `01KTRNTY3WW3JANCQ7WPWBEAVM`, Feature `01KTRNSARVCH5DG50QM0RW7GYA`. Treat a build output containing `error :` or `Failed to download catalog item` as failed. Each RT appears as one `Ok` or `Fail` row in `results.json` and in QAOps; failures list the failing test cases and script errors. No CI workflow triggers the run yet.

## Test inventory

| Group | Regression scripts |
|---|---|
| Service Catalog | `CreateServiceSpecification`, `DynamicDeleteSpec`, `DeleteServiceCategory`, `DynamicDeleteService`, `ServiceSpecificationConfiguration`, `ServiceSpecificationConfiguration_Edit`, `ServiceProfiles`, `ServiceSpecificationEdit` |
| Service Inventory | `AddServiceItem`, `AddServiceItemDuplicateLabel`, `CreateServiceInventoryItem`, `ServiceConfiguration`, `ServiceConfiguration_EditDuplicate`, `ServiceConfigurationItem`, `ServiceConfigurationItem_EditDuplicate`, `ServiceStateTransition`, `InvalidServiceTransition`, `ScheduledConfigurationActivation`, `ServiceUpdate` |
| Service Order | `CreateServiceOrder`, `ServiceOrderItem`, `ServiceOrderItem_Edit`, `ServiceOrderItemConfiguration`, `ServiceOrderItemConfiguration_Edit`, `ServiceOrderItemStateTransition`, `ServiceOrderItemLifecycle`, `ServiceOrderItemCancellationFlow`, `ServiceOrderCompletion`, `ServiceOrderAcknowledgeReject`, `ServiceOrderItemRejection`, `ServiceOrderToServiceLink`, `ServiceOrderEdit` |
The script names above have the `SLC_SM_RT_` prefix in DataMiner. The solution and the QAOps integration tests currently cover 32 tests.

## Known coverage gaps and product findings

- **Specification duplication:** the product has no specification duplicate action, script, UI flow, or API. Per the requested edit-only scope, `ServiceSpecificationEdit` covers editing and preservation of specification data; it does not cover duplication. The duplicate-specification acceptance item remains unmet until the product exposes that operation.
- **Scheduled activation:** `ScheduledConfigurationActivation` verifies saving a future configuration version and explicitly activating it. The current product does not activate a version automatically when its start date arrives, so automatic scheduled activation is not covered.
- **Service-order provisioning:** `ServiceOrderToServiceLink` validates the order-to-service-item lifecycle and checks service links. Current order-transition behavior does not pass the payload to service creation, can stop processing after an item failure, and may enter an interactive dialog; this RT is expected to expose those failures.
- **Integration and data integrity:** MediaOps Plan job creation, SRM Booking, demo-data consistency, and migration export/import are not covered by this suite.
- **Other observed behavior:** profile deletion can leave the profile record behind; deleting a configuration version can leave its values behind; clearing service Category or Specification fields during edit removes those links; rejecting an order item does not transition its parent order. The tests report the behaviors they exercise, but these findings are not fixed by the RT suite.

Treat failures matching these findings as product issues to triage, not as evidence that the suite passed. Tests have been compiled but have not been executed against a live DataMiner system.

## Adding or maintaining a test

1. Add an Automation test project under `Tests\Regressions Tests\<project>\` and name its script `SLC_SM_RT_<Feature>`.
2. Use `SLC_SM_TestLibrary` for test cases and reporting. Create uniquely identified fixtures and delete them in cleanup, in reverse dependency order.
3. Add the project to `SLC-Service-Management.slnx` and add a `[TestMethod]` calling `RegressionScriptRunner.RunAndAssert("SLC_SM_RT_<Feature>")` to the matching `*RegressionTests.cs` file in `Tests\SLC_SM_RT.QAOps.IntegrationTests`. The Test Package wildcard installs new scripts automatically, but QAOps only runs scripts that have a test method. Set the script's `PublishingMode` to `QaPortalScriptOutput`.
4. Bump `<Version>` in `SLC-Service-Management-QAOps.csproj`, rebuild, and run the new `.dmtest` on QAOps. Do not treat compilation alone as runtime verification.
