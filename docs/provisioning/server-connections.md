# Server connections

> **Document status:** Target V1 security contract.

The CLI uses SSH host-key verification, an agent or explicitly referenced local identity, least-privileged remote commands, timeouts, and a connection transcript stripped of secrets. First use displays the fingerprint for explicit trust; a changed key blocks operation.

Remote privilege preflight reports required actions before elevation. Private keys are never uploaded. Shell command construction uses fixed executables and encoded/structured arguments; company names and paths are validated, normalized, and never interpolated into a shell string.
