This is a headless multi-tenant app. On a customer-owned server, all companies share one public reverse proxy and one Postgres container, while every company has a separate Postgres database, a separate least-privilege database role, and its own application/service workspace. The SQLite registry stores only installation and hostname metadata; it does not contain company profile or business data. Company files live at `CompanyHQ/Companies/{company-slug}/`. The admin page of that company uses `/admin` and the website uses `/`.

The current delivery target is `Plans/shared-server-company-hosting.md`. The assigned agent must first compare the existing implementation to that plan and record the gaps, implement every missing requirement, then run and report the P10 confirmation and acceptance tests. Do not mark the work complete without evidence that two companies can be developed and queried independently.

---
The User should be able to add their own services to grow the company ontop of the base cms provided. All services should be added to the services project directory noting that the user should not be able to see or get the default/base services.

The user creates services inside the following :

Services/{Service}/
Services/{Service}/Api
Services/{Service}/Services
Services/{Service}/Models
Services/{Service}/Pages (This is for the ui of this service)
Services/{Service}/Settings (This is for the settings pages of this service)
Services/{Service}/{service}.json

> The services require a bool to confirm that that service is enabled in the company
---

---
The User would be able to overide the admin ui and use the model like the User model to be able to create their own style for the admin service ui (they cant edit the default model). The overide should take place using the same name as the admin ui but when they remove the custom ui then the default should return :

Admin/
Admin/Layout
Admin/Pages
Admin/Services (this is where the services would live in the explorer)
---

The user should be able to extend the models from the ui as well as use code to extend the model. The user should be able to link 2 tables like the user (default) and then adding the orders as a link to the user when the service grows.

---
The user would use commands to add the templates to the server:

-u UserName
-c CompanyName
-sip {server IP address if the config local SSH key is not being used.}
-conf {This is the directory to use the SSH key from the local config}

> Password is console inputs
---
