# Platform service tagging

## Goal

Define platform-only services and prevent them from being scaffolded or added as company services.

## Scope

- Add Mcp, CloudflareR2, and Resend to the root-service whitelist.
- Add the `platform` manifest tag and helper.
- Withhold platform service source from company scaffolds like hidden services.
- Reject platform or hidden services as scaffold parents and exclude them from add-service catalogs.
- Document root-service and platform-tag behavior.

## Task-specific tests

- Validate all three new root services.
- Attempt to scaffold below a platform root and assert a clear rejection.
- Generate a company and assert no platform service source is present.
- Assert the scaffolding API/catalog contains no platform service identifier.
