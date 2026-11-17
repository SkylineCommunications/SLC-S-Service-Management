---
name: sm-order-tracking
description: Use when the user asks about open, late, held or failed service orders, the content of an order, or which services an order creates or changes.
---

# Tracking service orders

## Recipes
- **Open orders:** `sm-orders-list` with `status` = `new,acknowledged,inProgress,held,pending`, `limit` 25, `fields` = `id,name,status,priority,requestedStartDate`. Summarize counts per status, then list.
- **Late orders:** add `requestedStartDate.lte` = now (Unix ms) to the open-orders query.
- **Order by customer reference:** `externalId`.
- **Order content:** `sm-order-items` for the order ID; show action, status and the linked specification/service per item.
- **Why an order is stuck:** `sm-order-get` + `sm-order-transitions` + `sm-order-items`; look for items in `failed`/`held` and report allowed next statuses.
- **High priority:** `priority` = `high`.

## Answering
- Operators: a short table (Name, Status, Priority, Requested start) and one sentence of advice.
- Engineers: add order/item IDs, item actions and linked service IDs.
