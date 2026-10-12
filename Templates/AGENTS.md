# Company scaffolds

- Creation installs the current template once. A generic-service update must never replace
  company-owned code, configuration, launchers, or custom scripts.
- The starter is a clean headless API that references generic service packages (and their Contracts)
  directly at one release pin in `Directory.Build.props`; never embed an HQ source checkout.
  Updates change the pin only, and published release versions are immutable.
- The template never references `CompanyHQ.Api` and never inherits an HQ host, `Api.*` helper, demo
  or local provider, test project, website, assets, database or authentication. Those belong to HQ or
  to the company that chooses them; do not add them back to make the starter look fuller.
- Default development starts the API in Docker; a company-owned web app is optional and runs on the
  host only when the company configures it. Production starts the configured services in Docker.
  Keep consumer automation minimal and company-owned.
- Do not copy `Apps/Web`, `Assets` or tests into a company, or ship test runners and reports.
