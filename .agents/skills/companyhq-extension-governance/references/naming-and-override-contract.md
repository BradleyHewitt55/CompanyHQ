# Naming and override contract

Read this before changing an Explorer-visible page, detail variant, component handoff, service, model/DTO binding, route/API, or MCP declaration.

## Keys and references

- Keys are lowercase kebab-case.
- A page family uses a dot-separated reference. Its detail variant adds the literal (slug).
- Use a stable component reference for visual work. CSS classes are diagnostic only.
- A same-name override is valid only when its path, entry name, metadata reference, operation, API version, and DTO all match the registered contract.
- Never reuse a retired key.

## Base admin page families

| Reference | Tenant list path | Tenant detail path |
| --- | --- | --- |
| admin.dashboard | Admin/Pages/dashboard/dashboard.<ui-extension> | Not applicable unless registered. |
| admin.pipeline | Admin/Pages/pipeline/pipeline.<ui-extension> | Not applicable unless registered. |
| admin.users | Admin/Pages/users/users.<ui-extension> | Admin/Pages/users/(slug)/users.<ui-extension> |
| admin.gallery | Admin/Pages/gallery/gallery.<ui-extension> | Admin/Pages/gallery/(slug)/gallery.<ui-extension> when registered. |
| admin.keys | Admin/Pages/keys/keys.<ui-extension> | Not applicable unless registered. |
| admin.skills | Admin/Pages/skills/skills.<ui-extension> | Admin/Pages/skills/(slug)/skills.<ui-extension> when registered. |
| admin.agents | Admin/Pages/agents/agents.<ui-extension> | Admin/Pages/agents/(slug)/agents.<ui-extension> when registered. |
| admin.services | Admin/Pages/services/services.<ui-extension> | Admin/Pages/services/(slug)/services.<ui-extension> |
| admin.settings | Admin/Pages/settings/settings.<ui-extension> | Not applicable unless registered. |

The list/main reference is the parent. The detail reference is, for example, admin.users.(slug). The (slug) template segment is lookup input, never authorization proof.

## Service layout

~~~text
Admin/Services/<service-key>/
  Pages/<page-key>/<page-key>.<ui-extension>
  Pages/<page-key>/(slug)/<page-key>.<ui-extension>
  Settings/<settings-key>/<settings-key>.<ui-extension>
  Service/
  Models/
~~~

Service page references are:

~~~text
service.<service-key>.pages.<page-key>
service.<service-key>.pages.<page-key>.(slug)
service.<service-key>.settings.<settings-key>
~~~

## Sparse source and metadata

Supplied base code is never mounted or exposed to a tenant/agent. The Page Catalog exposes only visual output, base revision, page/component contract, allowed slots, and DTO schema.

Creating an override creates the same-name entry plus metadata in the company-root Change worktree:

~~~text
Admin/Pages/users/
  users.override.json
  users.<ui-extension>
  (slug)/
    users.override.json
    users.<ui-extension>
~~~

Metadata declares schema version, exact reference, supplied-base revision, same-name entry, operation, API version, and DTO. It is versioned but hidden from the normal code-only Explorer tree. Deleting the final files reverts to supplied base.

## Route and DTO conventions

~~~text
admin list:            /admin/<page-key>
admin detail:          /admin/<page-key>/(slug)
service list:          /admin/services/<service-key>/<page-key>
service detail:        /admin/services/<service-key>/<page-key>/(slug)
service API:           /api/v<major>/services/<service-key>/<resource-key>
MCP tool:              companyhq.<service-key>.<operation>
~~~

Every view binds an explicit DTO. A user/agent can use only fields returned by that DTO and active policy.

Every published API major stays supported under the current no-removal contract. Breaking changes create a new major without removing or weakening prior major contracts. Security and tenant-isolation corrections apply to every major.

## Agent provider contract

- Providers, profiles, and skills are disabled by default and enabled explicitly at company or project scope.
- A profile binds one provider instance to an agent role, task categories, services/projects, skills, tools, DTOs, source paths, network destinations, limits, tests, and approvals.
- Effective agent access is the non-additive intersection of the current delegator, profile/role, active skills/tools, task, Change/worktree, service/project, DTO, and approval.
- Provider-native task state is projected into Pipeline In progress, In review, or Done. Internal prompts, logs, subtasks, provider-owned skills, secrets, and hidden context do not enter the Pipeline.
- Enabling, disabling, updating, or removing a provider/profile/skill is a governed Change with revocation, retention, backup, rollback, and test behavior.

## Change checklist

Before approval, verify:

1. key, reference, route, detail variant, source path, component reference, and namespace are unique/registered;
2. supplied base source remains private and tenant source exists only for requested overrides;
3. model/DTO, field policy, supported API major, and MCP mapping are explicit;
4. agent delegation is no broader than the staff principal and is bound to task/Change/worktree/approval;
5. migration, hooks/jobs, notifications, secret references, backup, rollback, and enable/disable are accounted for;
6. API/UI/policy/DTO/MCP/security/isolation tests are selected and attached;
7. Explorer, service, models/DTOs, access, agent profiles/roles/skills, MCP, source-control, Pipeline, testing, security, and relevant docs are updated.
