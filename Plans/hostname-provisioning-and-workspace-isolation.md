# Hostname provisioning flows and developer workspace isolation

Wave 4. Two parallel lanes; each agent owns exactly one task section below.

## Task: Company hostname provisioning UI and CLI

### Goal

Collect a company hostname during CLI and admin company-add flows and give the customer clear DNS instructions. Needs wave-3 routing to exist.

### Scope

- [ ] Require or collect the hostname in both flows.
- [ ] Display instructions to point the customer-owned hostname to the server IP.
- [ ] Do not request DNS-provider credentials or create DNS records.

### Task-specific tests

- [ ] Invoke the CLI with a hostname and assert it is persisted in the company registration and route configuration.
- [ ] Submit the admin company-add flow and assert the hostname is required and persisted.
- [ ] Assert both flows display the server-IP DNS instruction.
- [ ] Assert neither flow accepts or sends Cloudflare/DNS-provider credentials or makes DNS API calls.

## Task: Company developer workspace isolation

### Goal

Ensure separate company workspaces are independently editable without central-application access or cross-company file/data access. Needs per-company credentials from wave 2.

### Scope

- [ ] Support separate developer workspaces rooted at each company directory.
- [ ] Keep company services, pages, files, and database credentials isolated.
- [ ] Do not require developers to query a central CompanyHQ application to add or modify a company service.

### Task-specific tests

- [ ] Open separate Acme and Beta developer workspaces.
- [ ] Add or modify a service in each workspace and assert only the owning company reflects the change.
- [ ] Assert neither workspace can read the other company's files or deployment configuration.
- [ ] Assert neither developer workflow needs a central CompanyHQ application query.
