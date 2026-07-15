# Page and layout overrides

Users own the scaffolded `src/Web` and non-hidden `src/Services` source. Hidden service source is withheld and ships only as compiled modules.

Routing remains registry-driven: service pages do not use `@page`. Registering a user component under the same stable page name after hidden modules load replaces only that page; removing the registration restores the hidden default. The hidden service logic and API endpoints are never replaced.

For deployment-local markup overrides, place `<route>.html` beneath `CompanyHQ/Admin/Pages` (for example `users.html`). The route is confined to that company's mounted Pages directory, is read on navigation, and removing it immediately restores the registered default. Different company mounts can carry different markers without sharing files or cache state.

Layouts use Blazor's built-in `LayoutComponentBase` and `LayoutView`. Register `MainLayout`, `AdminLayout`, or `CompanyLayout` in `LayoutRegistry` to replace that shell, or register another layout name and resolve it for a page. Same-name registrations use last-registration-wins precedence and unregistering restores the previous layout.
