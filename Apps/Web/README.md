# CompanyHQ Web

One Next.js application hosts the company's public website and its administration console.

## Page ownership

```text
app/
  page.tsx        The public home page: company name, description, and contact details
  home.css
  auth/           Sign in, sign up, password reset, email verification
  admin/
    layout.tsx
    shell.tsx
    admin.css
    (pages)/
      (dashboard)/
      users/
      blog/
      faqs/
      messages/
      email-templates/
      settings/
    _resources/
```

The home page is the default website a company receives. It reads the company profile the
administrator saves under **Settings → Company Profile** and needs no session. Replace or extend it
for a company that wants more than a standard page; keep it free of sign-in requirements.

Parentheses are Next.js route groups: the dashboard stays `/admin`. `_resources` contains shared
admin list/editor page composition and is not routable. Every supported resource has its own list
and `[id]` entrypoints; `new` uses the same editor as details. Resource fields and specific editor
styles live beside those routes. An admin section exists only when the API serves it.

Keep page markup and page CSS in `app`. `Components` holds reusable controls, cards and tables.
Admin lists all use `Components/Admin/resource-table.tsx`. Preserve the Main and Services sections in
`app/admin/shell.tsx`.

Both areas import canonical design tokens through `app/globals.css`. The console is permission-based:
`/api/auth/me` returns the signed-in role's permissions, `middleware.ts` and `app/admin/layout.tsx`
admit anyone whose role holds at least one console permission, and each list, page and action checks
the specific one (`Components/admin-data.ts`, `src/api/access.ts`). Those checks only decide what
the console offers; the API enforces every call, so navigation is not an authorization boundary.

## Local checks

Copy `.env.example` to `.env.local` and start the API and Redis services.

```sh
npm install
npm run typecheck
npm run lint
npm run build
npm run start -- --port 3100
```

Browser tests are local-only. `tests/`, `playwright.config.ts`, and their output are gitignored, and
Playwright is not a dependency of this package; install it locally with
`npm install --no-save @playwright/test` when you need it.
