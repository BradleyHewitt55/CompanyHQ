# Script management

- Keep consumer automation minimal and self-contained; add an entry point only for a company requirement.
- Keep operational details and usage comments in the executable file, rather than duplicating them here.
- Company-owned automation must survive generic-service updates.
- Preserve existing credentials, explicit configuration overrides, and persistent data during startup.
- Limit process termination to the process recorded by this project; validate its identity first.
- Never commit environment secrets, generated runtime state, or test output.
