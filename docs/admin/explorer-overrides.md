# Explorer, page references, and overrides

> **Document status:** Target V1 product contract.

The Explorer is a governed view of tenant-owned source. It opens the company root repository on protected main by default and shows only source that the company has actually added. It does not reveal supplied base-admin code.

## Page Catalog versus Source Explorer

| Surface | Shows | Never shows |
| --- | --- | --- |
| Page Catalog | All supplied and service pages by name, route, effective UI preview, base revision, component contract, slots, and DTO contract. | Supplied source bytes, source maps, internal paths, internal classes, or base-code diff. |
| Source Explorer | Company-created overrides, custom services, and project source in the selected repository/branch. | A file or directory for an untouched supplied page. |

This is deliberate. A tenant can customize a page without being handed a fork of CompanyHQ's base admin implementation.

## Same-name sparse override files

Each page family has a stable lowercase kebab-case key. When an owner/developer first customizes it, CompanyHQ creates only that page's same-name override entry and metadata in a Change worktree.

~~~text
Admin/
  Pages/
    users/
      users.override.json
      users.<ui-extension>
      (slug)/
        users.override.json
        users.<ui-extension>
~~~

The user sees the Users page by the same Users name in the Page Catalog and edits the users entry in the Source Explorer. No directory exists until the first override is created. Deleting the final override files through a Change restores the supplied page immediately.

The ui-extension is selected by the eventual admin renderer. This contract names the file and ownership rule without choosing a framework.

## List and detail page references

CompanyHQ uses the parent page as the reference and the literal (slug) variant for its detail page:

| Page family | List/main reference and route | Detail reference and route template |
| --- | --- | --- |
| Users | admin.users at /admin/users | admin.users.(slug) at /admin/users/(slug) |
| Projects | admin.projects at /admin/projects | admin.projects.(slug) at /admin/projects/(slug) |
| Service products | service.ecommerce.pages.products at /admin/services/ecommerce/products | service.ecommerce.pages.products.(slug) at /admin/services/ecommerce/products/(slug) |

The (slug) notation is CompanyHQ's source and documentation template notation. At runtime it is one URL segment. The slug is only a lookup input; authorization always occurs against the resolved company record and never against the mutable slug string.

Service overrides use the same layout:

~~~text
Admin/
  Services/
    ecommerce/
      Pages/
        products/
          products.override.json
          products.<ui-extension>
          (slug)/
            products.override.json
            products.<ui-extension>
      Settings/
        general/
          general.override.json
          general.<ui-extension>
~~~

## Override metadata

Override metadata is created with the entry file, versioned in Git, and hidden from the normal code-only tree unless a developer opens page details. It is framework-neutral and must declare the page reference, parent base revision, entry name, API version, operation, and DTO binding.

~~~json
{
  "schemaVersion": 1,
  "reference": "admin.users.(slug)",
  "extends": { "layer": "supplied-base", "revision": "base-admin@approved" },
  "entry": "users",
  "data": {
    "operation": "admin.users.(slug).read",
    "apiVersion": "v1",
    "dto": "AdminUserDetailDto"
  }
}
~~~

The registry validates the path, file name, reference, DTO, and operation together. A similar display label or arbitrary filename never overrides a page.

## DTO-first page editing

An override receives the registered page DTO, not the raw model. A Users detail override may use user.name and user.email only when the active user's AdminUserDetailDto contains them. It cannot query a hidden model field or recreate a broader user object in the browser.

When a permitted owner/developer adds a model attribute through Settings and includes it in the relevant DTO, that field becomes available to the associated admin/client page and its delegated agent after the Change is approved. See [models and DTOs](../models-and-dtos.md).

## Resolution and fallback

The effective UI resolves:

~~~text
tenant same-name override -> enabled service contribution -> supplied base UI
~~~

The supplied base is a rendered fallback and contract, not tenant-visible source. The Explorer can compare visual output, DTO contract, and tenant-owned diff; it cannot show or copy a base-code diff.

An override for an optional service remains in Git but becomes inactive when its service is disabled. It does not resolve to an unrelated admin page.

## Edit and agent workflow

Creating an override, editing tenant code, or sending a selected component to an agent:

1. checks the user's role, company, page, project/service, DTO, and action policy;
2. creates a Change and a named isolated worktree in the owning repository;
3. scopes the editor/agent to the selected same-name files and allowed DTO;
4. records diff, preview, tests, pull request, approval, and rollback evidence;
5. returns to supplied base when the override is deleted.

The visual picker and bottom coding composer are defined in [visual agent handoff](visual-agent-handoff.md).

## Documentation update rule

A new page family, detail variant, service page, slot, model/DTO binding, route/API version, MCP operation, or service must update this registry, its service contract, access policy, tests, and the [extension-governance skill](../../.agents/skills/companyhq-extension-governance/SKILL.md) before approval.
