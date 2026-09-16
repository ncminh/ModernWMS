# Unit Testing Baseline Plan

Status: **Phases 0-2 complete.** Phases 3+ are still just a proposal — nothing beyond Phase 2 has been implemented yet.

## 1. Current state (as found)

- **No test project is wired into the solution.** `ModernWMS.sln` only contains `ModernWMS`, `ModernWMS.Core`, `ModernWMS.WMS`, plus the `SQL` and `_docs` solution folders. There is no test project reference.
- An **empty, untracked** xUnit scaffold already existed at `_tests/MWMS.UnitTests/ModernWMS.UnitTests.csproj` (net8.0, xUnit 2.5.3, `Microsoft.NET.Test.Sdk` 17.8.0, `coverlet.collector` — note the project *file* is `ModernWMS.UnitTests.csproj`, only the containing folder is `MWMS.UnitTests`). It had no source files, was not added to `ModernWMS.sln`, and had no `ProjectReference` to any of the three main projects. Phase 0 finished wiring this project rather than creating a second one.
- CLAUDE.md is explicit that **"There is no test project in the solution... do not invent test commands."** This plan intentionally introduces one, so it should be reviewed as a deliberate change to that baseline, not a violation of it.
- No CI (no `.github/workflows`), so a new test project won't run anywhere automatically until CI is added — out of scope for this plan unless requested separately.
- **Assertion style: [Shouldly](https://github.com/shouldly/shouldly)**, not xUnit's `Assert.*`. The test project has `<Using Include="Shouldly" />` alongside the existing `<Using Include="Xunit" />`, so `using Shouldly;` doesn't need to be repeated per file. All Phase 0/1 tests were written this way from the start (or converted, for the handful predating the decision) — new phases should follow the same convention: `actual.ShouldBe(expected)`, `flag.ShouldBeTrue()/ShouldBeFalse()`, `x.ShouldBeNull()/ShouldNotBeNull()`, `collection.ShouldBeEmpty()`, `collection.ShouldHaveSingleItem()` (returns the item, so it chains: `data.ShouldHaveSingleItem().warehouse_name.ShouldBe("X")`), `id.ShouldBeGreaterThan(0)`.

### Architectural constraints that shape the test design

- **Convention-based DI/EF discovery is reflection-over-assemblies-on-disk.** `SqlDBContext.MappingEntityTypes` (`ModernWMS.Core/DBContext/SqlDBContext.cs`) scans `ModernWMS*.dll` in the executing app's base directory to find every `BaseModel` subclass, and `StartupExtensions` does the same for `IDependency` services and `IJob` jobs. A test project that references `ModernWMS.Core`/`ModernWMS.WMS` will have those DLLs copied into its own output folder by the SDK, so the scan works — but this means tests **must run as a normal `dotnet test` build output**, not from an isolated in-memory assembly, or the entity model comes back empty.
- **No repository abstraction over EF.** Services take `SqlDBContext` directly (concrete class, not an interface) and call `_dBContext.GetDbSet<TEntity>()`. There is nothing to substitute a mock for — mocking `DbContext`/`DbSet` here would fight the framework and Mapster's convention-based mapping. The pragmatic approach (and the one consistent with CLAUDE.md's "don't add abstractions beyond what the task requires") is to test services against a **real EF Core provider**, not a mocked context.
- **`FunctionHelper` is a concrete class, not behind an interface**, and several of the largest services depend on it directly (`AsnService`, `DispatchlistService`, `StockmoveService`, `StockfreezeService`, `StockprocessService`, …). It in turn needs a real `SqlDBContext` and an `IHttpContextAccessor`. For service-level tests that touch these dependencies, `IHttpContextAccessor` can be a trivial fake (no HTTP context ⇒ `GetCurrentUser()` returns a default `CurrentUser()`), and `FunctionHelper` itself can just be constructed for real against the same test `SqlDBContext` — cheaper than introducing an interface for something this small.
- **SQLite is the natural test provider.** `appsettings.Development.json` already targets SQLite, and the only existing migration was scaffolded against it. Recommended approach: `Microsoft.Data.Sqlite` in-memory (`DataSource=:memory:`, connection kept open for the test's lifetime) with `dbContext.Database.EnsureCreated()`, one fresh connection/context per test. This exercises real EF translation instead of the (looser) EF Core `InMemory` provider, and it matches the project's already-chosen dialect.
- **Multi-tenancy, audit columns, and form numbers are hand-rolled in service code**, not EF/DB features (see CLAUDE.md "Cross-cutting rules"). These are exactly the things worth unit-testing first, because nothing in the framework enforces them — a missing `tenant_id` filter or a skipped `creator`/`create_time` assignment is a silent bug, not a compile error.
- **Workflow status is an untyped `byte`** with meanings defined only inline in each service. Tests double as documentation of what each status value means, which the codebase currently lacks.
- **Controllers are thin** (`map service tuple → ResultModel`) and `[Authorize]` is commented out on `BaseController` — controller tests are low value relative to service tests and are pushed to a later, optional phase.

## 2. Phase 0 — done

1. ✅ Added `_tests/MWMS.UnitTests/ModernWMS.UnitTests.csproj` to `ModernWMS.sln` as a real project (`dotnet sln add`), not just a `_docs`-style solution folder item.
2. ✅ Added `ProjectReference`s to `ModernWMS.Core` and `ModernWMS.WMS`.
3. ✅ Added `Microsoft.EntityFrameworkCore.Sqlite` 8.0.23 as an explicit test-project package (matches the version already used by `ModernWMS.Core`). No mocking library was added — `IHttpContextAccessor` isn't needed yet (no test constructs `FunctionHelper` directly yet; that lands with Phase 4's `Asn` coverage) and there's no other interface-driven DI worth mocking at this stage.
4. ✅ Added shared test support under `_tests/MWMS.UnitTests/TestSupport/`:
   - `SqliteTestDbContextScope` — opens an in-memory SQLite connection, builds a real `SqlDBContext` against it, calls `Database.EnsureCreated()`, and disposes both together. Confirmed the `MappingEntityTypes` reflection scan (finding `BaseModel` subclasses across `ModernWMS*.dll`) works correctly from the test project's own build output.
   - `FakeStringLocalizer<T>` — implements `IStringLocalizer<T>` by returning the lookup key as-is; sufficient until a test needs to assert real localized text.
5. ✅ Landed the smoke test: `Services/Sku/SpuServiceTests.PageAsync_EmptyDatabase_ReturnsEmptyResult` — constructs `SpuService` directly (no DI container), runs `PageAsync` against an empty SQLite DB, asserts an empty result. `dotnet test _tests/MWMS.UnitTests/ModernWMS.UnitTests.csproj` → 1 passed. `dotnet build ModernWMS.sln` still succeeds (0 errors; pre-existing analyzer warnings in `ModernWMS.WMS` are unrelated to this change).
6. ✅ `dotnet test ModernWMS.sln` is now a real, documented command — CLAUDE.md's Commands section has been updated accordingly.

## 2b. Phase 1 — done

Added one test class per module under `_tests/MWMS.UnitTests/Services/<Module>/`, 54 tests total (55 with the Phase 0 smoke test), all green:

- `Warehouse` (8 tests): tenant-scoped `PageAsync`, `AddAsync` audit-column stamping + duplicate-name rejection (scoped per tenant, so the same name in a different tenant is allowed), `UpdateAsync` not-found handling and its cascade of `is_valid` down to child `Warehousearea`/`Goodslocation` rows, `DeleteAsync` blocked-by-child-`Goodslocation` guard and the unblocked success path.
- `Warehousearea` (8 tests): same tenant-isolation/duplicate/not-found shape, plus its own cascade (`UpdateAsync` pushes `area_name`/`area_property`/`is_valid` down to child `Goodslocation` rows) and its own delete guard (blocked by child `Goodslocation`).
- `Goodslocation` (7 tests): tenant isolation, add/update/duplicate/not-found, and the delete guard that's keyed on `Stock.qty > 0` rather than existence of any stock row at all (a location with a zero-qty stock row can still be deleted — asserted explicitly).
- `GoodsOwner` (8 tests): tenant isolation, audit stamping, duplicate rejection on add/update, delete, and both `ExcelAsync` branches (all-new import vs. a duplicate against an already-persisted row, which must reject **and leave the DB untouched**).
- `Customer` (7 tests): same CRUD shape, plus its own referential delete guard (blocked when a `Dispatchlist` row references the customer).
- `supplier` (8 tests): same CRUD shape, plus `ExcelAsync`'s duplicate-within-the-same-batch rejection, and a test that specifically documents `SupplierService.AddAsync`'s duplicate-name check running *after* the entity is already staged via `AddAsync` (see finding below) — asserts the net effect (rejection, nothing actually committed) still holds today.
- `company` (8 tests): same CRUD shape; `GetAsync` on an unknown id returns an empty `CompanyViewModel` (not null) per current behavior, asserted explicitly since it differs from the `null`-returning `GetAsync` on the other six services.

`dotnet test _tests/MWMS.UnitTests/ModernWMS.UnitTests.csproj` → **55 passed**, 0 failed. `dotnet build ModernWMS.sln` still succeeds with 0 errors.

### Gotcha hit while writing these (fixed in the test code, not production code)

`DbSet<T>.FindAsync` and `.Find` check the change tracker's local identity map **before** hitting the database. `WarehouseService.DeleteAsync` (and every other module's delete) uses `ExecuteDeleteAsync`, which deletes directly in SQL and bypasses the change tracker entirely — so a test that `Add()`ed an entity earlier in the same `SqlDBContext` and then calls `FindAsync` after deleting it will get back the stale tracked instance instead of `null`. Every "confirm the row is really gone" assertion in these tests uses `.AsNoTracking().FirstOrDefaultAsync(...)` instead of `FindAsync` for this reason.

### Findings surfaced — fixed

Writing these tests surfaced two bugs, both now fixed; full writeup (root cause, exact diff, verification) is in **`_docs/issue-logs.md`**:

1. **`GetAsync`/`UpdateAsync`/`DeleteAsync`-by-id had no `tenant_id` filter**, across all seven Phase 1 services and `SpuService`. Fixed by threading `CurrentUser` into every by-id method (three services — `GoodsownerService`, `CustomerService`, `CompanyService` — didn't take it at all on `UpdateAsync`) and adding the tenant check to each lookup/delete predicate, including updating the corresponding interfaces and controller call sites. `SpuService.DeleteAsync` needed an extra guard since it also deletes child `Sku` rows that have no `tenant_id` of their own. 24 new cross-tenant regression tests added (one `GetAsync`/`UpdateAsync`/`DeleteAsync` test per service).
2. **`SupplierService.AddAsync` staged the new entity via `AddAsync` before running the duplicate-name check**, unlike every sibling service. Fixed by reordering to check-then-add, matching the rest of the codebase.

`dotnet test ModernWMS.sln` → **79 passed**, 0 failed (55 from Phase 1 + 24 new regression tests). `dotnet build ModernWMS.sln` → 0 errors.

## 2c. Phase 2 — done

Added one test class per module, 43 new tests (122 total):

- `Category` (10 tests): tenant isolation, add/update duplicate-name rejection, `UpdateAsync`'s `is_valid` cascade down a category tree, `DeleteAsync`'s referenced-by-`Spu` guard and whole-subtree delete, plus the three cross-tenant regression tests.
- `Freightfee` (9 tests): tenant-scoped `PageAsync`, add/update/delete, `ExcelAsync` import, plus cross-tenant regression tests. No natural-key duplicate check exists for this module (carrier/route/price, not a name) — matches current behavior, nothing to test there.
- `Print` (`PrintSolutionService`, 9 tests): tenant-scoped `PageAsync` and the path-scoped `GetByPathAsync` (both `vue_path` **and** `tab_page` must match, and tenant-scoped), add/update/delete, plus cross-tenant regression tests.
- `Sku` (`SpuService`, 19 tests, up from the 4 already covering Phase 0/1's cross-tenant work): tenant-scoped `PageAsync`, add/update duplicate-`spu_code` rejection, the three `detailList` reconciliation branches on `UpdateAsync` (`id == 0` → add a new `Sku` row, `id > 0` → update an existing one, `id < 0` → remove the row named by `-id`), `DeleteAsync`'s referenced-by-`Asn` guard and its cascade-delete of child `Sku` rows, `GetSkuAsync`/`GetSkuByBarCodeAsync`, and `InsertOrUpdateSkuSafetyStockAsync`'s add/remove branches.

`dotnet test _tests/MWMS.UnitTests/ModernWMS.UnitTests.csproj` → **122 passed**, 0 failed. `dotnet build ModernWMS.sln` → 0 errors.

### Findings surfaced — fixed

Two more bugs, on top of the two from Phase 1 (same eleven-services tenant-filter gap extended to `Category`/`Freightfee`/`PrintSolution` — see Issue 1 in `_docs/issue-logs.md`, already updated). Full writeup for both in **`_docs/issue-logs.md`**:

3. **`CategoryService.GetChildren` recursed on `item.parent_id` instead of `item.id`.** In `UpdateAsync`'s `is_valid` cascade (which loads the whole non-root category table as its candidate pool) this caused **infinite recursion — `StackOverflowException`, uncatchable, kills the process** — for any 3-level-deep hierarchy. In `DeleteAsync` (which only loaded direct children to begin with) it instead silently failed to cascade past one level, orphaning grandchildren. Both the recursion and `DeleteAsync`'s too-narrow initial query were fixed. **Caution for anyone re-running just this file in isolation**: the regression tests that prove this (`CategoryServiceTests.UpdateAsync_TogglingInvalid_CascadesToDescendants`, `DeleteAsync_WithMultiLevelDescendants_DeletesWholeSubtree`) use a real 3-level hierarchy — that's deliberate, since a 2-level tree wouldn't have exercised the bug at all.
4. **`SpuService.UpdateAsync` recomputed sibling `Sku.volume` via `ExecuteUpdateAsync` with a `Math.Round` in the expression tree**, which EF Core's SQLite provider cannot translate to SQL — breaking every `UpdateAsync` call that actually persists a change, under this project's own default `appsettings.Development.json` (`SQLITE`) setup. Fixed by fetching the sibling rows and recomputing in memory instead of pushing the computation into a single SQL statement.

`dotnet test ModernWMS.sln` → **122 passed**, 0 failed. `dotnet build ModernWMS.sln` → 0 errors.

## 3. Phased rollout (each phase = one PR, one module family)

Ordering favors **small, self-contained modules first** to validate the harness under real conditions, then moves to the modules CLAUDE.md flags as the most complex/risky, where regressions are also the most expensive.

| Phase | Scope | Why this grouping |
|---|---|---|
| **0** ✅ | Test project wiring + shared fixtures + one smoke test (see §2) | Nothing else can start until the harness exists and is proven. |
| **1** ✅ | Reference/master-data services: `Warehouse`, `Warehousearea`, `Goodslocation`, `GoodsOwner`, `Customer`, `supplier`, `company` | Small, mostly CRUD + `tenant_id` filtering, no cross-module quantity math. Good for validating the SQLite-fixture pattern (insert → query → assert) cheaply and catching any tenant-isolation gaps early. |
| **2** ✅ | `Sku` (Spu/Category), `Freightfee`, `Print` | Still CRUD-shaped; `Sku` has a few more relations (category tree) worth exercising with real data. |
| **3** | `user`, `userrole`, `Rolemenu` | Auth-adjacent but not the JWT/login flow itself (that's `ModernWMS.Core/AccountController`, out of scope here). Focus on role/menu authority assignment logic, since CLAUDE.md flags a recent bug fix in `rolemenu.menu_actions_authority`. |
| **4** | `Asn` (`AsnService`, ~1400 lines) | First "where the complexity actually is" module. Cover: form-number generation via `FunctionHelper`, status transitions (`asn_status` literals), tenant filtering, and audit-column stamping on insert/update. |
| **5** | `Dispatchlist` (`DispatchlistService`, ~1800 lines) | Largest/most complex service per CLAUDE.md. Split this phase further if needed (e.g. list/search vs. status-transition/stock-deduction logic) rather than trying to cover it in one PR. |
| **6** | Stock movement family: `Stock`, `Stockmove`, `Stockadjust`, `Stockfreeze`, `Stockprocess`, `Stocktaking` | CLAUDE.md explicitly warns a quantity change usually has to touch several of these consistently — test them together (shared fixtures/scenarios) so cross-service quantity invariants (e.g. total on-hand conserved across a move) are actually asserted, not just each service in isolation. |
| **7 (optional/backlog)** | `ActionLog`, `Approve/FlowSetService` (no controller/interface — unfinished module), controller-layer tests | Lower value: `ActionLog` is peripheral, and `FlowSetService` is explicitly unfinished per CLAUDE.md — don't sink test effort into an API that may still change shape. Controller tests only add value once `[Authorize]` / real auth is enabled. |

Notes for every phase:
- Cover the module's **service** layer, not controllers (controllers just map tuples to `ResultModel`).
- Every test must set `tenant_id` on the fixture's `SqlDBContext` and assert cross-tenant data is excluded — this is a hand-rolled filter with no compiler backstop.
- Assert audit columns (`creator`, `create_time`, `last_update_time`) and `id = 0`-before-insert are actually set, since EF doesn't do this automatically.
- Where a service calls `FunctionHelper.GetFormNoAsync`, assert the generated number's prefix/reset behavior against the real `global_unique_serial` table, not a mock.
- Add the module's status `byte` values as named local constants in the test file (since no enum exists) — cheap documentation, and keeps the assertions readable.

## 4. Explicitly out of scope for this plan

- CI wiring (no `.github/workflows` exists today) — separate decision.
- Refactoring services to introduce interfaces/mocking seams purely for testability — would contradict CLAUDE.md's anti-abstraction guidance; only reconsider if a specific phase proves genuinely un-testable without it.
- `ModernWMS.Core` infrastructure itself (JWT, Hangfire jobs, Swagger, middleware) — different risk profile from the WMS domain logic, would need its own plan.
- Load/performance testing, migrations testing, frontend testing.
