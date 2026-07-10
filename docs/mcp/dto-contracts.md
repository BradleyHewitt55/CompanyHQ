# MCP DTO contracts

> **Document status:** Confirmed Target V1 contract.

MCP uses registered Zod DTOs, never domain/database models. Inputs reject unknown fields for mutations. Outputs are allowlisted, policy-projected, depth/count/byte bounded, and stripped of secrets, hidden fields, internal paths, provider credentials, and raw errors.

Breaking contracts create a new tool/resource major or versioned name. The registry publishes compatibility metadata; unsupported versions fail explicitly.
