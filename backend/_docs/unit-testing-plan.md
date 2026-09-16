# Unit Testing Baseline Plan

Status: **Phases 0-4 complete** (`AsnService` split per Option B; sub-phases 4a-4e all done). Phases 5+ are still just a proposal.

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

## 2d. Phase 3 — done

Before writing tests, renamed the three service classes to proper PascalCase, at the user's request and to the exact scope they confirmed: **service class only** (matching the precedent already set by the user's own earlier renames — `WarehouseareaService`→`WarehouseAreaService`, `GoodslocationService`→`GoodsLocationService`, `GoodsownerService`→`GoodsOwnerService`, `FreightfeeService`→`FreightFeeService`). `UserroleService` → `UserRoleService`, `RolemenuService` → `RoleMenuService` (`UserService` was already correctly cased, so it's unchanged). Interfaces (`IUserroleService`, `IRolemenuService`), controllers (`UserroleController`, `RolemenuController`), folders, and entities/viewmodels (`UserroleEntity`, `RolemenuEntity`, `RolemenuViewModel`, …) were explicitly left as-is — the user confirmed this scope and also confirmed the Core-project `UserEntity`/`UserroleEntity` (they live in `ModernWMS.Core/Models/`, not `ModernWMS.WMS`, and the user has been actively hand-editing `UserEntity` there per commit `971dd4b`) should not be touched at all.

Added one test class per module, 43 new tests (165 total):

- `User` (`UserService`, 20 tests): tenant-scoped `PageAsync` and `GetSelectItemsAsnyc` (valid-only, tenant-scoped role list), add/update duplicate-`UserNum` rejection with audit stamping (the generated password returned to the caller is never the same string as the stored `AuthString` — it's hashed), delete, `ExcelAsync`'s three branches, `ResetPwd` (including its own cross-tenant regression test — this one batch-resets by a client-supplied id list), `ChangePwd` (unknown id / wrong old password / correct old password), and `Register` — the new-tenant signup flow that creates the user, a default "admin" `Userrole`, ~24 starter `Menu` rows, and one `Rolemenu` mapping per menu, all under one freshly-minted `tenant_id`.
- `UserRole` (`UserroleService` class now `UserRoleService`, 11 tests): tenant isolation, add/update duplicate-`role_name` rejection, `UpdateAsync`'s rename-cascade onto every `User.UserRole` string field carrying the old role name (denormalized, not a real FK), `BulkSaveAsync`'s combined add/update/delete-by-negative-id contract, plus cross-tenant regression tests.
- `RoleMenu` (`RolemenuService` class now `RoleMenuService`, 12 tests): tenant-scoped `GetAllAsync`/`GetAllMenusAsync`/`GetMenusByRoleId`, `GetAsync`'s JSON-deserialized `menu_actions_authority`, `AddAsync`'s "one mapping set per role" guard, `UpdateAsync`'s add/update/remove-by-negative-id reconciliation (same shape as `UserRole.BulkSaveAsync` and `Spu.UpdateAsync`'s detail list), delete, plus cross-tenant regression tests.

`dotnet test _tests/MWMS.UnitTests/ModernWMS.UnitTests.csproj` → **165 passed**, 0 failed. `dotnet build ModernWMS.sln` → 0 errors.

### Findings surfaced — fixed

Same Issue 1 tenant-filter gap, now also closed in `UserService`, `UserroleService`, and `RolemenuService` — see the updated Issue 1 in `_docs/issue-logs.md`. Two variants worth calling out specifically: `UserService.ResetPwd` batch-resets passwords for a client-supplied id list with no tenant filter at all, and `RolemenuService.GetAsync`/`GetMenusByRoleId`/`DeleteAsync` are keyed by `userrole_id` rather than `id` and had no `CurrentUser` parameter at all (not just a missing filter). `UserService.ChangePwd` was deliberately left alone — full reasoning in the issue log.

### Gotcha hit while writing these (fixed in the test code, not production code)

`RolemenuService.UpdateAsync` builds brand-new `RolemenuEntity` instances (via a LINQ projection) carrying the *same* primary keys as rows already persisted, then calls `UpdateRange`/`AddRange`/`RemoveRange` on them — this only works because a real HTTP request gets a **fresh** `SqlDBContext` from DI, so those ids were never tracked in that context to begin with. A test that seeds rows with `Add()` + `SaveChangesAsync()` and then reuses the *same* `SqlDBContext` for the service call still has those seed instances tracked, so EF Core's identity map throws ("another instance with the same key value... is already being tracked") the moment the service tries to attach its own detached instances with matching ids. Fixed by calling `scope.DbContext.ChangeTracker.Clear()` after seeding and before invoking the service, in `RoleMenuServiceTests.UpdateAsync_AddsUpdatesAndRemovesMappingsInOneCall` — the same "simulate a fresh per-request context" idea as Phase 1's `FindAsync`-vs-`ExecuteDeleteAsync` gotcha, just triggered by attach/update instead of a stale read.

`dotnet test ModernWMS.sln` → **165 passed**, 0 failed. `dotnet build ModernWMS.sln` → 0 errors.

## 3. Phased rollout (each phase = one PR, one module family)

Ordering favors **small, self-contained modules first** to validate the harness under real conditions, then moves to the modules CLAUDE.md flags as the most complex/risky, where regressions are also the most expensive.

| Phase | Scope | Why this grouping |
|---|---|---|
| **0** ✅ | Test project wiring + shared fixtures + one smoke test (see §2) | Nothing else can start until the harness exists and is proven. |
| **1** ✅ | Reference/master-data services: `Warehouse`, `Warehousearea`, `Goodslocation`, `GoodsOwner`, `Customer`, `supplier`, `company` | Small, mostly CRUD + `tenant_id` filtering, no cross-module quantity math. Good for validating the SQLite-fixture pattern (insert → query → assert) cheaply and catching any tenant-isolation gaps early. |
| **2** ✅ | `Sku` (Spu/Category), `Freightfee`, `Print` | Still CRUD-shaped; `Sku` has a few more relations (category tree) worth exercising with real data. |
| **3** ✅ | `user`, `userrole`, `Rolemenu` | Auth-adjacent but not the JWT/login flow itself (that's `ModernWMS.Core/AccountController`, out of scope here). Focus on role/menu authority assignment logic, since CLAUDE.md flags a recent bug fix in `rolemenu.menu_actions_authority`. |
| **4** ✅ | `Asn` (`AsnService`, ~1400 lines pre-split) | First "where the complexity actually is" module. Cover: form-number generation via `FunctionHelper`, status transitions (`asn_status` literals), tenant filtering, and audit-column stamping on insert/update. **Broken down into sub-phases 4a-4e (all done), pre-refactored per Option B — see §3a below.** |
| **5** | `Dispatchlist` (`DispatchlistService`, ~1800 lines) | Largest/most complex service per CLAUDE.md. Split this phase further if needed (e.g. list/search vs. status-transition/stock-deduction logic) rather than trying to cover it in one PR. |
| **6** | Stock movement family: `Stock`, `Stockmove`, `Stockadjust`, `Stockfreeze`, `Stockprocess`, `Stocktaking` | CLAUDE.md explicitly warns a quantity change usually has to touch several of these consistently — test them together (shared fixtures/scenarios) so cross-service quantity invariants (e.g. total on-hand conserved across a move) are actually asserted, not just each service in isolation. |
| **7 (optional/backlog)** | `ActionLog`, `Approve/FlowSetService` (no controller/interface — unfinished module), controller-layer tests | Lower value: `ActionLog` is peripheral, and `FlowSetService` is explicitly unfinished per CLAUDE.md — don't sink test effort into an API that may still change shape. Controller tests only add value once `[Authorize]` / real auth is enabled. |

Notes for every phase:
- Cover the module's **service** layer, not controllers (controllers just map tuples to `ResultModel`).
- Every test must set `tenant_id` on the fixture's `SqlDBContext` and assert cross-tenant data is excluded — this is a hand-rolled filter with no compiler backstop.
- Assert audit columns (`creator`, `create_time`, `last_update_time`) and `id = 0`-before-insert are actually set, since EF doesn't do this automatically.
- Where a service calls `FunctionHelper.GetFormNoAsync`, assert the generated number's prefix/reset behavior against the real `global_unique_serial` table, not a mock.
- Add the module's status `byte` values as named local constants in the test file (since no enum exists) — cheap documentation, and keeps the assertions readable.

## 3a. Phase 4 breakdown: `AsnService` — status as of this section: **proposal, not started**

`AsnService` is a different shape of risk than Phases 1-3. Reading the whole file (1423 lines) before writing anything surfaced:

- **The by-id/by-tenant gap from Issue 1 (`_docs/issue-logs.md`) is bigger here than anywhere else.** It's not just `GetAsync`/`UpdateAsync`/`DeleteAsync` this time — **none** of the eleven "Flow Api" methods (`ConfirmAsync`, `ConfirmCancelAsync`, `UnloadAsync`, `UnloadCancelAsync`, `SortingAsync`, `GetAsnsortsAsync`, `ModifyAsnsortsAsync`, `SortedAsync`, `SortedCancelAsync`, `GetPendingPutawayDataAsync`, `PutAwayAsync`) filter by `tenant_id` at all — each just does `Asns.Where(t => idList.Contains(t.id))` against a client-supplied id list. `UpdateAsnmasterAsync`'s entity fetch has the same gap, and `DeleteAsnmasterAsync` doesn't take a `CurrentUser` at all. This is the same class of bug as before, just present on ~13 methods instead of 2-3 — worth fixing slice-by-slice as each sub-phase below is tested, same as every phase so far, rather than as one giant separate pass.
- **A likely-live correctness bug, found by reading, not yet by a test:** `ConfirmAsync` and `UnloadAsync` both do
  ```csharp
  entities.ForEach(t =>
  {
      var vm = viewModels.FirstOrDefault(t => t.id == t.id);   // <- bug
      ...
  });
  ```
  The inner lambda's `t` parameter shadows the outer `t`, so `t.id == t.id` always compares a variable to itself and is always `true`. `vm` therefore always resolves to `viewModels[0]` — **every** ASN in a multi-select confirm/unload batch silently gets the *first* row's `arrival_time` (`ConfirmAsync`) or `unload_time`/`unload_person` (`UnloadAsync`), not its own. This is exactly the kind of bug a test with ≥2 distinct rows in one batch call will catch immediately — expect sub-phase 4c to fix it, the same way Phase 2 fixed the `CategoryService.GetChildren` and `SpuService.UpdateAsync` bugs it found.
- **`FunctionHelper.GetFormNoAsync(string table_name, ...)` doesn't take a `tenant_id` parameter** — it calls its own `GetCurrentUser()`, which re-derives the tenant from `IHttpContextAccessor`'s JWT rather than trusting the `CurrentUser` already passed into the calling `AsnService` method. With no HTTP context (the normal case in these tests), `GetCurrentUser()` returns a default `CurrentUser` (`tenant_id == 1`), regardless of what tenant the test passes to `AsnService.AddAsync`/`SortingAsync`. Every asn-number/series-number assertion in 4a and 4d needs to either use tenant 1, or fake `IHttpContextAccessor` to return a real JWT for other tenants — call this out explicitly in each of those sub-phases rather than rediscovering it mid-test. `GetFormNoListAsync` (the method underneath) already takes `tenant_id` as a parameter, so this is purely `GetFormNoAsync`'s wrapper not forwarding it — see the refactor proposal below.
- **Dead code:** `AsnService.GetOrderCode(CurrentUser)` is public but not declared on `IAsnService` and never called from anywhere (confirmed by grep) — the same dead method exists verbatim in `DispatchlistService`, `StockfreezeService`, `StockmoveService`, `StockprocessService`, `StocktakingService`. Not testing it; flagging it as a cleanup candidate whenever someone's touching one of those files anyway.

### Sub-phases

Split along the file's own `#region` boundaries — `Api` and `Arrival list` are already CRUD-shaped like Phases 1-3; `Flow Api` (11 methods, the actual state machine) is split further into the three natural stages an ASN moves through, so each slice's fixtures build on "an ASN that has already reached this stage" without needing the *code* from the previous slice to exist yet.

| Sub-phase | Scope | Notes |
|---|---|---|
| **4a** | `Api` region: `PageAsync`, `GetAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`, `BulkModifyGoodsownerAsync` | Lowest risk, same shape as every phase so far (3-way join read, simple write). `DeleteAsync` has real logic beyond a plain delete — it decrements `asn_status` by one instead of deleting once `asn_status > 0`, and refuses outright at `asn_status == 8` — worth a test per branch. Establishes the `Spu`/`Sku`/`Asnmaster` seeding helpers the later sub-phases reuse. |
| **4b** | `Arrival list` region: `PageAsnmasterAsync`, `GetAsnmasterAsync`, `AddAsnmasterAsync`, `UpdateAsnmasterAsync`, `DeleteAsnmasterAsync` | Same add/update/remove-by-negative-id detail-list reconciliation shape already covered in Phase 2 (`SpuService.UpdateAsync`) and Phase 3 (`RoleMenuService.UpdateAsync`) — should move quickly. `DeleteAsnmasterAsync` cascades to delete every child `Asn` row first; no referenced-by-guard exists (unlike `Category`/`Customer` in earlier phases) — confirm that's actually the intended behavior before just asserting it. |
| **4c** | Confirm/unload pair: `ConfirmAsync`, `ConfirmCancelAsync`, `UnloadAsync`, `UnloadCancelAsync` | Where the `t.id == t.id` shadowing bug lives (see above) — a batch test with 2+ distinct rows is both the regression test and the bug repro. Each method also has a from-status precondition (`Confirm` requires `asn_status == 0`, `Unload` requires `<= 1`, etc.) worth one test per rejected precondition. |
| **4d** | Sorting sub-flow: `SortingAsync`, `GetAsnsortsAsync`, `ModifyAsnsortsAsync`, `SortedAsync`, `SortedCancelAsync`, `GetAsnPrintSeriesNumberAsync` | Introduces `AsnsortEntity` and exercises `FunctionHelper.GetFormNoListAsync` for the multi-unit serial-number case (`sorted_qty > 1 && is_auto_num`) as well as the single-number case — both branches in `SortingAsync` need a test. `GetAsnPrintSeriesNumberAsync` is a trivial read tacked on here since it also joins against `Asnsort`. |
| **4e** | Putaway sub-flow: `GetPendingPutawayDataAsync`, `PutAwayAsync` | Highest-stakes slice — `PutAwayAsync` is the one method in this whole service that actually creates/increments `StockEntity` rows, which is exactly the kind of change CLAUDE.md flags as needing multi-service consistency (`Stock`/`Stockmove`/etc.). At ~90 lines with a nested nested `foreach` and a 7-column composite match-or-create key on `Stock`, this is the single most complex method in the file — plan for more tests here than any other sub-phase, and consider whether it needs its own follow-up slice if it turns out to be even more involved once real tests are written against it. |

Each sub-phase is its own reviewable chunk (own commit, own confirmation) — not one big Phase 4 PR.

### Suggested refactor — proposal only, nothing below has been implemented

§4 below ("Explicitly out of scope") rules out refactoring for testability *in general*, on the reasoning that the SQLite-backed test harness already makes services testable without mocking seams. That reasoning still holds for `AsnService` — every sub-phase above is testable today, as-is, the same way every earlier phase was. The case for refactoring here is different: **`AsnService` is one 1423-line class doing four distinct jobs**, and that size is what makes it worth breaking into smaller *pieces*, not smaller *test files covering one big piece*. Two options, smallest-diff first:

**Option A — extract small private helper methods, no structural change.** Pull the pure status-transition checks (e.g. "can this ASN be confirmed" ⇔ `asn_status == 0`) out of the inline `entities.Any(t => ...)` LINQ predicates into named private methods/constants on the same class (e.g. `private const byte PreDeliveryStatus = 0;` or `private static bool CanConfirm(AsnEntity e) => e.asn_status == PreDeliveryStatus;`). No file moves, no interface changes, no controller changes. This mainly buys readability and gives the state machine's rules names — it doesn't reduce `AsnService`'s size or the number of dependencies a test for any one method has to pull in, since it's still one class either way.

**Option B — split `AsnService` into sibling services along its own existing `#region` boundaries** (this also maps one-to-one onto the sub-phases above, so each new test file would cover exactly one new service class):
- `AsnService` (trimmed to just the `Api` region: 4a's six methods)
- `AsnmasterService` (the `Arrival list` region: 4b's five methods)
- `AsnConfirmService` (4c's four methods)
- `AsnSortingService` (4d's six methods, including the dead-simple `GetAsnPrintSeriesNumberAsync`)
- `AsnPutawayService` (4e's two methods)

Each becomes its own `IXxxService : IBaseService<...>`, auto-registered by the existing reflection-based DI convention with **zero new registration code** (see CLAUDE.md's "Convention-driven registration" — this is exactly the scenario that convention was built for). `AsnController` would inject the relevant services per action instead of one `IAsnService`, which is a mechanical, behavior-preserving change but touches every action method in the controller (478 lines) — a real, reviewable diff, not a trivial one. This is the option that actually makes `AsnService` itself smaller and lowers the dependency footprint of each resulting test file, at the cost of a larger one-time restructuring diff before any Phase 4 test is written.

Recommendation if asked: **Option B, done as its own commit before 4a starts**, specifically because the sub-phase boundaries were *already* chosen to match the file's own region comments — the seams exist; this just makes them physical.

### Option B — done (not yet committed)

Implemented as a pure move: no behavior changes, no tenant-filter fixes, no bug fixes (the `t.id == t.id` shadowing bug above was moved into `AsnConfirmService` verbatim — fixing it is still deferred to sub-phase 4c, same as originally planned).

- `AsnService` trimmed to the `Api` region's six methods (`PageAsync`, `GetAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`, `BulkModifyGoodsownerAsync`). Its dead `GetOrderCode` method (see above — unreferenced anywhere) was dropped in the same pass; the identical dead method in `DispatchlistService`/`StockfreezeService`/`StockmoveService`/`StockprocessService`/`StocktakingService` was left alone since those files weren't otherwise touched.
- New `AsnmasterService` (`IAsnmasterService : IBaseService<AsnmasterEntity>`): the `Arrival list` region's five methods — maps to sub-phase 4b.
- New `AsnConfirmService` (`IAsnConfirmService : IBaseService<AsnEntity>`): `ConfirmAsync`/`ConfirmCancelAsync`/`UnloadAsync`/`UnloadCancelAsync` — maps to 4c.
- New `AsnSortingService` (`IAsnSortingService : IBaseService<AsnsortEntity>`): `SortingAsync`/`GetAsnsortsAsync`/`ModifyAsnsortsAsync`/`SortedAsync`/`SortedCancelAsync`/`GetAsnPrintSeriesNumberAsync` — maps to 4d.
- New `AsnPutawayService` (`IAsnPutawayService : IBaseService<AsnEntity>`): `GetPendingPutawayDataAsync`/`PutAwayAsync` — maps to 4e.
- `AsnController` now injects all five services instead of one `IAsnService`, and each action was repointed to the service that now owns it; routes are unchanged.
- No DI registration code was needed anywhere — confirmed the reflection-based convention (`StartupExtensions.RegisterAssembly`) picks up all four new interface/class pairs automatically, the same way it already does for every other service. Verified by actually running the app (`dotnet run`) and hitting `GET /asn?id=1` and `GET /asn/asnmaster?id=1` against the real checked-in `wms.db` — both returned real data, confirming `AsnController`'s five-service constructor resolves cleanly and each split service still reads/writes correctly, not just that it compiles.
- `dotnet build ModernWMS.sln` → 0 errors. `dotnet test ModernWMS.sln` → still 165 passed, 0 failed (no Asn tests exist yet — this only confirms nothing else regressed).

Sub-phases 4a-4e can now proceed against the split services.

### Sub-phase 4a — done

Covered `AsnService`'s six `Api`-region methods, 16 tests (181 total): tenant-scoped `PageAsync` including both `sqlTitle` filter branches (`asn_status:<n>` exact match and `asn_status:alltodo` → `<= 3`), `GetAsync`'s multi-join projection (`Asn` × `Asnmaster` × `Spu` × `Sku`), `AddAsync`'s audit stamping and `FunctionHelper`-generated `asn_no`, `UpdateAsync`'s not-found/success paths, `DeleteAsync`'s three-way branch (`asn_status == 0` deletes the row outright, `== 8` is blocked, anything else just decrements the status by one instead of deleting), `BulkModifyGoodsownerAsync`, and cross-tenant regression tests for all four by-id/by-id-list operations.

Extended the Issue 1 tenant-filter fix (see `_docs/issue-logs.md`) to `GetAsync`/`UpdateAsync`/`DeleteAsync`/`BulkModifyGoodsownerAsync` — the same gap flagged above, now closed for this slice; the remaining ~11 Flow Api methods (4c/4d/4e) still have it, to be fixed as each of those sub-phases lands.

Added two more `TestSupport` helpers needed once a test actually calls `FunctionHelper`: `NullHttpContextAccessor` (an `IHttpContextAccessor` that always reports no `HttpContext`) and `TestFunctionHelperFactory.Create(dbContext)` (builds a real `FunctionHelper` against a test `SqlDBContext` with that fake accessor) — this is exactly the "no HTTP context ⇒ `GetCurrentUser()` returns `tenant_id == 1`" behavior flagged above, now concretely wired into the harness rather than just described.

One test-setup gotcha, not a production bug: `AsnEntity` has a required `Asnmaster` navigation property (non-nullable, so EF Core treats it as a real FK constraint), so `AddAsync` needs a genuinely existing `asnmaster_id` — a bare `new AsnViewModel()` with `asnmaster_id == 0` throws a SQLite FK-constraint `DbUpdateException`. Fixed by seeding a real `Spu`/`Sku`/`Asnmaster` trio first (`SeedAsnPrerequisitesAsync`), the same shape as the `AsnEntity ↔ AsnmasterEntity` FK gotcha hit and fixed during Phase 2/3 exploration.

`dotnet test _tests/MWMS.UnitTests/ModernWMS.UnitTests.csproj` → **181 passed**, 0 failed. `dotnet build ModernWMS.sln` → 0 errors.

### Sub-phase 4b — done

Covered `AsnmasterService`'s five `Arrival list`-region methods, 10 tests (191 total): tenant-scoped `PageAsnmasterAsync` (including the `asn_status:<n>` filter) and `GetAsnmasterAsync`'s detail-list projection, `AddAsnmasterAsync`'s audit stamping / `FunctionHelper`-generated `asn_no` / child-`Asn`-row creation, `UpdateAsnmasterAsync`'s not-found path and its add/update/remove-by-negative-id detail-list reconciliation (same shape already covered for `Spu`/`RoleMenu` in Phases 2/3), `DeleteAsnmasterAsync`'s cascade-delete of child `Asn` rows, and cross-tenant regression tests for `GetAsnmasterAsync`/`UpdateAsnmasterAsync`/`DeleteAsnmasterAsync`.

Extended the Issue 1 tenant-filter fix to `UpdateAsnmasterAsync` (entity fetch had no tenant check) and `DeleteAsnmasterAsync` (no `CurrentUser` parameter at all) — the latter needed the same "check ownership before touching children" guard as `SpuService`/`CategoryService` in earlier phases, since it deletes every child `Asn` row before the `Asnmaster` row itself.

`dotnet test _tests/MWMS.UnitTests/ModernWMS.UnitTests.csproj` → **191 passed**, 0 failed. `dotnet build ModernWMS.sln` → 0 errors.

### Sub-phase 4c — done

Covered `AsnConfirmService`'s four methods, 14 tests (205 total): each method's from-status precondition check (`ConfirmAsync` requires `asn_status == 0`, `ConfirmCancelAsync` requires `== 1`, `UnloadAsync` requires `<= 1`, `UnloadCancelAsync` requires `== 2`), `UnloadAsync`'s "default to the calling user when `unload_person_id == 0`" branch, and cross-tenant regression tests for all four.

**Fixed the shadowing bug flagged while planning this sub-phase** (see Issue 5 in `_docs/issue-logs.md`): `ConfirmAsync`/`UnloadAsync` compared a lambda parameter to itself (`t.id == t.id` inside a nested lambda that shadowed the outer `t`), so every row in a multi-select batch silently got the *first* row's `arrival_time`/`unload_time`/`unload_person`. `ConfirmAsync_MultipleRows_EachGetsItsOwnArrivalTime` and `UnloadAsync_MultipleRows_EachGetsItsOwnUnloadData` seed two distinct rows in one batch call specifically to catch this — a single-row test would have passed against the buggy code too.

Extended the Issue 1 tenant-filter fix to all four methods — none of them took a `CurrentUser` at all before this (`UnloadAsync` already had one, for the "default unload person" fallback, but never used it to scope the query).

`dotnet test _tests/MWMS.UnitTests/ModernWMS.UnitTests.csproj` → **205 passed**, 0 failed. `dotnet build ModernWMS.sln` → 0 errors.

### Sub-phase 4d — done

Covered `AsnSortingService`'s six methods, 20 tests (225 total): `SortingAsync`'s both branches (`sorted_qty > 1 && is_auto_num` → one `Asnsort` row per unit via `FunctionHelper.GetFormNoListAsync`, vs. the single-row branch via `GetFormNoAsync`), `GetAsnsortsAsync`, `ModifyAsnsortsAsync`'s delete-by-negative-id and update-by-positive-id branches (each also recomputing the parent `Asn.sorted_qty`), `SortedAsync`'s `more_qty`/`shortage_qty` branches, `SortedCancelAsync`'s two distinct block conditions (`actual_qty > 0` vs. `sorted_qty < 1`) plus its cascade-delete of `Asnsort` rows, `GetAsnPrintSeriesNumberAsync`, and cross-tenant regression tests for all six.

This was the largest single tenant-filter fix of the four Phase 4 sub-phases so far — all six methods lacked it, and `ModifyAsnsortsAsync` needed a different shape of fix than every prior instance of Issue 1: it operates on a raw client-supplied `List<AsnsortEntity>` (ids and values straight from the request body, not fetched from the DB first), used for both a delete-by-id and a blind attach-and-`UpdateRange`. The delete got the usual filter added to its `Where`; the update needed an extra pre-check — query which of the requested ids actually belong to the caller's tenant, then drop the rest from the list before it's ever attached — since there was no existing DB fetch to add a filter onto. Full writeup in `_docs/issue-logs.md`.

`dotnet test _tests/MWMS.UnitTests/ModernWMS.UnitTests.csproj` → **225 passed**, 0 failed. `dotnet build ModernWMS.sln` → 0 errors.

### Sub-phase 4e — done (Phase 4 complete)

Covered `AsnPutawayService`'s two methods, 15 tests (240 total): `GetPendingPutawayDataAsync`'s "remaining sorted qty" grouping (and that fully-put-away rows are excluded), and `PutAwayAsync`'s full validation chain (missing/unknown/cross-tenant location, unknown/wrong-status/over-quota `Asn`), its stock-upsert-by-composite-key logic (new row vs. increment an existing matching one), the `asn_status → 4` transition once `actual_qty` reaches `sorted_qty`, the damage-location (`warehouse_area_property == 5`) branch, the series-number-matched `Asnsort.putaway_qty` writeback, and cross-tenant regression tests for both the `Asn` and the `Goodslocation` checks (kept as two separate tests specifically so each isolates its own check rather than one masking the other).

Extended the Issue 1 tenant-filter fix to `GetPendingPutawayDataAsync` (no `CurrentUser` at all) and `PutAwayAsync`'s `Asn` fetch. Also caught a second, distinct gap in the same method: the `GoodslocationEntity` rows named by client-supplied `goods_location_id`s had no tenant filter either — the first instance in Phase 4 where the missing check was on a *referenced* entity rather than the primary one the method operates on. Full writeup in `_docs/issue-logs.md`.

**This completes Phase 4.** All five sub-phases (4a-4e) are done: `AsnService` (16 tests), `AsnmasterService` (10), `AsnConfirmService` (14, including the fixed shadowing bug), `AsnSortingService` (20), `AsnPutawayService` (15) — 75 tests total for the `Asn` module, on top of the Option B structural split.

`dotnet test _tests/MWMS.UnitTests/ModernWMS.UnitTests.csproj` → **240 passed**, 0 failed. `dotnet build ModernWMS.sln` → 0 errors. **Not committed yet** — waiting for confirmation, per standing instruction, before any commit.

## 4. Explicitly out of scope for this plan

- CI wiring (no `.github/workflows` exists today) — separate decision.
- Refactoring services to introduce interfaces/mocking seams purely for testability — would contradict CLAUDE.md's anti-abstraction guidance; only reconsider if a specific phase proves genuinely un-testable without it.
- `ModernWMS.Core` infrastructure itself (JWT, Hangfire jobs, Swagger, middleware) — different risk profile from the WMS domain logic, would need its own plan.
- Load/performance testing, migrations testing, frontend testing.
