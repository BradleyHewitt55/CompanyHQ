# Codex adapter

> **Document status:** Target V1 adapter contract; implementation not verified.

The Codex adapter maps CompanyHQ task/run IDs to provider sessions, translates streaming items, plans, approvals, user input, tool calls, usage, cancellation, and resume identifiers into canonical events. It exposes only the installation MCP server and assigned worktree.

Provider-specific sandbox and approval modes can only narrow CompanyHQ policy. A provider “full access” setting is never accepted as authorization. Compatibility tests use a mock provider and recorded schema fixtures without secrets.
