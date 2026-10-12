# Company scaffolds

- Creation installs the current template once. A generic-service update must never replace
  company-owned apps, assets, configuration, launchers, or custom scripts.
- Consume CompanyHQ NuGet packages at one release pin in `Directory.Build.props`; never embed an
  HQ source checkout. Updates change the pin only, and published release versions are immutable.
- Default development starts infrastructure and API in Docker and Web on the host. Production
  starts the complete stack in Docker. Keep consumer automation minimal and company-owned.
- Local email and object storage are development adapters, including in the local production
  smoke test. Deployments needing external delivery must register their own providers.
