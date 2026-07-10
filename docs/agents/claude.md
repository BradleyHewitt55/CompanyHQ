# Claude adapter

> **Document status:** Target V1 adapter contract; implementation not verified.

The Claude adapter implements the same canonical session, streaming, approval, user-input, usage, cancellation, and resume contracts. Native tool permission callbacks are mapped to CompanyHQ approval/tool requests; denial or task cancellation resolves pending callbacks safely.

Provider session IDs are opaque adapter state. The adapter cannot translate a provider permission mode into broader CompanyHQ access.
