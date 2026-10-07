# Company scaffolds

- Creation installs the current template once. A generic-service update must never replace
  company-owned apps, assets, configuration, launchers, or custom scripts.
- Keep the CompanyHQ dependency inside the project in its own Git checkout; record the selected
  branch and resolved commit. Updates refuse local edits and divergent history.
- Default development starts infrastructure and API in Docker and Web on the host. Production
  starts the complete stack in Docker. Keep consumer automation minimal and company-owned.
- Local email and object storage are development adapters, including in the local production
  smoke test. Deployments needing external delivery must register their own providers.
