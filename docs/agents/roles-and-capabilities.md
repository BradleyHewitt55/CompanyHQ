# Agent roles and capabilities

> **Document status:** Confirmed Target V1 security contract.

An agent profile declares provider/instance, purpose, allowed task types, CMS actions, MCP tools, projects/collections, filesystem roots, network hosts, model/budget limits, approval rules, and memory policy. It grants no standing human role.

Effective capability is the non-additive intersection of delegator, profile, task, resource, worktree, tool, action, environment, approval, expiry, rate and cost constraints. Capabilities are short-lived, audience-bound, revocable, and rechecked at tool execution.
