# Local MCP connections

> **Document status:** Target V1 security contract.

Local means inside the installation's controlled worker boundary, not trusted-by-location. Use stdio or a Unix socket/Windows named pipe with OS permissions plus short-lived task authentication. The server supplies a minimal environment and validates peer/session identity.

Local clients receive no ambient filesystem, database, Redis, Docker, or secret authority. Disconnect cancels or reconciles the bound run according to task policy.
