# Authorization matrix

The server uses an authenticated fallback policy. Every exception must carry `AllowAnonymous`; hiding a control never grants or removes authority.

| Surface | Anonymous | Viewer | Member | Admin | Owner |
|---|---:|---:|---:|---:|---:|
| Home, login, health | Allow | Allow | Allow | Allow | Allow |
| Bootstrap | Token + one-shot flag | Token + one-shot flag | Token + one-shot flag | Token + one-shot flag | Token + one-shot flag |
| Company registration | Registry token | Registry token | Registry token | Registry token | Registry token |
| Company deletion | Deny | Deny | Deny | Deny | Exact-name confirmation required |
| Admin pages, service metadata/read APIs | Deny | Allow | Allow | Allow | Allow |
| Create/update domain records | Deny | Deny | Allow | Allow | Allow |
| Change password/logout | Deny | Own identity | Own identity | Own identity | Own identity |
| Users directory | Deny | Read | Read | Read | Read |
| Roles, user deletion, service state, scaffolding/runtime load | Deny | Deny | Deny | Allow | Allow |
| Model/schema extensions | Deny | Deny | Deny | Deny | Allow |

Company scope is configuration and deployment derived: each company has its own database, data-protection key ring, service/override mounts, builder root, and compose project. Client-supplied company IDs are never used as authority. Resource IDs are resolved only inside that scoped database. Runtime builds are confined to the configured company service root and authenticated to the internal builder with a per-company secret.
