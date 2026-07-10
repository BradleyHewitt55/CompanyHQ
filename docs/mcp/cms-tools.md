# MCP CMS tools

> **Document status:** Target V1 tool contract.

CMS tools cover schema/collection discovery, authorized query/get, draft creation, validated patch/update, submit review, approval request, publish/unpublish/schedule requests, revision history/restore proposal, and media lifecycle. Every write becomes a typed CMS command with expected version and idempotency key.

Publishing tools do not imply approval; workflow obligations may return `approval_required`. Agents cannot forge actor fields, write status directly, bypass validation/hooks, or alter revision/audit records.
