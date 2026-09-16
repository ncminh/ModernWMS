# Issue Log

Bugs found while writing unit test coverage (see `unit-testing-plan.md`), and how they were fixed. Each entry: what was wrong, why it matters, what changed, and how it's verified.

---

## Issue 1 — `GetAsync`/`UpdateAsync`/`DeleteAsync`-by-id had no `tenant_id` filter

**Severity:** High (cross-tenant data leak / cross-tenant write-and-delete).

**Found in:** `WarehouseService`, `WarehouseareaService`, `GoodslocationService`, `GoodsownerService`, `CustomerService`, `SupplierService`, `CompanyService`, `SpuService` (Phase 1); `CategoryService`, `FreightfeeService`, `PrintSolutionService` (Phase 2); `UserService`, `UserroleService`, `RolemenuService` (Phase 3); `AsnService`'s `Api` region (Phase 4, sub-phase 4a); `AsnmasterService`'s `UpdateAsnmasterAsync`/`DeleteAsnmasterAsync` (Phase 4, sub-phase 4b); `AsnConfirmService` — all four methods (`ConfirmAsync`, `ConfirmCancelAsync`, `UnloadAsync`, `UnloadCancelAsync`), none of which took a tenant check at all before this (Phase 4, sub-phase 4c); `AsnSortingService` — all six methods (`SortingAsync`, `GetAsnsortsAsync`, `ModifyAsnsortsAsync`, `SortedAsync`, `SortedCancelAsync`, `GetAsnPrintSeriesNumberAsync`), the largest single slice of this defect found in one service (Phase 4, sub-phase 4d); `AsnPutawayService` — `GetPendingPutawayDataAsync` (no `CurrentUser` at all) and `PutAwayAsync`'s `Asn` fetch (Phase 4, sub-phase 4e, completing Phase 4); `DispatchConfirmService.ConfirmOrder`'s `dispatchlist_datas` fetch (Phase 5's Option B split); `DispatchDeliveryService.Package`/`Weight`/`Delivery`'s entity fetches (Phase 5's Option B split); `DispatchConfirmService.GetPickListByDispatchID` (no `CurrentUser` at all) and `CancelDispatchlistDetailOpration` (no `CurrentUser` at all), plus `ConfirmPickDetail`/`CancelConfirmPickDetail` (both already took `CurrentUser` but never used it to scope the query — same shape as Phase 1's `GoodsownerService`-family gap) (Phase 5, sub-phase 5b); `DispatchDeliveryService.SetFreightfee` and `SignForArrival` (neither took `CurrentUser` at all) (Phase 5, sub-phase 5c) — same defect every time, found and fixed the same way as each phase touched these services.

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
- Every interface (`IWarehouseService`, `IWarehouseareaService`, `IGoodslocationService`, `IGoodsownerService`, `ICustomerService`, `ISupplierService`, `ICompanyService`, `ISpuService`; Phase 2's `ICategoryService`, `IFreightfeeService`, `IPrintSolutionService`; Phase 3's `IUserService`, `IUserroleService`, `IRolemenuService`) updated to match.
- Every controller call site (`WarehouseController`, `WarehouseareaController`, `GoodslocationController`, `GoodsownerController`, `CustomerController`, `SupplierController`, `CompanyController`, `SpuController`, `CategoryController`, `FreightfeeController`, `PrintSolutionController`, `UserController`, `UserroleController`, `RolemenuController`) updated to pass `CurrentUser` (already available on every controller via `BaseController`).
- `CategoryService.DeleteAsync` needed the same "guard before touching children" shape as `SpuService`: it also fans out to delete descendant categories via `GetChildren` (see Issue 3), so the tenant check was added to the final `ExecuteDeleteAsync` predicate rather than trying to re-derive tenant ownership for every descendant id individually — consistent with how the sibling `Warehouse`/`Warehousearea` delete-cascade checks were handled in Phase 1.
- **Phase 3 also caught two variants beyond plain `GetAsync`/`UpdateAsync`/`DeleteAsync`:** `UserService.ResetPwd` batch-resets passwords for a client-supplied `id_list` with no tenant filter at all — fixed by threading `CurrentUser` in and adding the same tenant check to that query. `RolemenuService.GetAsync`/`GetMenusByRoleId` are keyed by `userrole_id`, not `id`, and had no tenant check either (not even a `CurrentUser` parameter on `GetAsync`/`GetMenusByRoleId`/`DeleteAsync`) — fixed the same way, filtering on `RolemenuEntity.tenant_id` directly (it already carries its own `tenant_id`, so no join to `Userrole` was needed for the check). `UserService.ChangePwd` was deliberately **not** touched — it's gated by a client-supplied old-password match rather than being a plain by-id lookup, so it doesn't fit this pattern the same way; flagging it here rather than silently declaring it fixed.
- No other internal callers existed for any of the services touched by this issue (confirmed by grepping the whole solution for calls to these methods outside the `Controllers/` folders) — the fix has no other ripple effect.
- `AsnService.GetAsync`'s query is a multi-join projection (`Asn` × `Asnmaster` × `Spu` × `Sku`) with no `where` clause at all before this fix (unlike its sibling `PageAsync`, which already filtered by tenant) — the fix added `where m.tenant_id == currentUser.tenant_id` to the query itself, matching `PageAsync`'s existing pattern, rather than filtering after projection.
- `AsnmasterService.DeleteAsnmasterAsync` needed the same "guard before touching children" shape as `SpuService.DeleteAsync`/`CategoryService.DeleteAsync`: it deletes every child `Asn` row (by `asnmaster_id`) before deleting the `Asnmaster` row itself, so the fix checks the `Asnmaster` belongs to the caller's tenant *before* touching any child rows, returning `delete_failed` immediately if not — otherwise a cross-tenant call could still wipe out another tenant's `Asn` detail rows even while the tenant check on the final `Asnmaster` delete blocked deleting the parent.
- `AsnSortingService.ModifyAsnsortsAsync` is the trickiest variant of this fix so far: `entities` is a raw, client-supplied `List<AsnsortEntity>` (ids and field values straight from the request body), used to both `ExecuteDeleteAsync` (by negative id) and blind-`UpdateRange` (attaching the client-supplied objects directly and marking them `Modified`, rather than fetching real rows first). The delete got the usual `&& tenant_id == user.tenant_id` added to its `Where`. The update needed an extra step, since there was nothing fetched from the DB to filter by tenant in the first place: before attaching, the fix now queries which of the requested update ids actually belong to the caller's tenant, and silently drops the rest from `updateEntities` before `UpdateRange` ever touches them. The final "recompute `Asn.sorted_qty` from the sum of its `Asnsort` rows" step also got the tenant filter added to its `Asn` fetch, so a cross-tenant `asn_id` slipped into the request body doesn't cause its `Asn.sorted_qty` to be recomputed either.
- `AsnPutawayService.PutAwayAsync` had a second, distinct flavor of this gap beyond its own `Asn` fetch: the `GoodslocationEntity` rows named by the client-supplied `goods_location_id`s (the request body's putaway destinations) were fetched with no tenant filter either — `Goodslocations.Where(t => LocationIdList.Contains(t.id))`. A tenant-A caller who supplied a `goods_location_id` belonging to tenant B would have had `StockEntity` rows written into tenant B's warehouse location. This is the first instance in Phase 4 where the missing check was on a *referenced* entity (a location named by id in the request), not the primary entity the method operates on — fixed by adding `&& t.tenant_id == currentUser.tenant_id` to that fetch too, so an out-of-tenant location id now fails the existing "all requested locations must exist" check instead of silently resolving to someone else's location.
- **Phase 5's Option B split fixed this ahead of any sub-phase test** (unlike every phase above, where the fix landed alongside the sub-phase's regression test): `DispatchConfirmService.ConfirmOrder`'s `dispatchlist_datas` fetch (`DBSet.Where(t => dispatchlist_id_list.Contains(t.id))`, straight off a client-supplied id list) gained `&& t.tenant_id == currentUser.tenant_id`; `DispatchDeliveryService.Package`/`Weight`/`Delivery` each had the identical shape on their `entities` fetch and got the same fix. These were fixed during the structural split itself because reading the code made the gap obvious without needing a test to surface it first — the actual cross-tenant regression tests landed with sub-phase 5b (`ConfirmOrder`); 5c (`Package`/`Weight`/`Delivery`) is still pending.
- **Sub-phase 5b found three more variants while writing its own tests** (fixed alongside their regression tests, the normal sequence for this issue): `GetPickListByDispatchID` had no `CurrentUser` parameter at all — any caller could read another tenant's pick-list detail (location, sku, series number, price) for any `dispatchlist_id` they could guess. Fixed by adding `CurrentUser currentUser`, joining to `DispatchlistEntity` for the first time in this query, and filtering on `dl.tenant_id == currentUser.tenant_id`. `CancelDispatchlistDetailOpration(int id)` also had no `CurrentUser` at all — any caller could revert another tenant's package/weight state by guessing a `dispatchlist_id`. Fixed by adding the parameter and the usual `&& t.tenant_id == currentUser.tenant_id` on the fetch. `ConfirmPickDetail`/`CancelConfirmPickDetail` are the "parameter present but unused" variant already seen in Phase 1's `GoodsownerService` family: both already took `CurrentUser` (to stamp `picker`/`picker_id`) but the `pick_DBSet.Where(t => picklist_id.Contains(t.id))` fetch never checked it. `DispatchpicklistEntity` carries no `tenant_id` of its own, so there was nothing to filter on directly — fixed by joining to its parent `DispatchlistEntity` (via `dispatchlist_id`) and filtering on the parent's `tenant_id`, the same "join to the owning entity" shape as `AsnSortingService.ModifyAsnsortsAsync`'s fix. Cross-tenant ids are silently dropped from the working set rather than erroring, matching that same precedent.
- **Sub-phase 5c found the same "no `CurrentUser` at all" shape on the last two untouched methods in the file**, closing out the `Dispatchlist` module: `SetFreightfee` and `SignForArrival` — the only two methods in the pre-split `DispatchlistService` that never took a `CurrentUser` (their controller actions didn't pass one either, since there was nothing to pass). Both were fixed the same way as `CancelDispatchlistDetailOpration` above: add the parameter, add `&& t.tenant_id == currentUser.tenant_id` to the `entities` fetch, thread it through the interface and the two controller call sites.

### Verification

Added one cross-tenant regression test per service per operation (`GetAsync_BelongsToDifferentTenant_Returns…`, `UpdateAsync_BelongsToDifferentTenant_ReturnsNotExists`, `DeleteAsync_BelongsToDifferentTenant_DoesNotDelete`) — 24 tests in Phase 1 across `WarehouseServiceTests`, `WarehouseareaServiceTests`, `GoodslocationServiceTests`, `GoodsownerServiceTests`, `CustomerServiceTests`, `SupplierServiceTests`, `CompanyServiceTests`, `SpuServiceTests`; 9 more in Phase 2 across `CategoryServiceTests`, `FreightfeeServiceTests`, `PrintSolutionServiceTests`; 6 more in Phase 3 across `UserServiceTests`, `UserRoleServiceTests`, `RoleMenuServiceTests` (plus `UserServiceTests.ResetPwd_UserBelongsToDifferentTenant_LeavesPasswordUnchanged` for the `ResetPwd` variant); 4 more in Phase 4's sub-phase 4a across `AsnServiceTests` (`GetAsync`/`UpdateAsync`/`DeleteAsync`/`BulkModifyGoodsownerAsync`); 3 more in sub-phase 4b across `AsnmasterServiceTests` (`GetAsnmasterAsync`/`UpdateAsnmasterAsync`/`DeleteAsnmasterAsync`, the last also asserting the child `Asn` rows survive an attempted cross-tenant delete); 4 more in sub-phase 4c across `AsnConfirmServiceTests` (`ConfirmAsync`/`ConfirmCancelAsync`/`UnloadAsync`/`UnloadCancelAsync`); 6 more in sub-phase 4d across `AsnSortingServiceTests` (`SortingAsync`/`GetAsnsortsAsync`/`ModifyAsnsortsAsync`/`SortedAsync`/`SortedCancelAsync`/`GetAsnPrintSeriesNumberAsync`); and 3 more in sub-phase 4e across `AsnPutawayServiceTests` (`GetPendingPutawayDataAsync`, plus `PutAwayAsync_LocationBelongsToDifferentTenant_IsRejected` and `PutAwayAsync_AsnBelongsToDifferentTenant_ReturnsNotExists`, which isolate the two separate tenant checks from each other). Each seeds a row under tenant 1 and calls the service as tenant 2, asserting the row is invisible/unmodifiable/undeletable from tenant 2. All pass against the fixed code (and would fail against the pre-fix code, since the tenant check is exactly what makes them pass).

`dotnet test ModernWMS.sln` → 240 passed, 0 failed (as of Phase 4's sub-phase 4e, completing Phase 4). `dotnet build ModernWMS.sln` → 0 errors.

Phase 5 additions: sub-phase 5b added `DispatchConfirmServiceTests.CancelDispatchlistDetailOpration_BelongsToDifferentTenant_ReturnsNotExists`, `GetPickListByDispatchID_BelongsToDifferentTenant_ReturnsEmpty`, `ConfirmPickDetail_BelongsToDifferentTenant_IsIgnored`, `CancelConfirmPickDetail_BelongsToDifferentTenant_IsIgnored`, and `ConfirmOrder_BelongsToDifferentTenant_ReturnsDataChanged` (the last proving the tenant filter added during the Option B split, ahead of any test). `dotnet test ModernWMS.sln` → 288 passed, 0 failed (as of sub-phase 5b). `dotnet build ModernWMS.sln` → 0 errors.

Sub-phase 5c added `DispatchDeliveryServiceTests.Package_BelongsToDifferentTenant_ReturnsDataChanged`, `Weight_BelongsToDifferentTenant_ReturnsDataChanged`, `Delivery_BelongsToDifferentTenant_ReturnsDataChanged` (all three proving the tenant filters added during the Option B split), and `SetFreightfee_BelongsToDifferentTenant_LeavesRowUntouched`/`SignForArrival_BelongsToDifferentTenant_LeavesRowUntouched` (proving the two brand-new `CurrentUser` parameters added in this sub-phase). This closes out Issue 1 for the entire `Dispatchlist` module — every method across all three split services now filters by tenant. `dotnet test ModernWMS.sln` → 306 passed, 0 failed (as of sub-phase 5c, completing Phase 5). `dotnet build ModernWMS.sln` → 0 errors.

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

`dotnet test ModernWMS.sln` → 79 passed, 0 failed (at the time this issue was fixed, in Phase 1).

---

## Issue 3 — `CategoryService.GetChildren` recursed on the wrong id, and `DeleteAsync` couldn't see past direct children

**Severity:** High (crashes the process — `StackOverflowException` cannot be caught — on ordinary data; found before it could ship, since a test surfaced it during Phase 2).

**Found in:** `CategoryService.GetChildren` (private helper used by both `UpdateAsync`'s is-valid cascade and `DeleteAsync`'s cascade-delete).

### What was wrong

```csharp
private void GetChildren(List<CategoryEntity> entities, int parentId, ref List<CategoryEntity> children)
{
    var data = entities.Where(t => t.parent_id == parentId).ToList();
    foreach (var item in data)
    {
        children.Add(item);
        if (entities.Any(t => t.parent_id.Equals(item.id)))
        {
            GetChildren(entities, item.parent_id, ref children); // bug: should be item.id
        }
    }
}
```

The recursive call passed `item.parent_id` instead of `item.id`. Every `item` in `data` was already filtered by `t.parent_id == parentId`, so `item.parent_id` **always equals** the `parentId` already passed into the current call — the recursive call is therefore always `GetChildren(entities, parentId, ref children)`, identical to the call already in progress.

The blast radius differed by caller because each passes a different candidate pool as `entities`:
- **`UpdateAsync`** (toggling `is_valid`) passes `entities = await DbSet.Where(t => t.parent_id > 0).ToListAsync()` — the *entire* non-root category table. For any category whose child itself has a child (a 3-level-deep chain), the recursion condition (`entities.Any(t => t.parent_id.Equals(item.id))`) is true, and the call recurses with identical arguments forever: **`StackOverflowException`**, which .NET cannot catch — it kills the process outright. Toggling `is_valid` on any category with an active 3-level subtree would have taken down the whole ASP.NET Core worker.
- **`DeleteAsync`** passes only the *direct children* of the id being deleted (`DbSet.Where(t => t.parent_id.Equals(id))`), so the buggy recursive call's condition was never true in practice (a flat list of siblings never contains another sibling's child) — no crash, but `GetChildren` could then never discover grandchildren either. Deleting a category with a 3-level-deep subtree silently deleted only the category and its direct children, leaving grandchildren behind as now-orphaned rows still referencing a `parent_id` that no longer exists.

### Fix

1. `GetChildren`: recurse on `item.id`, not `item.parent_id`.
2. `DeleteAsync`: changed its initial `entities` fetch from `Where(t => t.parent_id.Equals(id))` (direct children only) to `Where(t => t.parent_id > 0)` (the same "whole non-root table" pool `UpdateAsync` already used), so the now-correct recursion has the full candidate pool to traverse multiple levels.

### Verification

`CategoryServiceTests.UpdateAsync_TogglingInvalid_CascadesToDescendants` and `DeleteAsync_WithMultiLevelDescendants_DeletesWholeSubtree` both seed a 3-level hierarchy (root → child → grandchild) — exactly the shape that hung the old code — and assert the grandchild is updated/deleted too. Both pass against the fixed code; run standalone first (`dotnet test --filter FullyQualifiedName~CategoryServiceTests`) before folding into the full suite, since a regression here would have crashed the whole `dotnet test` process rather than reporting a clean failure.

`dotnet test ModernWMS.sln` → 122 passed, 0 failed (as of Phase 2). `dotnet build ModernWMS.sln` → 0 errors.

---

## Issue 4 — `SpuService.UpdateAsync` recomputed SKU volume with a `Math.Round` inside `ExecuteUpdateAsync`, which SQLite can't translate

**Severity:** High (breaks the feature entirely under the project's own default dev database).

**Found in:** `SpuService.UpdateAsync`.

### What was wrong

After saving SPU/SKU changes, the method recalculated every sibling SKU's `volume` in one bulk statement:

```csharp
await _dBContext.GetDbSet<SkuEntity>().Where(t => t.spu_id.Equals(entity.id))
    .ExecuteUpdateAsync(p => p.SetProperty(x => x.volume, x => Math.Round(x.lenght * dec * x.width * dec * x.height * dec, 3)));
```

`ExecuteUpdateAsync` translates its expression tree to a single SQL `UPDATE` statement. EF Core's SQLite provider cannot translate `System.Math.Round(decimal, int)` into SQL, so this throws `InvalidOperationException` ("could not be translated") wrapped in the call — **every single successful `UpdateAsync` call that actually persists a change** (`qty > 0`, which is the normal case) hits this line. Since `appsettings.Development.json` defaults `Database:db` to `SQLITE` and ships a checked-in `wms.db`, this isn't a rare-provider edge case — it's the path this repository's own default local setup takes, meaning SPU/SKU edits were broken in local dev.

### Fix

Replaced the bulk `ExecuteUpdateAsync` with fetch-compute-save: load the sibling `SkuEntity` rows into memory, compute `Math.Round(...)` in .NET (works identically regardless of provider), and let the normal change-tracked `SaveChangesAsync()` persist it. EF Core's identity map means rows already tracked from earlier in the same method (the ones just added/edited from `viewModel.detailList`) come back as the same tracked instances rather than duplicates, so there's no double-tracking conflict.

### Verification

`SpuServiceTests.UpdateAsync_NewDetailRow_IsAdded`, `UpdateAsync_ExistingDetailRow_IsUpdated`, and `UpdateAsync_NegativeDetailRowId_RemovesTheRow` all exercise this code path (each ends in a persisted change, so each was hitting the crash before the fix) and pass now.

`dotnet test ModernWMS.sln` → 122 passed, 0 failed. `dotnet build ModernWMS.sln` → 0 errors.

---

## Issue 5 — `AsnConfirmService.ConfirmAsync`/`UnloadAsync` compared a lambda variable to itself, so every row in a batch silently used the first row's data

**Severity:** High (silent data corruption on a common bulk operation — every multi-select confirm/unload was affected, not an edge case).

**Found in:** `AsnConfirmService.ConfirmAsync` and `UnloadAsync` (flagged while planning Phase 4 sub-phase 4c by reading the code; confirmed and fixed once tests were written for it).

### What was wrong

```csharp
entities.ForEach(t =>
{
    var vm = viewModels.FirstOrDefault(t => t.id == t.id);   // <- bug
    ...
});
```

The inner lambda's parameter `t` shadows the outer `t` from `entities.ForEach(t => ...)`. Inside the inner lambda, both `t.id` references resolve to the *inner* `t` (the `viewModels` element being tested), so the predicate is always `t.id == t.id` — always `true`, for every candidate — regardless of which outer `entities` row is currently being processed. `FirstOrDefault` therefore always returns `viewModels[0]`, no matter which `entities` row the outer `ForEach` is on.

Effect: confirming or unloading **more than one ASN in the same batch call** applied the *first* selected row's `arrival_time` (`ConfirmAsync`) or `unload_time`/`unload_person_id`/`unload_person` (`UnloadAsync`) to *every* row in the batch, silently overwriting whatever the user actually entered for the other rows. Single-row calls were unaffected (there's only one candidate, so the bug is unobservable), which is likely why this shipped — the bug only manifests on multi-select bulk actions.

### Fix

Renamed the inner lambda's parameter so it can no longer shadow the outer one, and fixed the comparison to actually compare the two: `viewModels.FirstOrDefault(v => v.id == t.id)` in both `ConfirmAsync` and `UnloadAsync`.

### Verification

`AsnConfirmServiceTests.ConfirmAsync_MultipleRows_EachGetsItsOwnArrivalTime` and `UnloadAsync_MultipleRows_EachGetsItsOwnUnloadData` each seed **two** distinct ASNs in one batch call with two distinct values, and assert each row keeps its own value rather than both collapsing onto the first row's — a single-row test would not have exercised this bug at all. Both pass against the fixed code (and would fail against the pre-fix code, since the bug's effect is exactly "second row's own value never gets applied").

`dotnet test ModernWMS.sln` → 205 passed, 0 failed. `dotnet build ModernWMS.sln` → 0 errors.

---

## Issue 6 — `DispatchConfirmService.ConfirmOrderCheck` compared a stock's `tenant_id` against the caller's `user_id`, not `tenant_id`

**Severity:** Critical (wrong field entirely, not just a missing filter — breaks the core stock-availability calculation for any deployment with more than one user).

**Found in:** `DispatchlistService.ConfirmOrderCheck` (found by reading the file while planning Phase 5's Option B split; fixed during the split itself, ahead of sub-phase 5b's tests — this bug was severe and obvious enough on reading that it didn't need a test to surface it).

### What was wrong

```csharp
var stock_group_datas = from stock in stock_DbSet.AsNoTracking()
                        join gl in _dBContext.GetDbSet<GoodslocationEntity>().AsNoTracking() on stock.goods_location_id equals gl.id
                        where stock.tenant_id == currentUser.user_id   // <- bug
                        group stock by ...
```

This method computes how much stock is actually available to satisfy a dispatch order — the whole reason it exists is to answer "can we confirm this order against real inventory?" The filter compares `StockEntity.tenant_id` against `currentUser.user_id`, two unrelated identifiers. Any caller whose `user_id` doesn't happen to numerically equal their own `tenant_id` (i.e. essentially always, since `tenant_id` identifies the organization and `user_id` identifies the person within it) gets a `stock_group_datas` result that's either **empty** (no stock rows happen to have `tenant_id == user_id`) or, worse, **includes some other tenant's stock** if another tenant's `tenant_id` happens to equal this caller's `user_id`. Either way, `qty_available` and the `confirm` flag downstream are computed from the wrong data — an order could be wrongly marked unconfirmable (false negative, blocking real business) or wrongly marked confirmable against stock that doesn't actually belong to the caller's tenant (cross-tenant leak feeding a real allocation decision).

### Fix

Changed the comparison to `stock.tenant_id == currentUser.tenant_id`, matching the tenant-scoping pattern used by every other query in the same method (the `dl.tenant_id == currentUser.tenant_id` check later in the same method was already correct — only this one `stock_group_datas` filter had the wrong field).

### Verification

Deferred to sub-phase 5b, along with the rest of `ConfirmOrderCheck`'s test coverage — the regression test there needs to seed stock under the caller's actual `tenant_id` (proving real stock is now found) and, separately, a stock row whose `tenant_id` happens to equal the caller's `user_id` (proving that row is *not* found post-fix, since pre-fix it would have been).

`dotnet build ModernWMS.sln` → 0 errors. `dotnet test ModernWMS.sln` → 240 passed, 0 failed (no Dispatchlist tests exist yet — confirms nothing else regressed from this fix).

---

## Issue 7 — `DispatchDeliveryService.SignForArrival` compared a viewmodel's id to itself instead of to the entity being processed

**Severity:** High (silent data corruption on a common bulk operation, same effect class as Issue 5 but a different root cause — a straight copy-paste typo, not variable shadowing).

**Found in:** `DispatchlistService.SignForArrival` (found by reading the file while planning Phase 5's Option B split; fixed during the split itself, ahead of sub-phase 5c's tests).

### What was wrong

```csharp
foreach (var entity in entities)
{
    var vm = viewModels.FirstOrDefault(t => t.id == t.id && t.dispatch_status == entity.dispatch_status);   // <- bug
    ...
}
```

Unlike Issue 5 (where an inner lambda parameter shadowed an outer one of the same name), there's no shadowing here — the outer loop variable is `entity`, and the lambda's parameter is `t`. But the id comparison was written as `t.id == t.id` instead of `t.id == entity.id`, so it's a tautology that's always `true` regardless of which `entity` the outer `foreach` is currently on. `FirstOrDefault` therefore returns the *first* `viewModels` entry whose `dispatch_status` happens to match the current `entity`'s status — not the one that actually corresponds to this `entity`'s `id`.

Effect: signing for arrival on **more than one dispatch order sharing the same `dispatch_status` in one batch call** applies whichever viewmodel matched first's `damage_qty` to every entity with that status, silently corrupting `sign_qty`/`damage_qty` for all but (at most) one of them.

### Fix

Changed the predicate to `t.id == entity.id && t.dispatch_status == entity.dispatch_status`, so each entity is matched to its own viewmodel by id, same as every other batch method in this file already does correctly (e.g. `Package`/`Weight`'s `entities.FirstOrDefault(t => t.id == vm.id && ...)`).

### Verification

Deferred to sub-phase 5c — the regression test there needs ≥2 dispatch orders sharing the same `dispatch_status` in one batch call, each with a distinct `damage_qty`, and must assert each entity ends up with its *own* `damage_qty`/`sign_qty` rather than all collapsing onto the first match — the same shape as Phase 4's `ConfirmAsync_MultipleRows_EachGetsItsOwnArrivalTime` (Issue 5). A batch where every order has a different `dispatch_status`, or only one order, would not have exercised this bug at all.

`dotnet build ModernWMS.sln` → 0 errors. `dotnet test ModernWMS.sln` → 240 passed, 0 failed (no Dispatchlist tests exist yet — confirms nothing else regressed from this fix).
