---
name: sm-lifecycles
description: Use when the user asks about the status of a service, service order or order item, what a status means, or which status can come next. Explains how to read lifecycle state with the read-only transition tools.
---

# Service Management lifecycles

The allowed transitions are defined by DOM behaviors in the solution. **Do not memorize or guess them**: always read them live.

- Service: `sm-service-transitions` → current status + `allowedStatuses`.
- Service order: `sm-order-transitions` → current status + allowed next statuses.
- Order item: read its `status` via `sm-order-items`.

## Service order statuses (API names)
| Status | Meaning for operators |
|---|---|
| `new` | Received, not yet reviewed. |
| `acknowledged` | Accepted for processing. |
| `inProgress` | Being processed. |
| `held` | Paused, waiting for something (e.g. customer input). |
| `pending` | Waiting on an external dependency. |
| `assessCancellation` / `pendingCancellation` | Cancellation requested / being executed. |
| `completed` | All items processed successfully. |
| `partiallyFailed` | Some items failed. |
| `failed` | Processing failed. |
| `rejected` | Not accepted. |
| `cancelled` | Cancelled. |

Final states: `completed`, `partiallyFailed`, `failed`, `rejected`, `cancelled`. Only orders in `new`, `acknowledged`, `pending` or `held` can still be edited (in the app).

## Service statuses
Service statuses are returned in lowercase (e.g. `new`, `active`, `retired`). Use `sm-service-transitions` to explain what can happen next.

## Read-only reminder
You never change a status. If asked, explain which status is allowed next (from the transition tool) and tell the user to perform the transition in the Service Management app.
