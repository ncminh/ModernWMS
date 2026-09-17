# Vue → React Migration: Login Flow Pilot

This document explains how the **login flow** was ported from the Vue 3 app in
`frontend/` to the React app in `frontend2/`. It is written for a developer who
knows the Vue app (or the backend) but has never touched `frontend2/`, and who
needs to either review this pilot or continue the migration with the next
module.

It covers, end to end: the backend contract, how each Vue file maps to its
React equivalent, why each architectural decision was made, how to run/test
the app, and what was deliberately left out of scope.

## 1. Why this exists

`frontend2/` is a pilot for migrating the whole WMS frontend off Vue 3 /
Vuetify and onto React. The goal of the pilot was to prove the pattern on the
smallest possible vertical slice — sign in, land on a home shell, see your
granted menus — before committing to porting the ~9 feature modules under
`frontend/src/view/`.

Scope of this pilot, intentionally:
- Login page (username/password, remember-me, show/hide password, validation).
- Fetching the user's menu authority after login.
- A placeholder home screen with a static sidebar and the real granted-menu
  list, gated behind an auth check.
- Unit tests for the above.

Explicitly **not** in scope (see [§8](#8-whats-intentionally-out-of-scope)):
dynamic per-menu routing, i18n, token refresh, the global loading spinner /
toast system, printing, and every non-login feature module.

## 2. Backend contract this flow depends on

Both apps talk to the same .NET backend (`backend/`). The React client was
written by reading the actual controllers/DTOs, not by guessing from the Vue
code, so treat this section as the source of truth if the backend changes.

### Response envelope

Every endpoint returns `ResultModel<T>` (`ModernWMS.Core/Models/ResultModel.cs`).
The backend has global `CamelCasePropertyNamesContractResolver` configured
(`ModernWMS.Core/Extentions/StartupExtensions.cs`), so PascalCase C# properties
are camelCased on the wire; snake_case properties are emitted unchanged.

```json
{
  "isSuccess": true,
  "code": 200,
  "errorMessage": "",
  "data": { }
}
```

### `POST /login`

`ModernWMS.Core/Controller/AccountController.cs` → `LoginAsync`.

Request body (`LoginInputViewModel`):
```json
{ "user_name": "admin", "password": "<md5 hex of the plaintext password>" }
```
The backend expects the password **pre-hashed with MD5** by the client — this
is not a real security control, it's just the existing contract, so the React
client has to match it (see [§4.3](#43-authentication-httpts-auth-apits)).

Success `data` (`LoginOutputViewModel`):
```json
{
  "user_num": "admin",
  "user_name": "Administrator",
  "user_id": 1,
  "user_role": "Admin",
  "userrole_id": 1,
  "tenant_id": 1,
  "expire": 120,
  "access_token": "...",
  "refresh_token": "..."
}
```
`expire` is in **minutes**.

### `GET /rolemenu/authority?userrole_id={int}`

`ModernWMS.WMS/Controllers/RoleMenu/RoleMenuController.cs` → `GetMenusByRoleId`.
Called right after login with the `userrole_id` from the login response.
Success `data` is an array of `MenuViewModel`:
```json
[
  {
    "id": 1,
    "menu_name": "Customer",
    "module": "base",
    "vue_path": "customer",
    "vue_path_detail": "",
    "vue_directory": "base/customer",
    "sort": 0,
    "menu_actions": ["add", "edit", "delete"]
  }
]
```
An empty array means the account exists but has no assigned menus — the Vue
app treats this as a login failure ("no authority"), and so does the React
port.

### `POST /refresh-token`

`AccountController.RefreshToken`. Request: `{ accessToken, refreshToken }`
(camelCased). Response `data` is just the new access token string (no new
refresh token). **Not implemented in the React pilot** — see
[§8](#8-whats-intentionally-out-of-scope).

## 3. Side-by-side: Vue file → React file

| Concern | Vue (`frontend/`) | React (`frontend2/`) |
|---|---|---|
| Login page shell | `src/view/login/login.vue` | `src/pages/login/login-page.tsx` |
| Login form + submit logic | `src/components/login/login-form.vue` | `src/pages/login/login-page.tsx` (merged — see [§4.1](#41-login-page-srcpagesloginlogin-pagetsx)) |
| Login/menu API calls | `src/api/sys/login.ts` | `src/lib/auth-api.ts` |
| Axios instance + interceptors | `src/utils/http/request.ts` | `src/lib/http.ts` |
| Auth/session state (Vuex module) | `src/store/module/user.ts` | `src/lib/auth-context.tsx` + `src/lib/auth-storage.ts` |
| Router auth guard | `src/router/index.ts` (`router.beforeEach`) | `src/components/protected-route.tsx` |
| Route table | `src/router/index.ts` | `src/App.tsx` |
| Dynamic menu → route/sidebar | `src/utils/router/index.ts` (`menusToRouter`, `menusToSideBar`) | *not ported* — `src/pages/home/home-page.tsx` renders a static placeholder sidebar plus the raw granted-menu list (see [§8](#8-whats-intentionally-out-of-scope)) |
| Global toasts (`hookComponent.$message`) | `src/components/system/hookComponent/*` | inline `role="alert"` text in the form (see [§4.1](#41-login-page-srcpagesloginlogin-pagetsx)) |
| UI library | Vuetify 3 (`v-text-field`, `v-btn`, ...) | shadcn/ui primitives over Tailwind (see [§5](#5-ui-layer-shadcnui--tailwind)) |

## 4. Walking through the React implementation

### 4.1 Login page (`src/pages/login/login-page.tsx`)

This merges what Vue split across `login.vue` (page shell) and
`login-form.vue` (form + submit handler) into a single component, because
there's no separate branding panel/i18n switcher to justify a second file in
this pilot.

Flow, mirroring `login-form.vue`'s `method.login()`:
1. `handleSubmit` prevents default submit, clears any previous error.
2. Client-side required-field checks for username/password (equivalent to
   Vuetify's `userNameVaildRules`/`passwordVaildRules`) — shown inline as
   `<p role="alert">`.
3. Calls `login({ userName, password })` (`src/lib/auth-api.ts`).
4. If `!isSuccess`, show `errorMessage` and stop — **does not** call the
   authority endpoint (mirrors the Vue early-return).
5. On success, calls `getUserAuthority(loginRes.data.userrole_id)`.
6. If that fails, or returns an empty array, show an error and stop —
   the user is **not** signed in in either case (this matches Vue: it only
   commits token/user state to the store after the authority check passes).
7. On full success, calls `signIn(...)` from `useAuth()` — this is the React
   equivalent of the block of `store.commit('user/set...')` calls in
   `login-form.vue`.
8. "Remember me" — see [§4.4](#44-remember-me-a-deliberate-behavior-change) —
   then `navigate(redirectTo, { replace: true })`.

State is plain `useState`, not a form library — there are only two fields, so
`react-hook-form`/`zod` would be pure overhead for this pilot. If a later
module needs 10+ validated fields, that's the point to introduce one, not now.

### 4.2 Auth state (`src/lib/auth-context.tsx`, `src/lib/auth-storage.ts`)

Vue's `store/module/user.ts` is a Vuex module with `state`/`mutations`/
`getters` for `token`, `refreshToken`, `expirationTime`, `effectiveMinutes`,
`userInfo`, `menulist`, plus `isRefreshingToken` used only by the token-refresh
queue.

React has no global store in this pilot (no Redux/Zustand dependency was
added), so the equivalent is a `React.Context`:

- `src/lib/auth-storage.ts` — the only code that touches `localStorage`
  directly. `readAuth()`/`writeAuth(auth)` serialize a single JSON blob under
  the key `wms_auth`: `{ token, refreshToken, expiresAt, user, menuList }`.
  This is intentionally **one key holding one object**, not five separate
  Vuex-style keys, because there's no reducer/mutation layer to keep five keys
  in sync — one blob is the simpler invariant to hold.
- `src/lib/auth-context.tsx` — `AuthProvider` lazy-initializes its state from
  `readAuth()` (so a page refresh doesn't lose the session), and exposes
  `{ user, menuList, isAuthenticated, signIn(payload), signOut() }` via
  `useAuth()`. `isAuthenticated` is derived (`Boolean(auth?.token)`), not
  stored — same reasoning as above, avoid two sources of truth.
- **Every** `signIn`/`signOut` call goes through `writeAuth` *and* `setAuth`
  together, so `localStorage` and React state can never drift apart.

`src/lib/http.ts` reads the token straight from `readAuth()` on every request
(no React involved) — this is what lets a plain axios module attach
`Authorization` headers without needing to be "inside" a component tree,
mirroring how Vue's `request.ts` reads straight from the Vuex `store` object.

### 4.3 Authentication (`http.ts`, `auth-api.ts`)

`src/lib/http.ts` is a deliberately trimmed-down version of
`utils/http/request.ts`:
- Same base URL construction: `` `${VITE_BASE_PATH}:${VITE_SERVER_PORT}` ``.
- Same rule for which URLs skip the `Authorization` header (`/login`,
  `/user/register`).
- **Does not** port: the request counter / `emitter` loading-spinner events,
  the `culture` query param / i18n coupling, or the token-refresh queue
  (`isTokenExpired`, `pushSubscribeInterface`, `handleRefreshToken`). See
  [§8](#8-whats-intentionally-out-of-scope) for why and what that means in
  practice today.

`src/lib/auth-api.ts` mirrors `api/sys/login.ts` 1:1 (two thin wrapper
functions, `login` and `getUserAuthority`), with one addition: it MD5-hashes
the password client-side before sending it (`Md5.hashStr` in Vue → the `md5`
npm package here), because the backend contract requires it
(see [§2](#2-backend-contract-this-flow-depends-on)).

### 4.4 "Remember me" — a deliberate behavior change

Vue's `login-form.vue` XORs the password with a hardcoded string
(`'ModernWMS2024'`) and stores *both* username and password in `localStorage`
under `userLoginInfo`, with a chain of fallbacks to decode older/differently-
encoded blobs. That XOR is not real encryption — anyone with access to the
browser's `localStorage` can recover the plaintext password trivially.

The React port only remembers the **username** (`wms_remembered_user` in
`localStorage`) and always requires the password to be re-typed. This is a
one-line intentional divergence from a faithful port, not an oversight — flag
it in review if a faithful (but insecure) port of the password-remembering
behavior turns out to be a real product requirement.

### 4.5 Route guard (`src/components/protected-route.tsx`)

Vue's `router.beforeEach` guard does three things: redirect to `/login` when
there's no token, lazily build dynamic routes from the menu list on first
navigation after login, and prevent back/forward navigation
(`window.history.pushState` in `router.afterEach`).

`ProtectedRoute` only ports the first part:
```jsx
if (!isAuthenticated) {
  return <Navigate to="/login" replace state={{ from: location.pathname }} />
}
return children
```
It's a wrapper component used in `App.tsx`, not a router-level guard, because
React Router v7 (data-router mode aside) doesn't have a direct equivalent of
`beforeEach` for the simple `<Routes>` setup used here. The dynamic-route-
building and back/forward-blocking behaviors are out of scope — see
[§8](#8-whats-intentionally-out-of-scope).

`LoginPage` reads `location.state.from` and redirects there after a successful
sign-in, so deep-linking to a protected URL while logged out survives the
login round-trip.

### 4.6 Placeholder home (`src/pages/home/home-page.tsx`)

Vue's post-login experience is fully dynamic: `menusToRouter`/`menusToSideBar`
(`utils/router/index.ts`) turn the backend's `menulist` into real routes
(resolved against `import.meta.glob('../view/*/*/*.vue')`) and a real sidebar,
with `GetMenuNameAndModule`/`GetModuleAndIcon` hardcoded switches supplying
labels/icons per route name.

None of that exists yet in React — there's nothing to route *to*. Instead,
`HomePage` renders:
- A **static** placeholder sidebar (`Dashboard`/`Inventory`/`Orders`/
  `Settings`) — inert buttons, not real navigation. This is the "some
  placeholder menus" the pilot asked for; it exists to prove the shell layout,
  not to be built on directly.
- The **real** `menuList` from the auth context, listed as plain text
  (`menu_name (module)`), to prove the login → authority → render pipeline
  actually works end to end with live backend data.

When the next module is ported, this is the file to replace with real
routing — see [§9](#9-how-to-port-the-next-module).

## 5. UI layer: shadcn/ui + Tailwind

Vue uses Vuetify 3 (a full Material component library, theme via
`plugins/vuetify`). React uses [shadcn/ui](https://ui.shadcn.com) — not a
component *library* you install, but a set of components you copy into your
own repo and own outright, built on Tailwind CSS v4 and Radix primitives.

What's in the repo:
- `components.json` — shadcn CLI config (`style: "new-york"`, base color
  `neutral`, path aliases). This means you *can* use
  `npx shadcn@latest add <component>` to pull in more components the same
  way — it's not decorative, it's live config.
- `src/index.css` — Tailwind v4 import + the shadcn color tokens as CSS
  variables (`--background`, `--primary`, `--border`, ...), both light and
  `.dark` variants, plus the `@theme inline` block that maps them to Tailwind
  utility classes (`bg-background`, `text-primary`, etc).
- `src/lib/utils.ts` — the standard shadcn `cn()` helper
  (`clsx` + `tailwind-merge`), used by every UI component to merge default
  classes with any `className` override.
- `src/components/ui/` — the five primitives this pilot needed: `button.tsx`,
  `input.tsx`, `label.tsx`, `checkbox.tsx`, `card.tsx`. These are the verbatim
  shadcn "new-york" style TSX components — `frontend2` is TypeScript (see
  [§10](#10-typescript) for how/when that was added), so no type-stripping was
  needed to bring them in.
- `vite.config.ts` — registers `@tailwindcss/vite` and the `@` → `src` path
  alias (mirrors the Vue app's `@` alias in `vite.config.ts`/`tsconfig.json`).

**To add another shadcn component** (e.g. `Select`, `Dialog`, `Alert`) for the
next module, either run the CLI (`npx shadcn@latest add select`) from
`frontend2/`, or hand-copy the TSX source from ui.shadcn.com straight into
`src/components/ui/` — both are normal; the CLI is faster and keeps you on the
same version conventions as `components.json` already declares
(`"tsx": true`).

## 6. Project setup, running, and testing

### Environment

`.env.development` / `.env.production` follow the same
`VITE_BASE_PATH` + `VITE_SERVER_PORT` convention as `frontend/`, so both apps
point at the same backend without any code changes:
```
VITE_BASE_PATH=http://localhost
VITE_SERVER_PORT=16453
```
The backend must be running separately (same requirement as the Vue app) —
`frontend2` has no mock/stub backend of its own.

### Commands

```bash
npm install        # first time only
npm run dev         # Vite dev server (defaults to :5173; frontend/ also
                     # defaults to :5173, so don't run both at once, or pass
                     # --port to one of them)
npm run build       # `tsc -b && vite build` — type-checks first, same shape
                     # as the Vue app's `vue-tsc && vite build` step
npm run lint        # ESLint (flat config, react-hooks + react-refresh rules)
npm test            # vitest run — unit tests, see below
```

Unlike `frontend/`, this app **does** have a configured test runner and test
script, and that should stay true for every module ported after this one.

### Tests

- `vitest` + `@testing-library/react` + `@testing-library/user-event`,
  configured directly in `vite.config.ts` (`test: { environment: 'jsdom', ... }`)
  rather than a separate `vitest.config.js`.
- `src/test/setup.ts` runs before every test file. It does two things:
  1. Loads `@testing-library/jest-dom/vitest` for the `toHaveTextContent`-style
     matchers.
  2. Polyfills `ResizeObserver` — Radix's `Checkbox` (via
     `@radix-ui/react-use-size`) calls it, and it doesn't exist in jsdom.
     **If a future component uses Radix and a test involving it throws
     `ResizeObserver is not defined`, this is why — it's already handled here,
     you don't need to re-add it per test file.**
- `src/pages/login/login-page.test.tsx` — mocks `@/lib/auth-api` entirely
  (`vi.mock`) and drives the form through four cases: empty-submit validation,
  server-rejected credentials, zero-menu-authority block, and full
  success-signs-in-and-stores-menus. It renders a tiny `<AuthProbe>` sibling
  component to assert on the resulting auth-context state instead of reaching
  into internals.
- `src/components/protected-route.test.tsx` — renders a real two-route
  `<Routes>` tree and asserts the redirect-when-signed-out and
  render-when-signed-in behaviors, seeding the signed-in case via
  `writeAuth()` directly (not through the UI) since that's the unit under
  test.
- **A gotcha if you write more form tests**: `screen.getByLabelText(/username/i)`
  is not safe here — it will also match the "**Remember my username**"
  checkbox label and throw a multiple-elements error. Use an anchored regex
  (`/^username$/i`) instead. Same issue exists for `/password/i` vs. the
  "Show password" icon button's `aria-label`.

## 7. Visual/manual verification done for this pilot

There's no project-level "run the app" skill yet for `frontend2/` (there is
one for the Vue app already — nothing has been written for this one). To spot
check the pilot, the dev server was started manually and driven with a
headless Chromium (`playwright-core`, pointed at the already-cached browser
binary since `chromium-cli` wasn't available in this environment) to screenshot:
- The login page at rest.
- The empty-submit validation message.
- The redirect from `/` to `/login` when signed out.

If you're picking this up, a real "run" skill for `frontend2/` (dev server
launch, port, and a login smoke test) is worth writing the first time you need
to do this more than once — see the repo's `run` skill guidance for the
pattern.

## 8. What's intentionally out of scope

Everything below exists in the Vue app but was deliberately **not** ported in
this pilot. Don't treat their absence as a bug — treat it as the next slice(s)
of migration work:

| Not ported | Where it lives in Vue | Why it's deferred |
|---|---|---|
| Token refresh (`/refresh-token`, expiry-window detection, request queueing while refreshing) | `utils/http/request.ts` (`isTokenExpired`, `handleRefreshToken`, `pushSubscribeInterface`) | No other authenticated API calls exist yet in `frontend2` to need it for; adding it now would be speculative |
| Dynamic menu → route/sidebar generation | `utils/router/index.ts` (`menusToRouter`, `menusToSideBar`), `router/index.ts`'s `import.meta.glob` | There are no ported feature pages yet to route to — see [§9](#9-how-to-port-the-next-module) |
| i18n (`vue-i18n`, `en`/`cn`/`tw` JSON dictionaries, `culture` query param on every request) | `languages/`, `utils/http/request.ts` | Whole-app concern, bigger than one module; needs its own decision (e.g. `react-i18next`) before any page bakes in hardcoded English strings that would need re-touching |
| Global loading spinner / toast system (`hookComponent.$message`/`$dialog`, `emitter` bus) | `components/system/hookComponent/**`, `utils/bus.ts` | The login page's own inline error text was sufficient for this scope; a shared toast/dialog convention should be designed once more than one page needs it |
| Back/forward navigation blocking | `router.afterEach` (`window.history.pushState`) | Minor UX detail, not core to proving the auth flow |
| Printing, hiprint, barcode/QR | `components/hiprint/`, `hiprint/` | Unrelated to login; a separate, much later migration slice |
| Per-row/button permission checks (`getMenuAuthorityList`, `menu_actions`) | `utils/common.ts` | No permission-gated UI exists yet in React to apply it to; the raw `menu_actions` array is already present in the fetched menu data (§2) for whenever it's needed |

## 9. How to port the next module

Follow the same shape this pilot used:

1. **Find the Vue source**: `frontend/src/view/<module>/<page>/` (list view)
   and `add-or-update-<page>.vue` (form), per `frontend/CLAUDE.md`'s
   "View module structure" section.
2. **Find the backend contract first**, from the actual controller/DTOs under
   `backend/`, the same way [§2](#2-backend-contract-this-flow-depends-on) was
   built — don't infer field names/casing from the Vue code alone, verify
   against the C# models.
3. **API wrapper**: add functions to a new `src/lib/<domain>-api.ts` (thin
   wrappers around `http` from `src/lib/http.ts`), same pattern as
   `auth-api.ts`.
4. **UI**: pull in whatever shadcn primitives the page needs
   (`npx shadcn@latest add ...`), following [§5](#5-ui-layer-shadcnui--tailwind).
5. **Page**: add it under `src/pages/<module>/`, wrap it in `<ProtectedRoute>`
   in `App.tsx`.
6. **Tests**: mock the API module with `vi.mock`, write the same shape of
   tests as `login-page.test.tsx` (validation, error path, success path).
7. **Once ≥2 modules exist**, that's the point to revisit the deferred items
   in [§8](#8-whats-intentionally-out-of-scope) that stop being speculative —
   most urgently dynamic routing/sidebar generation (once there's more than
   one destination to route to) and a shared toast/error-display convention
   (once more than one page needs to report async errors).

## 10. TypeScript

The pilot started as plain JS/JSX and was converted to TypeScript once the
login flow itself was working, on the reasoning that it's better to establish
strict typing conventions while the codebase is still one page than to retrofit
them after several modules exist. `frontend2` is TS from this point forward —
every new file should be `.ts`/`.tsx`, not `.js`/`.jsx`.

### Config layout

Unlike `frontend/`'s two-file `tsconfig.json` + `tsconfig.node.json` (an older
convention, and one whose `tsconfig.json` has a corrupted `include` array from
a bad find/replace — don't copy it), `frontend2` uses the current standard
Vite React+TS three-file **project-reference** split:
- `tsconfig.json` — empty solution file, just references the other two.
- `tsconfig.app.json` — covers `src/**`. `strict: true` (not the Vue app's
  `noImplicitAny: false` relaxation — deliberately stricter, since this is a
  new codebase).
- `tsconfig.node.json` — covers `vite.config.ts` only.

Both set `"skipLibCheck": true` — required to avoid a real error a plain
`tsc -b` throws otherwise (`tinybench`, a transitive dependency pulled in via
Vitest's `/// <reference types="vitest/config" />` in `vite.config.ts`, has a
`.d.ts` that references `DOMHighResTimeStamp` without assuming `lib: "DOM"` is
present). This is the standard Vite template default for exactly this class of
third-party-typings issue, not a shortcut taken here.

### The `http.ts` typed-wrapper pattern

`src/lib/http.ts`'s response interceptor unwraps `AxiosResponse` down to just
`response.data` at runtime (same as the Vue app's `request.ts`). Typing this
naively — exporting the raw `axios.create(...)` instance typed as
`AxiosInstance` — would make every call site's return type lie: `http.post<T>()`
would claim to resolve to `AxiosResponse<T>` (with `.data`, `.status`, etc.)
when it actually resolves to just the unwrapped envelope at runtime. Instead,
`http` is exported cast to a narrower shape —
`{ get<T>(url, config?): Promise<T>; post<T>(url, data?, config?): Promise<T> }`
— so callers like `src/lib/auth-api.ts` write
`http.post<Envelope<LoginResponseData>>('/login', {...})` and get back exactly
what actually arrives at runtime. **Reuse this cast, don't re-derive it**, when
adding the next domain's API wrapper (see [§9](#9-how-to-port-the-next-module)).

### A version pin to know about

`typescript-eslint` (currently `^8.70.0`) declares a peer range of
`"typescript": ">=4.8.4 <6.1.0"`. The `typescript` package's npm `latest` tag
was `7.0.2` at the time this was set up — a newer major line outside that
range. `frontend2` pins `typescript@^6.0.3` (the newest release inside
`typescript-eslint`'s supported range) instead of taking `latest` blindly.
**Re-check this pin** before bumping `typescript-eslint` or `typescript`
independently — bump them together, and only as far as the installed
`typescript-eslint` version's peer range actually allows.

### Test typing

`vi.mock('@/lib/auth-api')` mocks the module; `vi.mocked(login)` /
`vi.mocked(getUserAuthority)` (see `src/pages/login/login-page.test.tsx`) give
back a properly-typed mock whose `.mockResolvedValue(...)` argument is checked
against the real `Envelope<LoginResponseData>` / `Envelope<MenuItem[]>` return
types from `src/lib/types.ts` — use the same `vi.mocked(...)` wrapping for any
new mocked API module rather than casting mock return values by hand.
