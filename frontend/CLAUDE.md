# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
yarn dev       # start Vite dev server (localhost:5173, per .env.development)
yarn build     # type-check with vue-tsc, then build with vite
yarn preview   # preview the production build locally
```

There is no configured test runner and no lint script in `package.json` — ESLint/Prettier configs exist (`.eslintrc`, `.prettierrc.json`) but must be invoked directly, e.g. `npx eslint src --ext .ts,.vue`. There are no unit/e2e tests in this repo.

The backend API base URL/port come from `.env.development` / `.env.production` (`VITE_BASE_PATH` + `VITE_SERVER_PORT`); the dev API server must be running separately for the app to function.

## Architecture

This is the Vue 3 + TypeScript + Vuetify 3 frontend for a WMS (Warehouse Management System, package name `ykwms`). It talks to a separate .NET backend (see the sibling backend repo/directory) over a REST API proxied through `axios`.

### Permission-driven dynamic routing

Routes are **not** statically defined per page. `src/router/index.ts` only declares `/login` and a `home` shell route. After login, `loadRouter()` calls `menusToRouter` (`src/utils/router/index.ts`) to turn the user's `menulist` (from `store/module/user.ts`, populated from the login API) into `vue-router` routes, resolved against `import.meta.glob('../view/*/*/*.vue')`. This means:
- Every page component under `src/view/<module>/<page>/<page>.vue` is only reachable if the backend grants a matching menu entry for the logged-in user.
- The router guard in `router.beforeEach` redirects to `/login` when there's no token, and rebuilds routes on first navigation after login (`dynamicRouter.length === 0`).
- `menusToSideBar` (same file) builds the left nav from the same `menulist`, and `GetMenuNameAndModule`/`GetModuleAndIcon` are **hardcoded switch statements keyed by route name** — any new page/module must be added to both switches or it won't get a sidebar label/icon.
- Per-row/button permissions (edit, delete, import, export, etc.) come from `getMenuAuthorityList()` (`src/utils/common.ts`), which matches the current route against `menulist[].menu_actions` and is consumed by components like `BtnGroup` (`src/components/system/btnGroup.vue`) via an `authority-list`/`code` convention.

### HTTP layer and auth refresh

All API calls go through the single axios instance in `src/utils/http/request.ts` (baseURL built from `VITE_BASE_PATH`/`VITE_SERVER_PORT`). Its interceptors:
- Attach `Authorization: Bearer <token>` from `store.getters['user/token']`, except for `/login` and `/user/register`.
- Detect near-expiry tokens (`isTokenExpired`, 10-minute window) and transparently call `/refresh-token`, queueing (`pushSubscribeInterface`) any requests that fired while a refresh is in flight, then replaying them with the new token.
- Show/hide a global loading indicator via a request counter and the `emitter` event bus (`src/utils/bus.ts`, mitt), and surface errors/success via the global `hookComponent.$message`/`$dialog` (see below) rather than component-local UI.
- Unwrap successful responses to `response.data` when `code === 0` or the `success` response header is `'true'`.

API functions live under `src/api/<domain>/*.ts` (`base`, `sys`, `wms`), each just a thin wrapper returning `http({ url, method, data/params })` — follow this pattern (see `src/api/base/customer.ts`) rather than calling `http`/`axios` directly from view components.

### Global "hook components" (imperative UI from anywhere)

`src/components/system/index.ts` auto-registers every module under `src/components/system/hookComponent/**/*.ts` (via `import.meta.globEager`) onto `app.config.globalProperties` as `$<name>` (e.g. `$message`, `$dialog`), and also exports them as the `hookComponent` object. Use `hookComponent.$message({ type, content })` / `hookComponent.$dialog({ content, handleConfirm })` from `<script setup>` code (where `this` isn't available) instead of `ElMessage`-style imports — this is the project's convention for toasts and confirm dialogs everywhere, including inside the shared `request.ts` interceptor.

### View module structure

Feature pages live under `src/view/<module>/<page>/`, where `<module>` is `base`, `wms`, `deliveryManagement`, `warehouseWorking`, `statisticAnalysis`, `login`, `home`, `vwms`, `largeScreen`, `pageTemplate`. Each page is typically a folder containing:
- `<page>.vue` — the list/table view (search form + `vxe-table` + pagination via `custom-pager`), the file that the dynamic router loads.
- `add-or-update-<page>.vue` — the create/edit dialog form, opened/closed via local `showDialog`/`dialogForm` state and a `saveSuccess`/`close` event pair.
- Optional `import-table.vue` (Excel import dialog) and other page-specific dialogs (barcode/QR dialogs, tab components for sub-resources).

Corresponding request/response types live in `src/types/<Module>/<Entity>.ts` (note the module folder here is PascalCase, e.g. `types/Base/Customer.ts`, while `view`/`api` folders are camelCase) and are imported into both the API wrapper and the view.

Cross-page reusable pieces:
- `src/components/select/*.vue` — async-loaded dropdown selects for common entities (commodity, employee, freight, location, sku, warehouse).
- `src/components/table/vxe-date-column.vue` — registered globally in `main.ts` as `<VxeDateColumn>` for consistent date formatting in `vxe-table` columns.
- `src/constant/*.ts` — shared enums/config, notably `vxeTable.ts` (`PAGE_SIZE`, `PAGE_LAYOUT`, `DEFAULT_PAGE_SIZE`) and `style.ts` (`computedCardHeight`/`computedTableHeight` helpers used to size tables against the viewport, `errorColor`).
- `src/utils/common.ts` — `setSearchObject`/`removeArrayNull`/`removeObjectNull` for building the backend's generic filter payload, `getMenuAuthorityList`, and `localStorage` helpers (`getStorage`/`setStorage`).
- `src/utils/exportTable.ts` — Excel export used by list pages' "export" button (built on `vxe-table-plugin-export-xlsx`/`xlsx`).

### i18n

`vue-i18n` is initialized in `src/languages/i18n.ts` from JSON dictionaries in `src/languages/langsJson/{cn,en,tw}.json`, keyed by the same dot-path structure referenced throughout views (`$t('base.customer.customer_name')`, `i18n.global.t(...)`). Adding a field/label to a view requires adding the key to **all three** language files. The current UI language is read from Vuex (`store.getters['system/language']`) and also drives the `culture` query param sent on every HTTP request.

### Printing

`src/components/hiprint/` and `src/hiprint/` wrap the `yk-vue-plugin-hiprint` plugin (bootstrapped in `main.ts` via `setup()` on `app.config.globalProperties.hiprint`) for designing/printing label templates (barcodes via `jsbarcode`, QR via `qrcode.vue`/`vue-qr`). Print button flows in list views typically collect selected `vxe-table` rows and open `hiprintFast.vue` with a `tab-page` key that maps to a saved print template (see `src/api/base/printSolution.ts` and `src/view/base/print/`).

### Build tooling notes

- Path alias `@` → `src` (configured in both `vite.config.ts` and `tsconfig.json`).
- `vue-i18n` is aliased to its CJS build in `vite.config.ts` to avoid a known Vite/vue-i18n ESM interop issue.
- `unplugin-auto-import` / `unplugin-vue-components` / `unplugin-icons` are configured as devDependencies but currently generate empty `auto-imports.d.ts`/`components.d.ts` — don't assume Vue APIs or components are globally auto-imported; check existing `import` statements in a file before adding new ones.
- Code style: no semicolons, single quotes, 2-space indent (Prettier `printWidth: 150`; ESLint `max-len: 200`) — see `.prettierrc.json`/`.eslintrc`.
