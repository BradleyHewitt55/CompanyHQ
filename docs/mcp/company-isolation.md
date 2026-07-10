# MCP company isolation

> **Document status:** Confirmed Target V1 security contract.

An MCP server belongs to one installation and accepts only tokens issued for that installation and audience. The company is never selected by tool input. Registry, sessions, rate buckets, caches, resource URIs, audit, and keys are installation-specific.

Cross-installation tokens, session IDs, task IDs, resource URIs, replayed calls, or signing keys fail without revealing existence. Isolation tests run two live servers and attempt every substitution class.
