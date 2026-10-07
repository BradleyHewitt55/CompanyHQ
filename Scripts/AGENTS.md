# Script management

- Keep operational usage and implementation details in executable scripts, not in this document.
- Keep consumer lifecycle automation minimal and preserve company-owned automation during dependency updates.
- Propagate creation defaults through the scaffold; do not replace existing companies' scripts on service updates.
- Development uses host web apps with Docker backing services; production runs configured apps in Docker.
- Never delete persistent volumes or overwrite credentials as part of ordinary startup or verification.
- Run the minimum relevant checks against isolated resources; do not ship test runners or reports in consumers.
