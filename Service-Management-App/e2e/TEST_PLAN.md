# Service Management App E2E Test Plan

## Scope
Chromium tests run against the production build at the DataMiner public-app base path. No live system or credentials are used: DataMiner authentication is mocked at the documented `IsConnectionAlive` boundary, and the Orders list is mocked at the GQI query-session boundary (`/API/v1/Internal.asmx/OpenQuerySession`, `GetNextQuerySessionPage`, `CloseQuerySession`) for the `Get_ServiceOrders` ad hoc data source.

The GQI mock validates the request (connection from the `DMAConnection` cookie, `Custom data` query with the `SLC_SM_GQIDS_Get Service Orders` data source descriptor), returns columns in a different order than the C# data source to prove name-based mapping, and serves the sample orders over two pages to exercise paging.

## Scenarios
| ID | Scenario | Expected result | Spec |
| --- | --- | --- | --- |
| A1 | Valid session loads app | Orders renders with heading and service order rows | `app.spec.ts` |
| A2 | Primary navigation switches pages | Services, Catalog, and Settings render | `app.spec.ts` |
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
| D1 | Open order details | Clicking an order opens a details dialog (deep link `#orders/<id>`) with the order fields; Escape and Close dismiss it | `app.spec.ts` |
| D2 | Service order items | Details load `Get_ServiceOrderItems` with the `DOM ID` argument; items show action, category, specification, service and times; "No Start/End Time" placeholders are hidden; an empty state is shown when there are no items | `app.spec.ts` |
| D3 | Deep link | Opening `#orders/<id>` directly shows that order's details | `app.spec.ts` |
| D4 | Item load failure | An order items error shows an alert with Retry, which reloads the items | `app.spec.ts` |
| O1 | Workspace overview | The Service Management brand and empty route open a dashboard with order, service-inventory, and catalog metrics plus recent data panels | Manual/live DMA |
| S1 | Service inventory | Services loads the no-argument `Get_Services` GQI source and shows Service ID, name, state, start/end, specification, category, category type, and action columns | Manual/live DMA |
| S2 | Service details | Selecting a service opens a full-width Service Info dialog with lifecycle, service fields, related-order empty state, and service items loaded from `Get_ServiceItemsMultipleSections` | Manual/live DMA |
| C1 | Service catalog | Catalog loads service specifications through the catalog GQI source and shows specification and service-item counts | Manual/live DMA |
| W1 | Order write actions | New, edit, and delete actions call the corresponding Service Management Automation scripts through `ExecuteAutomationScriptWithOutput`; successful mutations refresh the list or item section | Manual/live DMA |
| W2 | Item write actions | Add, edit, and delete controls are available in the order drawer; delete actions require confirmation | Manual/live DMA |
| W3 | Service initialization | The Initialize service control is visible but disabled until the future service-initialization script contract is supplied | Manual/live DMA |

## Exclusions
Realtime GQI updates remain out of scope. The Services page uses the new no-argument `SLC_SM_GQIDS_Get Services` source; the Catalog page uses `SLC_SM_GQIDS_Get Service Specifications`. The write controls reuse the deployed Automation scripts, while the dev mock simulates their completion for local preview; a live DMA check is required to verify source deployment, permissions, and interactive editor behavior. Service initialization remains disabled until its script is supplied.

## Live preview
`npm run dev` uses live same-origin DataMiner requests. For local live-DMA development, set `VITE_DATAMINER_ORIGIN` to the DMA base URL so Vite proxies `/API` and `/auth`. `npm run dev:mock` explicitly enables the sample data from `dev/mockDataMiner.js` (dev server only, never part of `vite build`).

## Commands
```powershell
npm run build
npx playwright test
```
