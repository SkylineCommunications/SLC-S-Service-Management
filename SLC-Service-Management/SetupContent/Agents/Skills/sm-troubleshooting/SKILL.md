---
name: sm-troubleshooting
description: Use when a tool returns an error or empty result, when a user cannot find a service/order/specification, or when explaining why something cannot be done (deleted, transitioned, edited).
---

# Troubleshooting

## Tool status codes
| Code | Meaning | What to do |
|---|---|---|
| 200 | OK | Use the result. An empty array means nothing matched; say so and suggest a broader filter. |
| 400 | Invalid query | Read the message, fix the parameter (e.g. `parameter` must be `parameter:value`; status names must be API names) and retry once. |
| 404 | Not found | Check the ID format: services are `SERVICE-<number>`, orders/items/specifications/categories are UUIDs. Resolve by name with a list tool. |
| 500 | Server error | Report it; do not retry more than once. Suggest checking the Automation log of `SLC_SM_UDAPI_ServiceManagement`. |

## Common questions
- **"Why can't I delete this specification?"** Deletion is blocked while services or open order items reference it. Check with `sm-services-list` (`serviceSpecificationId`) and `sm-orders-list` + `sm-order-items`.
- **"Why can't I edit this order?"** Only orders in `new`, `acknowledged`, `pending` or `held` are editable.
- **"Service not found by name."** Try a shorter `name` filter, or list by category or organization.

## Never
Never suggest or perform write calls. Explain the cause and point the user to the Service Management app.
