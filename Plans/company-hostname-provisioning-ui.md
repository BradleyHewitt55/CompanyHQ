# Company hostname provisioning UI and CLI

## Goal

Collect a company hostname during CLI and admin company-add flows and give the customer clear DNS instructions.

## Scope

- Require or collect the hostname in both flows.
- Display instructions to point the customer-owned hostname to the server IP.
- Do not request DNS-provider credentials or create DNS records.

## Task-specific tests

- Invoke the CLI with a hostname and assert it is persisted in the company registration and route configuration.
- Submit the admin company-add flow and assert the hostname is required and persisted.
- Assert both flows display the server-IP DNS instruction.
- Assert neither flow accepts or sends Cloudflare/DNS-provider credentials or makes DNS API calls.
