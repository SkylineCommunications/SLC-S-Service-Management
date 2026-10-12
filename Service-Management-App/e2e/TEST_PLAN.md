# Service Management App E2E Test Plan

## Scope
Chromium tests run against the production build at the DataMiner public-app base path. No live system or credentials are used: DataMiner authentication is mocked at the documented `IsConnectionAlive` boundary, and the Orders list is mocked at the GQI query-session boundary (`/API/v1/Internal.asmx/OpenQuerySession`, `GetNextQuerySessionPage`, `CloseQuerySession`) for the `Get_ServiceOrders` ad hoc data source.

The GQI mock validates the request (connection from the `DMAConnection` cookie, `Custom data` query with the deployed data source descriptor), returns columns in a different order than the C# data source to prove name-based mapping, and serves the sample orders over two pages to exercise paging. The catalog configuration scenario also mocks `Get Service Specifications`, `Get Service Items`, and the typed `Get Service Configurations` request.

## Scenarios
| ID | Scenario | Expected result | Spec |
| --- | --- | --- | --- |
| A1 | Valid session loads app | Orders renders with heading and service order rows | `app.spec.ts` |
| A2 | Primary navigation switches pages | Services, Catalog, and Settings render | `app.spec.ts` |
| A4 | Order details no longer expose the test IAS launcher | The order details drawer contains the normal Edit and Delete actions without the removed "Launch test IAS" button | `app.spec.ts` |
| A9 | Service item launches its script | Each Service Inventory service item has a launch action (disabled without a service item script) that runs `Launch Interactive Subscript` interactively with the service `DOM ID` and `Item Label`; the item's booking wizard dialog is rendered, Create Booking completes the script, a success notice is shown, and the service items reload with the new implementation state | `ias.spec.ts` |
| A10 | Service item script error dialog | The script's `ShowErrorDialog` (title "Error", message, collapsible details, Close) is rendered; Close ends the script without a success notice and the item can be launched again | `ias.spec.ts` |
| G1 | Orders list from GQI | Rows show name, External ID (or short order ID), organization, priority, and state badge; all pages are read and the session is closed | `app.spec.ts` |
| G2 | Open orders metric | Metric counts orders not Completed, Cancelled, Rejected, or Failed; no fake trend is shown | `app.spec.ts` |
| G3 | Refresh | Refresh re-runs the query and closes the new session | `app.spec.ts` |
| G4 | GQI query fails | Error state shows the server message; Retry loads the orders | `app.spec.ts` |
| G5 | No service orders | Empty state is shown and the metric is 0 | `app.spec.ts` |
| G6 | Connection expired during GQI call | `NoConnectionWebApiException` redirects to DataMiner `/auth/` | `app.spec.ts` |
| T1 | Theme menu offers all modes | System, Light, and Dark are visible and selectable | `app.spec.ts` |
| T2 | Explicit theme persists | Dark theme changes computed state and survives reload | `app.spec.ts` |
| T3 | System theme follows OS preference | System mode reflects dark and light preferences | `app.spec.ts` |
| R1 | Responsive layout | Mobile content, including order rows, stays within the viewport | `app.spec.ts` |
| A3 | Missing session | App redirects to DataMiner `/auth/` | `auth.spec.ts` |
| D1 | Open order details | Clicking an order opens a details dialog (deep link `#orders/<id>`) with the order fields, state transition controls, and no DOM instance ID; Escape and Close dismiss it | `app.spec.ts` |
| D2 | Service order items | Details load `Get_ServiceOrderItems` with the `DOM ID` argument; items show action, category, specification, service and times; "No Start/End Time" placeholders are hidden; an empty state is shown when there are no items | `app.spec.ts` |
| D3 | Deep link | Opening `#orders/<id>` directly shows that order's details | `app.spec.ts` |
| D4 | Item load failure | An order items error shows an alert with Retry, which reloads the items | `app.spec.ts` |
| O1 | Workspace overview | The Service Management brand and empty route open a dashboard with order, service-inventory, and catalog metrics plus recent data panels | Manual/live DMA |
| O2 | Newest-first order lists | Recent Orders and the Orders table place the records with the newest available Created At timestamp first, falling back to Requested Start only when Created At is unavailable | Manual/live DMA |
| S1 | Service inventory | Services loads the no-argument `Get_Services` GQI source and shows compact, readable Service ID, name, state, start/end, specification, category, category type, and action columns without page-level horizontal overflow | Manual/live DMA |
| P1 | Table pagination | Orders, Services, and Catalog provide Previous/Next controls and a selectable 10, 25, 50, or 100 rows per page; filtering and refresh reset or clamp the current page | Manual/live DMA |
| S2 | Service details | Selecting a service opens a full-width Service Info dialog with lifecycle, service fields, related-order empty state, and service items loaded from `Get_ServiceItemsMultipleSections` | Manual/live DMA |
| C1 | Service catalog | Catalog loads service specifications through the catalog GQI source and shows specification and service-item counts | Manual/live DMA |
| W1 | Order write actions | New, edit, and delete actions call the corresponding Service Management Automation scripts through `ExecuteAutomationScriptWithOutput`; successful mutations refresh the list or item section. A nonresponsive script surfaces a timeout error instead of leaving the form on Saving indefinitely | Manual/live DMA |
| W2 | Item write actions | Add, edit, and delete controls are available in the order drawer; delete actions require confirmation | Manual/live DMA |
| W3 | Service initialization | An unlinked order item cannot initialize a Service Inventory service until its service specification has a configured version; the UI shows an explanatory note and does not run the initialization script | `app.spec.ts` / Manual/live DMA |
| W4 | Service order item CRUD | The order drawer opens a noninteractive Add/Edit form for order items, sends the payload-based Automation contract, refreshes the item list, and verifies deletes | Manual/live DMA |
| W5 | Service and specification item CRUD | Service detail and specification detail panels expose Add/Edit/Delete item actions through the shared payload-based item script and refresh the item list after mutations | Manual/live DMA |
| W6 | Service order item configurations | The order-item detail Configurations tab shows only the configuration-parameter editor supported by `ServiceOrderItemServiceInfo.Configurations`, without synthetic version metadata or profile controls; it supports add/edit/delete parameter values and reloads saved values through the noninteractive configuration Automation contract | Manual/live DMA |
| W7 | Nested profile configuration navigation | The root profile collection is shown as the single expandable list; opening a nested profile provides breadcrumbs without repeated root-level lists or level labels. Existing nested profile trees, including children of root profiles with no direct parameters, remain viewable through every stored level. Reusable profile parameters are read-only and have no delete action. Adding nested profile definitions remains limited to level 3. | Manual/live DMA |
| W8 | Service operations CRUD | Services exposes Add/Edit/Delete operation controls; each mutation sends `SLC_SM_IAS_Manage Service Scripts` with the action, service ID, script name, description, original description, and payload, then reloads the GQI operation list | `app.spec.ts` |
| W10 | Order organization selection | New and edit service-order forms fetch the People & Organizations data source and render organization names in a dropdown, while preserving an existing organization during edit | `app.spec.ts` |
| S3 | Service inventory reference data | Services provides a Manage categories entry point; service forms use fetched category, organization, and monitoring-service lists; clicking the far-left service icon opens logo assignment; the Catalog Categories subpage supports category creation with type and logo/icon metadata | `app.spec.ts` |
| S4 | Related service orders | Service details request `GetRelatedServiceOrders` with the selected service ID and show only the matching service-order relationships | `app.spec.ts` |
| S5 | Service inventory form validation | Service creation shows the generated Service ID, rejects duplicate IDs and names, rejects past start times and end times before the start, requires a linked service when linking is enabled, and keeps Create disabled until the form is valid; service-item creation rejects duplicate labels while allowing the IAS placeholder behavior | `app.spec.ts` |
| C2 | Specification configuration editor | Catalog specification configuration uses the specification owner type in the configuration GQI request; standalone parameters and profiles are independently collapsible, selecting a profile definition with compatible reusable profiles reveals a secondary root-level reusable-profile dropdown, adding a profile expands it and exposes reusable nested-profile choices, profile controls share a size, profile names are user-editable and not duplicated, nested navigation returns to the root expandable list, and reusable profile parameters have no delete action | `app.spec.ts` |
| C4 | Catalog form validation | Specification creation rejects duplicate names while allowing an empty label to use the generated placeholder; category creation requires a name and type, rejects duplicate names, and keeps Create disabled until valid | `app.spec.ts` |
| C5 | Discrete configuration parameters | Configuration parameters whose type is Discrete render a dropdown populated with the possible values for standalone and profile parameters, preserve the current value, and submit the selected value through the existing configuration payload | `app.spec.ts` |
| C6 | Configuration version lifecycle controls | The active version shows a dimmed disabled Active button and cannot be deleted; an inactive version shows an enabled Activate button and an enabled delete action | `app.spec.ts` |
| C7 | New configuration version activation | A new version is shown as inactive until it is explicitly activated; an unsaved new version cannot be deleted | `app.spec.ts` |
| C8 | Configuration value detail columns | Show Value Details reveals the legacy configuration columns for labels, links, units, ranges, step size, decimals, and discrete values; Hide Value Details collapses them again | `app.spec.ts` |

## Exclusions
The IAS WebSocket mock reproduces the `SLC_SM_IAS_Add Service Order` dialog from its C# view/presenter (DestVars are synthetic), and a simplified booking wizard for the subscript started by `Launch Interactive Subscript` (the real dialog depends on each service item's script, e.g. an SRM Booking Manager wizard). The exact wire field names (`Layout.Rows[].Cells[].Components[]`, `Options[{Key, Value}]`, check-box-list `;` separator, 7-digit ISO dates) follow other Skyline headless IAS clients, so a live DMA check is required to confirm the dialog round trip, the `Completed` result text, and permissions. The dev mock (`npm run dev:mock`) has no WebSocket endpoint, so the IAS dialog reports a connection error there.

Realtime GQI updates remain out of scope. The Services page uses the new no-argument `SLC_SM_GQIDS_Get Services` source; the Catalog page uses `SLC_SM_GQIDS_Get Service Specifications`. The write controls reuse the deployed Automation scripts, while the dev mock simulates their completion for local preview; a live DMA check is required to verify source deployment, permissions, and interactive editor behavior. Service initialization calls the existing `SLC_SM_Create Service Inventory Item` script with its `AddItemSilent` action; verify the API-safe path, permissions, and service/order-item link on a live DMA after deploying the script changes.

## Live preview
`npm run dev` uses live same-origin DataMiner requests. For local live-DMA development, set `VITE_DATAMINER_ORIGIN` to the DMA base URL so Vite proxies `/API` (including the `/API/v1/WebSocket.ashx` WebSocket used by the IAS dialog) and `/auth`. `npm run dev:mock` explicitly enables the sample data from `dev/mockDataMiner.js` (dev server only, never part of `vite build`).

## Commands
```powershell
npm run build
npx playwright test
```
