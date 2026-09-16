# Issue Log

Bugs found while writing Phase 0/1 unit test coverage (see `unit-testing-plan.md`), and how they were fixed. Each entry: what was wrong, why it matters, what changed, and how it's verified.

---

## Issue 1 — `GetAsync`/`UpdateAsync`/`DeleteAsync`-by-id had no `tenant_id` filter

**Severity:** High (cross-tenant data leak / cross-tenant write-and-delete).

**Found in:** `WarehouseService`, `WarehouseareaService`, `GoodslocationService`, `GoodsownerService`, `CustomerService`, `SupplierService`, `CompanyService`, `SpuService`.

### What was wrong

Every module's paged/list queries (`PageAsync`, `GetAllAsync`) correctly filter by `t.tenant_id.Equals(currentUser.tenant_id)`. But the single-record operations — `GetAsync(int id)`, `UpdateAsync(viewModel, ...)`'s entity fetch, and `DeleteAsync(int id)` — matched only on `t.id.Equals(id)`, with no tenant check at all. Since primary keys are global auto-increment integers (one shared sequence per table, not scoped per tenant), any authenticated user who could guess or enumerate an id could:

- **Read** another tenant's warehouse / warehouse area / goods location / goods owner / customer / supplier / company / SPU record via `GetAsync`.
- **Overwrite** it via `UpdateAsync` (the id-based lookup would find it regardless of which tenant it belonged to).
- **Delete** it via `DeleteAsync`.

This matches the exact warning already in `CLAUDE.md`'s "Cross-cutting rules" section — *"omitting the filter in a new query is a data leak, not a style slip"* — it just turned out the leak already existed on this whole class of by-id operations across the reference-data services, before this pass started.

### Why it existed

`GoodsownerService.UpdateAsync`, `CustomerService.UpdateAsync`, and `CompanyService.UpdateAsync` didn't even take a `CurrentUser` parameter — there was no tenant to check against without changing the method signature. The other five services' `UpdateAsync` did take `CurrentUser` (for the duplicate-name check) but simply never used it to scope the entity lookup. `GetAsync`/`DeleteAsync` across all eight services took only an `id`.

### Fix

For each of the eight services:
- `GetAsync(int id)` → `GetAsync(int id, CurrentUser currentUser)`, with `&& t.tenant_id == currentUser.tenant_id` added to the lookup.
- `UpdateAsync(viewModel[, currentUser])` → now always takes `CurrentUser currentUser`, with the same tenant check added to the entity fetch. (`GoodsownerService`, `CustomerService`, `CompanyService` gained the parameter; the other five already had it and just needed the filter added.)
- `DeleteAsync(int id)` → `DeleteAsync(int id, CurrentUser currentUser)`, with the same tenant check added to the delete predicate.
- `SpuService.DeleteAsync` needed one extra step beyond a simple filter: it deletes child `SkuEntity` rows (which have no `tenant_id` of their own) by `spu_id` before deleting the parent `SpuEntity`. Adding the tenant filter only to the final `SpuEntity` delete would have left a gap where a cross-tenant call still wiped out the child `Sku` rows even though the parent `Spu` survived. Fixed by checking the `Spu` belongs to the caller's tenant *before* touching any child rows, and returning `delete_failed` immediately if not.
- Every interface (`IWarehouseService`, `IWarehouseareaService`, `IGoodslocationService`, `IGoodsownerService`, `ICustomerService`, `ISupplierService`, `ICompanyService`, `ISpuService`) updated to match.
- Every controller call site (`WarehouseController`, `WarehouseareaController`, `GoodslocationController`, `GoodsownerController`, `CustomerController`, `SupplierController`, `CompanyController`, `SpuController`) updated to pass `CurrentUser` (already available on every controller via `BaseController`).
- No other internal callers existed (confirmed by grepping the whole solution for calls to these methods outside the `Controllers/` folders) — the fix has no other ripple effect.

### Verification

Added one cross-tenant regression test per service per operation (`GetAsync_BelongsToDifferentTenant_Returns…`, `UpdateAsync_BelongsToDifferentTenant_ReturnsNotExists`, `DeleteAsync_BelongsToDifferentTenant_DoesNotDelete`) — 24 new tests across `WarehouseServiceTests`, `WarehouseareaServiceTests`, `GoodslocationServiceTests`, `GoodsownerServiceTests`, `CustomerServiceTests`, `SupplierServiceTests`, `CompanyServiceTests`, `SpuServiceTests`. Each seeds a row under tenant 1 and calls the service as tenant 2, asserting the row is invisible/unmodifiable/undeletable from tenant 2. All pass against the fixed code (and would fail against the pre-fix code, since the tenant check is exactly what makes them pass).

`dotnet test ModernWMS.sln` → 79 passed, 0 failed. `dotnet build ModernWMS.sln` → 0 errors.

---

## Issue 2 — `SupplierService.AddAsync` ran its duplicate-name check after staging the new entity

**Severity:** Low today; latent-footgun for later changes to this request path.

**Found in:** `SupplierService.AddAsync`.

### What was wrong

Every sibling `AddAsync` (Warehouse, Warehousearea, Goodslocation, GoodsOwner, Customer, Company, Spu) checks for a duplicate name *first* and returns early if found, only building/staging the new entity afterward. `SupplierService.AddAsync` did it backwards: it called `DbSet.AddAsync(entity)` — which stages the entity in the `SqlDBContext`'s change tracker as `Added` — and only *then* ran the duplicate-name `AnyAsync` check. If a duplicate was found, the method returned `(0, msg)` without ever calling `SaveChangesAsync()`, so nothing was written to the database — but the abandoned `Added` entity was left sitting in the change tracker.

This caused no visible bug by itself, because the duplicate check queries the database (not the change tracker) and the call always returns before `SaveChangesAsync()`. But `SqlDBContext` is request-scoped (standard ASP.NET Core DI scoping) — if any *other* write happened later in the same HTTP request on the same `SqlDBContext` and called `SaveChangesAsync()`, the abandoned Supplier entity would get persisted along with it, as an unintended side effect of an unrelated save.

### Fix

Reordered `SupplierService.AddAsync` to check for the duplicate name first and only build/stage the entity if none was found — matching every other module's `AddAsync`.

### Verification

`SupplierServiceTests.AddAsync_DuplicateNameInSameTenant_IsRejectedWithoutPersisting` already asserted the duplicate call leaves exactly one row in the table; it continues to pass after the reorder. No new test was needed to prove the abandoned-tracked-entity risk is gone, since that risk was about ordering (now eliminated by construction) rather than an observable output — the existing assertion on row count is what would have caught a regression either way.

`dotnet test ModernWMS.sln` → 79 passed, 0 failed.
