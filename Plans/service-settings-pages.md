# Service settings pages

## Goal

Wire module settings pages into the admin UI with server-side authorization and enabled-service gating.

## Scope

- Register `IServiceModule.SettingsPages` in `ComponentRegistry` as `services/{id}/settings` and named subroutes.
- Gate settings pages by effective service enablement.
- Add Settings links to eligible service cards.
- Require the `ManageUsers` policy at the settings route.

## Task-specific tests

- Assert a settings component resolves only while its service is enabled.
- Disable the service and assert its settings route returns 404.
- bUnit-test Settings link visibility for modules with and without settings pages.
- Assert a Viewer receives server-side denial at a settings route.
