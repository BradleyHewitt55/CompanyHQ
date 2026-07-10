# Logging

> **Document status:** Target V1 operational contract.

Logs are structured JSON with timestamp, severity, service/version, environment, installation, correlation/trace/span, event code, and safe attributes. A central redaction library removes secrets, credentials, cookies, auth headers, prompt-sensitive values, hidden DTO fields, connection strings, and unsafe paths.

Never log full request bodies, model prompts, tool output, document snapshots, email content, or command environments by default. Debug elevation is time-bound, owner-approved, auditable, and still redacted.
