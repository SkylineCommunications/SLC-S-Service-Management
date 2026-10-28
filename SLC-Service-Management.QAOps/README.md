# SLC-Service-Management.QAOps

DataMiner **Test Package** (`.dmtest`) that verifies the Service Management solution on a clean, QAOps-provisioned DataMiner (DaaS). It is the end-to-end quality gate of the `2.0.X` CI/CD pipeline (`.github/workflows/qaops-tests.yml`).

## What a run does

1. QAOps provisions a clean Windows DataMiner for the chosen target (RC by default) and installs this `.dmtest`.
2. `TestPackageContent/TestPackagePipeline/1.TestPackageSetup.ps1` deploys the **solution's own `.dmapp`** (bundled in `TestPackageContent/Dependencies` by the `SLC-Service-Management` build) with `dataminer-package-deploy from-artifact`, so the real `SolutionInstaller` runs: Catalog prerequisites (SDM, DOM ImportExport), DOM import, web app registration, solution registration.
3. `2.TestPackageExecution.ps1` runs `SLC-Service-Management.IntegrationTests` through `Invoke-DotNetTestAndPublishResults`; every test becomes a row in QAOps.
4. The DaaS is destroyed when the run completes.

## The tests (`../SLC-Service-Management.IntegrationTests`)

`DmsTestSetup` runs once per test run on the DaaS:

- connects over SLNet with the QAOps credentials (`QAOpsDataMinerUser` / `QAOpsDataMinerPassword` encrypted keys);
- runs the `SLC_SM_Setup_UDAPI` script (the solution installer does not install the User-Defined API) and creates a dedicated API token that is granted on every `service-management/v1/*` definition — revoked again at the end;
- opens a DataMiner web session (`Json.asmx/ConnectApp`) and launches headless Chromium (Playwright downloads it on first use).

| Class | Port of | Covers |
|---|---|---|
| `ServiceManagementApiTests` | `tests/k6/udapi-smoke.js` | list/overview routes return JSON, validation (400/404), token authentication (401) |
| `ServiceManagementAppTests` | `Service-Management-App/e2e-live/app.live.spec.ts` | auth redirect, Orders page, workspace navigation, overview loads live data |

All tests carry `[TestCategory("IntegrationTest")]`; they **cannot run locally** (no QAOps keys) and are skipped by the Skyline unit-test workflow.

## Build

```powershell
dotnet build SLC-Service-Management.QAOps -c Release
```

This builds the test project, the solution package (needs the Catalog organization key as the `skyline:sdk:dataminertoken` user secret of `SLC-Service-Management`, or the `skyline__sdk__dataminertoken` environment variable) and produces `bin/Release/net48/DataMinerBuild/SLC-Service-Management.QAOps.<Version>.dmtest`. CI passes `-p:QAOpsPackageVersion=1.0.<run>` so every run submits a fresh version.

`PackageContent/ProjectReferences.xml` deliberately includes **no** solution project: everything ships inside the bundled `.dmapp`.

## Run manually

```powershell
dotnet tool install Skyline.DataMiner.qaops --global --add-source https://api.nuget.org/v3/index.json
dataminer-qaops test-run-and-wait -t 01KTRNHRHWQJ9SMESYJAFP8SE9 -c 01KTRNMVWEZX5EAW3GRDB744G4 `
  --token "<QAOps token>" --test-packages "<path>\SLC-Service-Management.QAOps.<Version>.dmtest" `
  -rf "<path>\result.json" -tags "<your name> SM RC"
```

Configuration IDs: RC `01KTRNMVWEZX5EAW3GRDB744G4`, Main `01KTRNTY3WW3JANCQ7WPWBEAVM`, Feature `01KTRNSARVCH5DG50QM0RW7GYA`. Tokens are created on the QAOps *Tokens* page.

## GitHub configuration used by the pipeline

| Name | Kind | Used by |
|---|---|---|
| `QAOPS_TOKEN` | repository secret | `qaops-tests.yml` – QAOps token with the `Run_Scoped_RC` scope and enough uses (one per push) |
| `DATAMINER_TOKEN` | repository secret (exists) | build-time Catalog downloads; `from-catalog` deploy on release tags |
| `DAAS_SYSTEM_TOKEN` | `dataminer` environment secret | `deploy-daas.yml` – System Key of the persistent DaaS (volatile upload + deploy on `2.0.X` pushes) |
| `DAAS_AGENT_ID` | `dataminer` environment variable | `deploy-daas.yml` – agent destination ID of the persistent DaaS (last GUID in its admin.dataminer.services URL) |
| `DMA_URL` | `dataminer` environment variable (exists) | environment URL shown on the deployment; base URL for the post-deploy k6 smoke — must now point at the DaaS (`https://<name>.on.dataminer.services`) |
| `SM_API_URL` | `dataminer` environment variable (optional) | k6 – overrides the API base (default `<DMA_URL>/api/custom`) |
| `SM_API_KEY` | `dataminer` environment secret | k6 – User-Defined API token on the DaaS that is allowed on the `service-management/v1/*` routes |
