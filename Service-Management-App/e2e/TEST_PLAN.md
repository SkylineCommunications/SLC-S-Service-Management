# Service Management App E2E Test Plan

## Scope
Chromium tests run against the production build at the DataMiner public-app base path. DataMiner authentication is mocked at the documented `IsConnectionAlive` boundary; no live system or credentials are used.

## Scenarios
| ID | Scenario | Expected result | Spec |
| --- | --- | --- | --- |
| A1 | Valid session loads app | Orders renders with heading and activity content | `app.spec.ts` |
| A2 | Primary navigation switches pages | Services, Catalog, and Settings render | `app.spec.ts` |
| T1 | Theme menu offers all modes | System, Light, and Dark are visible and selectable | `app.spec.ts` |
| T2 | Explicit theme persists | Dark theme changes computed state and survives reload | `app.spec.ts` |
| T3 | System theme follows OS preference | System mode reflects dark and light preferences | `app.spec.ts` |
| R1 | Responsive layout | Mobile content stays within the viewport | `app.spec.ts` |
| A3 | Missing session | App redirects to DataMiner `/auth/` | `auth.spec.ts` |

## Exclusions
The request does not define DataMiner queries or write operations, so page content is local placeholder content. GQI, realtime updates, filters, and write workflows are out of scope until their contracts are specified.

## Commands
```powershell
npm run build
npx playwright test
```