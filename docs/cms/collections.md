# Collections and globals

> **Document status:** Target V1 architecture contract.

A collection declares stable key/slug, labels, fields, access policies, admin list/detail behavior, hooks, indexes, workflow/version settings, localization, endpoints, and ownership. A global uses the same field/policy/version machinery with one logical document per scope.

Registration produces collection metadata, input schemas, query contracts, application-service handlers, admin navigation entries, form manifests, DTO projections, API routes, MCP declarations, and database mapping. None of these consumers may reinterpret raw configuration independently.

Operations are explicit: create, read, list, update, delete, restore, draft, submit for review, approve, publish, unpublish, schedule, duplicate, localize, and bulk variants. Permission for one operation never implies another. Bulk commands authorize and version-check every document and report per-item outcomes without leaking denied records.
