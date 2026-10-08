# Script management

- Keep consumer automation minimal and self-contained; add an entry point only for a company requirement.
- Keep operational details and usage comments in the executable file, rather than duplicating them here.
- Company-owned automation must survive generic-service updates.
- Configuration comes from `.env.dev` in development and `.env` in production; scripts never generate secrets or write local state files.
- Development and production are separate Compose projects with separate data; never point one at the other's volumes.
- Preserve existing credentials and persistent data during startup.
- A start script may write a migration but must stop before the API applies one that deletes data.
- Never commit environment secrets or test output.
