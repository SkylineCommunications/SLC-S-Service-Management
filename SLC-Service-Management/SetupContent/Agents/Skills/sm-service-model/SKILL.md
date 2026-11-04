---
name: sm-service-model
description: Use when the user asks what a service, service item, service specification, service order, order item, category, configuration, profile or relationship is in Service Management, or how they relate. Explains the entity model and which sm-* tool reads each entity.
---

# Service Management entity model

| Entity | What it is | ID format | Read with |
|---|---|---|---|
| **Service specification** | Catalog design of a service type: service items, relationships, configuration parameters and profiles. | UUID | `sm-specifications-list`, `sm-specification-get` |
| **Service** | A service instance in the inventory, usually created from a specification. Has a lifecycle status, category, organization, items and configuration. | `SERVICE-<number>` | `sm-services-list`, `sm-service-get` |
| **Service item** | A building block of a service (e.g. a workflow/job, a booking or a linked resource). | part of the service | `sm-service-get` with `full=true` |
| **Service item relationship** | Connection between service items (topology). | part of service/specification | `sm-service-get` `full=true`, `sm-specification-get` |
| **Configuration version** | A versioned set of configuration values of a service; one is active. | UUID | `sm-service-configuration-versions` |
| **Profile** | Named group of configuration values based on a profile definition. | UUID | `sm-service-get` `full=true` |
| **Service category** | Classification of services (e.g. Internet, Video). | UUID | `sm-categories-list` |
| **Service order** | A customer/OSS request to add, modify or delete services. | UUID | `sm-orders-list`, `sm-order-get` |
| **Service order item** | One line of an order with an action (`add`, `modify`, `delete`, `noChange`) on one service or specification. | UUID | `sm-order-items` |

## Typical flow
Specification (catalog) → service order with items → items are processed → services are created or changed in the inventory → configuration versions track changes.

## Name → ID
Users talk in names. Resolve names with the list tools (`name` filter), then use IDs for detail calls.
