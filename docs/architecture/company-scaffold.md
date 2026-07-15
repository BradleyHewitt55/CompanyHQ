# Company scaffold

Each generated company owns one immutable deployment directory beneath `CompanyHQ/Companies/{company-slug}/`. The slug is normalized at creation and is never derived from the mutable display name again.

```text
CompanyHQ/
  Companies/
    acme-industries/
      company.json
      .env
      docker-compose.yml
      data/
      data-protection/
      CompanyHQ/
```

Inside that deployment, the company owns exactly one editable tree:

```text
CompanyHQ/
  Admin/
    Pages/
    Services/
  Projects/
    Website/
      Pages/
      Services/
```

`src/` contains platform build inputs and is not user-facing. Compose mounts only paths beneath its own company directory, so services and overrides cannot cross company boundaries.

The server registry stores the immutable slug. Only an Owner can delete a registered company, after typing its exact display name and accepting an explicit browser confirmation. The server uses `Companies:Root` plus that stored slug to stop the Compose deployment and remove only the matching company directory; it never trusts a client-supplied path.
