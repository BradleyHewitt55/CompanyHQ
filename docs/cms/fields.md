# Fields

> **Document status:** Target V1 architecture contract.

V1 field kinds are text, textarea, number, checkbox, date/time, select, radio, email, JSON, code, relationship, upload, rich text, array, group, tabs, blocks, point, and computed/read-only presentation fields. Every field has a stable path and may declare required/default/validation, localization, uniqueness/indexing, conditional visibility, admin component, read/create/update policy, DTO inclusion, and migration metadata.

Nested arrays, groups, tabs, and blocks compile into a canonical field tree. Server traversal owns defaults, validation, access, hooks, sanitization, relationship resolution, and persistence mapping. Admin rendering consumes a serializable field manifest and a component registry; it does not execute server functions.

Conditions affect presentation only. Hidden inputs are still rejected or ignored according to the command contract. Field access is enforced during query planning, mutation validation, DTO projection, relationship expansion, exports, search, logs, and agent context construction.
