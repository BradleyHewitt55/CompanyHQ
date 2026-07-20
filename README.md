# CompanyHQ

This is a dynamic multi-tenant headless CMS system. Each user hosts their own server

## Company Creation

- You can create a new company using the command `companyhq new company {CompanyName}` or using the `AdminUI`
---
CompanyHQ/{CompanyName}/
CompanyHQ/{CompanyName}/Services/ // initialy empty
---
- The user gets a template that they can add services to the company using `companyhq new company {ServiceName}`
---
CompanyHQ/{CompanyName}/
CompanyHQ/{CompanyName}/Services/
CompanyHQ/{CompanyName}/Services/{Service}/
CompanyHQ/{CompanyName}/Services/{Service}/Models
CompanyHQ/{CompanyName}/Services/{Service}/Dtos
CompanyHQ/{CompanyName}/Services/{Service}/Services
CompanyHQ/{CompanyName}/Services/{Service}/Components // These are service specific components that is used by the service pages
CompanyHQ/{CompanyName}/Services/{Service}/Pages // This is for the service main pages
CompanyHQ/{CompanyName}/Services/{Service}/Settings // This is for the service settings
---

## Service plugins

Services are headless Razor Class Library plugins so they can be consumed by any CompanyHQ host. Each service references `Abstractions` and implements `ICompanyHqPlugin`; hosts use that contract to discover metadata and register the service's dependencies without coupling the service to the Admin project.
