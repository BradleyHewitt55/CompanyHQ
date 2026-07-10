# API, DTO, and MCP versioning

CompanyHQ version-controls public and internal API contracts. A route, DTO, MCP mapping, and service declaration must identify the same supported contract version.

## Route convention

Service APIs use a major version in the route:

~~~text
/api/v<major>/services/<service-key>/<resource-key>
~~~

The route registry records request DTO version, response DTO version, authorization policy, rate/abuse category, and matching MCP tool/resource/prompt declaration.

## Compatibility rules

| Change | Treatment |
| --- | --- |
| Add an optional field that policy allows | Additive. Document and test affected DTO projections. |
| Add a required field, remove/rename a field, change semantics, or tighten a successful response shape | Breaking. Publish a new major API/DTO contract. |
| Correct a security leak | Apply immediately; document the behavior change and notify affected consumers. |
| Change a policy result | Version when client-visible contract changes; always audit and test denial paths. |

No unversioned externally supported API is created. A client, admin override, project, agent, integration, and test must request or bind an explicit supported version.

## DTO versions

Models are internal. DTO versions describe what a caller may receive, and field policy is applied inside every version. A new model attribute does not automatically appear in an existing DTO or API version.

When a field is added through Settings, the Change selects affected DTO versions and whether the field is admin-only, client-only, both, or internal. It includes compatibility and regression tests before release.

## MCP parity

Every API version declares a matching MCP mapping. Tool names remain stable where possible, while their metadata records the API and DTO versions they use. A tool cannot silently start returning a broader DTO or switch API majors without an explicit contract update.

## Current support policy

CompanyHQ has no API-version deprecation or removal lifecycle. A breaking change creates a new major route, DTO family, and MCP mapping while every earlier published major remains available and tested. Removing a published version is outside the current product contract.

Security fixes, authorization corrections, tenant-isolation fixes, abuse controls, and secret redaction apply to every major immediately. Continued support is not permission to preserve insecure behavior. A service cannot be disabled or removed while an enabled consumer depends on one of its API majors; dependency resolution and data-retention rules apply first.

The route registry keeps ownership, consumer inventory, compatibility tests, DTO projection tests, MCP parity, operational health, and documentation for every published major. A major is published only when that support contract can be maintained.
