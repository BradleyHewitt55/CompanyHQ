# MCP architecture

> **Document status:** Target V1 architecture contract.

Transport adapters terminate local stdio or remote authenticated streaming HTTP. They construct `McpCallContext`, validate versioned Zod input, reauthorize, apply rate/deadline controls, dispatch to a tool/resource/prompt handler, call application services, project DTO output, redact/audit, and return a bounded protocol response.

Handlers cannot import database, queue, provider, or host-control adapters. Tool discovery is filtered by effective capability. The registry is compiled from core and approved extension declarations and rejects duplicate names or unsafe capabilities.
