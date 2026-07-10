# DTO contracts

> **Document status:** Confirmed Target V1 contract.

Database/domain models are never returned directly. Each surface registers Zod DTOs for admin list, admin detail, public client, preview, API major, MCP tool, audit summary, and agent context. DTOs use allowlists and may only narrow visibility relative to policy.

DTO projection occurs inside application query services after authorized filters are applied. Relationships recursively project their own DTO with depth and count limits. Unknown fields are stripped; hidden and secret fields cannot appear in errors, logs, cache values, exports, or telemetry.

Breaking public/API/MCP changes create a new major. Internal admin DTOs may evolve with the deployed application but configuration revision mismatches are rejected rather than guessed.
