# Convert the frontend2 login pilot from JS/JSX to TypeScript

## Context

`frontend2` was scaffolded and built out as plain JS/JSX (the Vite default),
while the Vue app it's replacing (`frontend/`) is TypeScript. The user wants
to know how much work it would take to switch the pilot to TypeScript before
committing to it, and wants the plan reviewed before any file changes happen.

This is a **mechanical, no-behavior-change** conversion: rename files, add
type annotations/interfaces for the backend contract and component props, add
the TS toolchain. No runtime logic changes. Everything below was verified
against what's actually installed/available right now (registry checks for
`typescript-eslint`, `typescript`, `@types/md5`), not assumed.

## Effort summary

| Category | Count | Notes |
|---|---|---|
| Config files (new/edited) | 9 | `tsconfig.json` (new), `tsconfig.app.json` (new), `tsconfig.node.json` (new), `src/vite-env.d.ts` (new), `vite.config.js`→`.ts`, `eslint.config.js` (edited, stays `.js`), `components.json` (`"tsx": true`), `index.html` (script src), `package.json` (scripts + deps) |
| App source files renamed + typed | 15 | every current `.jsx`/`.js` under `src/` (list below) |
| Test files renamed + typed | 2 | `login-page.test.jsx`, `protected-route.test.jsx` |
| New shared type definitions | ~4 interfaces | `Envelope<T>`, `LoginResponseData`, `MenuItem`, `StoredAuth`/`AuthContextValue` — one small `src/lib/types.ts` |
| Docs | 1 | `MIGRATION.md` has ~3 sentences that explicitly say "plain JS, not TSX" — these need updating so they don't mislead the next developer |
| New devDependencies | 4 | `typescript`, `typescript-eslint`, `@types/node`, `@types/md5` |

**Total: ~26 file touches, 4 new packages, zero behavior change.** For a
pilot this size, this is a single focused pass, not a multi-day effort. The
only non-mechanical part is writing correct prop types for the 5 shadcn UI
primitives (button/input/label/checkbox/card) — everything else is
copy-the-shape-of-the-existing-file-and-add-types.

### A version gotcha found during research

`typescript-eslint@8.70.0` (current latest) declares
`"typescript": ">=4.8.4 <6.1.0"` as a peer range. The `typescript` package's
npm `latest` tag is currently `7.0.2` (a newer major line). Installing
`typescript@latest` blind would produce an unsupported combination and noisy
peer-dependency warnings (or a broken lint step). **Plan is to pin
`typescript@^6.0.3`** (the newest release inside typescript-eslint's supported
range) rather than taking `latest`.

## What changes, file by file

### 1. Toolchain / config

- **Install**: `typescript@^6.0.3`, `typescript-eslint@^8.70.0`,
  `@types/node`, `@types/md5` (all other deps — react-router-dom, axios,
  @radix-ui/*, class-variance-authority, clsx, tailwind-merge, lucide-react —
  already ship their own types, no extra `@types/*` needed for them).
- **`tsconfig.json`** (new, solution/references file), **`tsconfig.app.json`**
  (new — `src/**`, `strict: true`, `jsx: "react-jsx"`,
  `moduleResolution: "bundler"`, `paths: { "@/*": ["./src/*"] }`), and
  **`tsconfig.node.json`** (new — covers `vite.config.ts` only). This is the
  current standard Vite React+TS scaffold layout (three-file project-reference
  split), not the older two-file layout `frontend/` uses — `frontend/`'s
  `tsconfig.json` predates that convention and also has a corrupted `include`
  array from a bad find/replace, so it's a pattern to learn from, not copy.
  Recommending **`strict: true`** (not the Vue app's `noImplicitAny: false`
  relaxation) since this is a good point to start the React codebase on strict
  typing before it grows.
- **`src/vite-env.d.ts`** (new): `/// <reference types="vite/client" />` plus
  an `ImportMetaEnv` augmentation for `VITE_BASE_PATH`, `VITE_SERVER_PORT`,
  `VITE_CLI_PORT` so `import.meta.env.VITE_BASE_PATH` in `src/lib/http.ts`
  type-checks instead of being `any`.
- **`vite.config.js` → `vite.config.ts`**: same content, typed via
  `defineConfig`.
- **`eslint.config.js`**: stays a `.js` file (flat config runs directly under
  Node, doesn't need to be compiled), but switches from plain
  `js.configs.recommended` to `typescript-eslint`'s `tseslint.config(...)`
  helper layered with the existing `reactHooks`/`reactRefresh` configs, and
  `files` patterns extended to `**/*.{ts,tsx}`.
- **`components.json`**: flip `"tsx": false` → `"tsx": true` so a future
  `npx shadcn add <component>` generates `.tsx` files matching the rest of the
  app.
- **`index.html`**: `<script src="/src/main.jsx">` → `main.tsx`.
- **`package.json`**: `"build": "tsc -b && vite build"` (mirrors the Vue app's
  `vue-tsc && vite build` pattern — type-check, then build), keep `test`/
  `lint`/`dev`/`preview` as-is.

### 2. Shared types (new file: `src/lib/types.ts`)

One small file holding the interfaces that both `auth-api.ts` and
`auth-context.tsx` need, sourced from the backend contract already documented
in `MIGRATION.md` §2 (verified against the actual C# controllers, not
guessed):

```ts
export interface Envelope<T> {
  isSuccess: boolean
  code: number
  errorMessage: string
  data: T
}

export interface MenuItem {
  id: number
  menu_name: string
  module: string
  vue_path: string
  vue_path_detail: string
  vue_directory: string
  sort: number
  menu_actions: string[]
}

export interface LoginResponseData {
  user_num: string
  user_name: string
  user_id: number
  user_role: string
  userrole_id: number
  tenant_id: number
  expire: number
  access_token: string
  refresh_token: string
}

export interface StoredAuth {
  token: string
  refreshToken: string
  expiresAt: number
  user: {
    userId: number
    userNum: string
    userName: string
    userRole: string
    userroleId: number
    tenantId: number
  }
  menuList: MenuItem[]
}
```

### 3. App source files (rename `.js`/`.jsx` → `.ts`/`.tsx`, add types)

Existing files, same logic, typed in place:

- `src/lib/utils.ts` — trivial (`cn(...inputs: ClassValue[])`).
- `src/lib/http.ts` — `http.interceptors.response.use` typed to return
  `Envelope<unknown>`-shaped data; callers narrow via generics on `http.post<T>`/`http.get<T>`.
- `src/lib/auth-api.ts` — `login(params): Promise<Envelope<LoginResponseData>>`,
  `getUserAuthority(userroleId: number): Promise<Envelope<MenuItem[]>>`.
- `src/lib/auth-storage.ts` — `readAuth(): StoredAuth | null`,
  `writeAuth(auth: StoredAuth | null): void`.
- `src/lib/auth-context.tsx` — `AuthContextValue` interface, `children: ReactNode`,
  `createContext<AuthContextValue | null>(null)`, same throw-if-null pattern in
  `useAuth()`.
- `src/components/ui/{button,input,label,checkbox,card}.tsx` — canonical
  shadcn TS prop typing per component: `button.tsx` gets
  `React.ComponentProps<'button'> & VariantProps<typeof buttonVariants> & { asChild?: boolean }`;
  `input.tsx`/`card.tsx` get `React.ComponentProps<'input'>`/`<'div'>`;
  `label.tsx`/`checkbox.tsx` get `React.ComponentProps<typeof LabelPrimitive.Root>`/
  `typeof CheckboxPrimitive.Root>`. This is the one part that isn't pure
  mechanical translation, but it's copying shadcn's own well-known TS template
  shape, not inventing new types.
- `src/components/protected-route.tsx` — `{ children: ReactNode }` prop.
- `src/pages/login/login-page.tsx` — typed form event handlers
  (`React.FormEvent<HTMLFormElement>`, `React.ChangeEvent<HTMLInputElement>`),
  and `useLocation()`'s `state` narrowed via
  `(location.state as { from?: string } | null)?.from`.
- `src/pages/home/home-page.tsx` — `PLACEHOLDER_MENUS: { name: string; icon: LucideIcon }[]`.
- `src/App.tsx`, `src/main.tsx` — no logic change; `main.tsx` needs
  `document.getElementById('root')!` (non-null assertion, standard Vite
  template pattern).

### 4. Tests (rename `.jsx` → `.tsx`)

- `src/pages/login/login-page.test.tsx`, `src/components/protected-route.test.tsx`
  — same test bodies; `vi.mock('@/lib/auth-api')` + `vi.mocked(login)` /
  `vi.mocked(getUserAuthority)` for typed `mockResolvedValue(...)` calls
  against the `Envelope<LoginResponseData>` / `Envelope<MenuItem[]>` shapes
  from `src/lib/types.ts`.

### 5. Docs

`MIGRATION.md` currently states in a few places that `frontend2` is
"plain JS, not TSX" (§5, shadcn components section) and that the build step
has "no type-check step... unlike the Vue app's `vue-tsc` build step" (§6).
Both statements flip once this lands — update them in place so the doc stays
accurate for the next developer, rather than leaving stale claims.

## Verification

1. `npm install` (new deps).
2. `npm run build` — now `tsc -b && vite build`; a clean run proves every
   renamed file type-checks with `strict: true`.
3. `npm run lint` — confirms the `typescript-eslint` flat-config swap works
   and nothing regresses on the existing `react-hooks`/`react-refresh` rules.
4. `npm test` — the same 6 existing Vitest cases must still pass unchanged
   (this is the strongest signal that no runtime behavior moved).
5. No new browser/screenshot smoke test planned — this is a type-layer-only
   change with no intended behavior change, so the existing test suite +
   build + lint is the right bar. If any of the three surface an unexpected
   runtime difference, that's a signal to stop and investigate rather than
   push through.
