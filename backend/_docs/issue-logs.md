# Issue Log

Bugs found while writing unit test coverage (see `unit-testing-plan.md`), and how they were fixed. Each entry: what was wrong, why it matters, what changed, and how it's verified.

---

## Issue 1 — `GetAsync`/`UpdateAsync`/`DeleteAsync`-by-id had no `tenant_id` filter

**Severity:** High (cross-tenant data leak / cross-tenant write-and-delete).

**Found in:** `WarehouseService`, `WarehouseareaService`, `GoodslocationService`, `GoodsownerService`, `CustomerService`, `SupplierService`, `CompanyService`, `SpuService` (Phase 1); `CategoryService`, `FreightfeeService`, `PrintSolutionService` (Phase 2); `UserService`, `UserroleService`, `RolemenuService` (Phase 3); `AsnService`'s `Api` region (Phase 4, sub-phase 4a); `AsnmasterService`'s `UpdateAsnmasterAsync`/`DeleteAsnmasterAsync` (Phase 4, sub-phase 4b); `AsnConfirmService` — all four methods (`ConfirmAsync`, `ConfirmCancelAsync`, `UnloadAsync`, `UnloadCancelAsync`), none of which took a tenant check at all before this (Phase 4, sub-phase 4c); `AsnSortingService` — all six methods (`SortingAsync`, `GetAsnsortsAsync`, `ModifyAsnsortsAsync`, `SortedAsync`, `SortedCancelAsync`, `GetAsnPrintSeriesNumberAsync`), the largest single slice of this defect found in one service (Phase 4, sub-phase 4d); `AsnPutawayService` — `GetPendingPutawayDataAsync` (no `CurrentUser` at all) and `PutAwayAsync`'s `Asn` fetch (Phase 4, sub-phase 4e, completing Phase 4); `DispatchConfirmService.ConfirmOrder`'s `dispatchlist_datas` fetch (Phase 5's Option B split); `DispatchDeliveryService.Package`/`Weight`/`Delivery`'s entity fetches (Phase 5's Option B split); `DispatchConfirmService.GetPickListByDispatchID` (no `CurrentUser` at all) and `CancelDispatchlistDetailOpration` (no `CurrentUser` at all), plus `ConfirmPickDetail`/`CancelConfirmPickDetail` (both already took `CurrentUser` but never used it to scope the query — same shape as Phase 1's `GoodsownerService`-family gap) (Phase 5, sub-phase 5b); `DispatchDeliveryService.SetFreightfee` and `SignForArrival` (neither took `CurrentUser` at all) (Phase 5, sub-phase 5c); `StockmoveService.GetAsync` and `DeleteAsync` (neither took `CurrentUser` at all) (Phase 6, sub-phase 6b); `StockadjustService.GetAsync`, `UpdateAsync`, `DeleteAsync`, `ConfirmAdjustment` (none of the four took `CurrentUser` at all) (Phase 6, sub-phase 6c); `StockfreezeService.GetAsync`, `UpdateAsync`, `DeleteAsync` (none of the three took `CurrentUser` at all) plus `AddAsync`'s `StockEntity` lookup (had `CurrentUser` but never used it to scope the query) (Phase 6, sub-phase 6d); `StockprocessService.GetAsync`, `UpdateAsync`, `DeleteAsync` (none took `CurrentUser` at all), plus `ConfirmProcess`/`ConfirmAdjustment` (both already took `CurrentUser` but never used it to scope their entity fetch) and `AddAsync`'s `StockEntity`/`StockProcessDetail` lookups (Phase 6, sub-phase 6e); `StocktakingService.GetAsync`, `DeleteAsync` (neither took `CurrentUser` at all), plus `PutAsync`/`ConfirmAsync` (both already took `CurrentUser` but never used it to scope their fetch) and `ConfirmAsync`'s `StockEntity` mutation lookup (Phase 6, sub-phase 6f) — same defect every time, found and fixed the same way as each phase touched these services.

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
- **`StockmoveService` (Phase 6, sub-phase 6b) had the gap on three separate methods, each a different flavor**: `GetAsync(int id)` and `DeleteAsync(int id)` took no `CurrentUser` at all — fixed by adding the parameter and the usual tenant filter, same as every "no parameter" instance above. `Confirm(int id, CurrentUser currentUser)` already took `CurrentUser` (to stamp `handler`) but never used it to scope the entity fetch — the "parameter present but unused" variant — fixed by adding `&& t.tenant_id == currentUser.tenant_id` to the fetch. `AddAsync` had a *fourth* flavor, on referenced entities rather than the primary one: neither `orig_goods_location_id` nor `dest_googs_location_id` (both client-supplied in the request body) were checked against the caller's tenant at all before being used to look up `StockEntity` rows and, on `Confirm`, to create a brand-new `StockEntity` at the destination — the same shape as `AsnPutawayService.PutAwayAsync`'s `goods_location_id` gap in Phase 4, except here a successful cross-tenant call would have gone on to create real stock state at another tenant's location. Fixed by validating both location ids belong to the caller's tenant before doing anything else, and adding the tenant filter to the `StockEntity` lookups in both `AddAsync` and `Confirm` for defense in depth.
- **`StockadjustService` (Phase 6, sub-phase 6c) had the "no `CurrentUser` at all" flavor on four of its seven methods**: `GetAsync(int id)`, `UpdateAsync(viewModel)`, `DeleteAsync(int id)`, and `ConfirmAdjustment(int id)` — none took a `CurrentUser`, so none had anything to check a tenant against. `ConfirmAdjustment` additionally reads a `StockProcessDetailEntity` by a client-recorded `source_table_id` (when `job_type == 2`) with no tenant check on that lookup either — fixed the same way as every other "referenced entity" instance in this issue, by adding `&& t.tenant_id == currentUser.tenant_id`. Notably, none of these four methods (nor `AddAsync`/`GetAllAsync`) are actually reachable via any HTTP route today — `StockAdjustController` only wires up `PageAsync` — so this gap has no live exploit path *yet*, but the fix keeps the service layer correct in case that changes, consistent with unit-testing the service layer rather than only the controller surface.
- **`StockfreezeService` (Phase 6, sub-phase 6d), unlike `StockadjustService`, has every one of its methods wired to `StockFreezeController`**, so this variant had a live exploit path: `GetAsync(int id)`, `UpdateAsync(viewModel)`, and `DeleteAsync(int id)` took no `CurrentUser` at all. `AddAsync`'s `StockEntity` lookup (the query that decides which stock rows get `is_freeze` toggled) had no tenant filter either, even though `AddAsync` already takes `CurrentUser` for audit stamping — the same "parameter present but unused" variant seen throughout this issue. A cross-tenant caller who supplied another tenant's real `sku_id`/`goods_location_id` could have frozen or unfrozen that tenant's stock. Fixed by adding the parameter to the three methods and the tenant filter to `AddAsync`'s stock lookup, threading the change through `IStockFreezeService` and all three controller call sites.
- **`StockprocessService` (Phase 6, sub-phase 6e) — the whole family's largest and most complex service — had the gap on every method that touches `StockEntity` or the `StockProcessEntity` itself**: `GetAsync`, `UpdateAsync`, `DeleteAsync` took no `CurrentUser` at all. `ConfirmProcess` and `ConfirmAdjustment` both already took `CurrentUser` (for audit stamping) but neither used it to scope their entity fetch. `ConfirmAdjustment` additionally reads/writes `StockEntity` (the method that actually applies a process's stock effect) with no tenant filter on that lookup at all — the most consequential instance of this issue in the whole stock-movement family, since a cross-tenant call here would directly corrupt another tenant's real inventory quantities, not just leak or block. `AddAsync` had the same "referenced entity" flavor on its own `StockEntity`/`StockProcessDetail` availability-check lookups. Fixed the same way as every other instance: add the parameter where missing, add `&& t.tenant_id == currentUser.tenant_id` everywhere it was missing, thread the change through `IStockProcessService` and every controller call site (`StockProcessController` wires up every method, so every one of these had a live exploit path).
- **`StocktakingService` (Phase 6, sub-phase 6f, the last individual service in the family) had the same shape once more**: `GetAsync` and `DeleteAsync` took no `CurrentUser` at all. `PutAsync` and `ConfirmAsync` both already took `CurrentUser` (for `handler`/audit stamping) but neither used it to scope their entity fetch. `ConfirmAsync`'s `StockEntity` lookup — the query that decides which stock row actually gets the counted-quantity adjustment applied — had no tenant filter either, the same "most consequential" flavor as `StockprocessService.ConfirmAdjustment` in 6e. Fixed the same way, with every controller action wired up so every gap had a live exploit path.

### Verification

Added one cross-tenant regression test per service per operation (`GetAsync_BelongsToDifferentTenant_Returns…`, `UpdateAsync_BelongsToDifferentTenant_ReturnsNotExists`, `DeleteAsync_BelongsToDifferentTenant_DoesNotDelete`) — 24 tests in Phase 1 across `WarehouseServiceTests`, `WarehouseareaServiceTests`, `GoodslocationServiceTests`, `GoodsownerServiceTests`, `CustomerServiceTests`, `SupplierServiceTests`, `CompanyServiceTests`, `SpuServiceTests`; 9 more in Phase 2 across `CategoryServiceTests`, `FreightfeeServiceTests`, `PrintSolutionServiceTests`; 6 more in Phase 3 across `UserServiceTests`, `UserRoleServiceTests`, `RoleMenuServiceTests` (plus `UserServiceTests.ResetPwd_UserBelongsToDifferentTenant_LeavesPasswordUnchanged` for the `ResetPwd` variant); 4 more in Phase 4's sub-phase 4a across `AsnServiceTests` (`GetAsync`/`UpdateAsync`/`DeleteAsync`/`BulkModifyGoodsownerAsync`); 3 more in sub-phase 4b across `AsnmasterServiceTests` (`GetAsnmasterAsync`/`UpdateAsnmasterAsync`/`DeleteAsnmasterAsync`, the last also asserting the child `Asn` rows survive an attempted cross-tenant delete); 4 more in sub-phase 4c across `AsnConfirmServiceTests` (`ConfirmAsync`/`ConfirmCancelAsync`/`UnloadAsync`/`UnloadCancelAsync`); 6 more in sub-phase 4d across `AsnSortingServiceTests` (`SortingAsync`/`GetAsnsortsAsync`/`ModifyAsnsortsAsync`/`SortedAsync`/`SortedCancelAsync`/`GetAsnPrintSeriesNumberAsync`); and 3 more in sub-phase 4e across `AsnPutawayServiceTests` (`GetPendingPutawayDataAsync`, plus `PutAwayAsync_LocationBelongsToDifferentTenant_IsRejected` and `PutAwayAsync_AsnBelongsToDifferentTenant_ReturnsNotExists`, which isolate the two separate tenant checks from each other). Each seeds a row under tenant 1 and calls the service as tenant 2, asserting the row is invisible/unmodifiable/undeletable from tenant 2. All pass against the fixed code (and would fail against the pre-fix code, since the tenant check is exactly what makes them pass).

`dotnet test ModernWMS.sln` → 240 passed, 0 failed (as of Phase 4's sub-phase 4e, completing Phase 4). `dotnet build ModernWMS.sln` → 0 errors.

Phase 5 additions: sub-phase 5b added `DispatchConfirmServiceTests.CancelDispatchlistDetailOpration_BelongsToDifferentTenant_ReturnsNotExists`, `GetPickListByDispatchID_BelongsToDifferentTenant_ReturnsEmpty`, `ConfirmPickDetail_BelongsToDifferentTenant_IsIgnored`, `CancelConfirmPickDetail_BelongsToDifferentTenant_IsIgnored`, and `ConfirmOrder_BelongsToDifferentTenant_ReturnsDataChanged` (the last proving the tenant filter added during the Option B split, ahead of any test). `dotnet test ModernWMS.sln` → 288 passed, 0 failed (as of sub-phase 5b). `dotnet build ModernWMS.sln` → 0 errors.

Sub-phase 5c added `DispatchDeliveryServiceTests.Package_BelongsToDifferentTenant_ReturnsDataChanged`, `Weight_BelongsToDifferentTenant_ReturnsDataChanged`, `Delivery_BelongsToDifferentTenant_ReturnsDataChanged` (all three proving the tenant filters added during the Option B split), and `SetFreightfee_BelongsToDifferentTenant_LeavesRowUntouched`/`SignForArrival_BelongsToDifferentTenant_LeavesRowUntouched` (proving the two brand-new `CurrentUser` parameters added in this sub-phase). This closes out Issue 1 for the entire `Dispatchlist` module — every method across all three split services now filters by tenant. `dotnet test ModernWMS.sln` → 306 passed, 0 failed (as of sub-phase 5c, completing Phase 5). `dotnet build ModernWMS.sln` → 0 errors.

Sub-phase 6b (`StockmoveService`, Phase 6) added `StockmoveServiceTests.GetAsync_BelongsToDifferentTenant_ReturnsNull`, `DeleteAsync_BelongsToDifferentTenant_DoesNotDelete`, `Confirm_BelongsToDifferentTenant_ReturnsNotExists`, and `AddAsync_DestLocationBelongsToDifferentTenant_ReturnsError` (the last proving the new location-ownership validation in `AddAsync`). `dotnet test ModernWMS.sln` → 341 passed, 0 failed (as of sub-phase 6b). `dotnet build ModernWMS.sln` → 0 errors.

Sub-phase 6c (`StockadjustService`, Phase 6) added `StockadjustServiceTests.GetAsync_BelongsToDifferentTenant_ReturnsNull`, `UpdateAsync_BelongsToDifferentTenant_ReturnsNotExists`, `DeleteAsync_BelongsToDifferentTenant_DoesNotDelete`, and `ConfirmAdjustment_BelongsToDifferentTenant_ReturnsNotExists`. `dotnet test ModernWMS.sln` → 357 passed, 0 failed (as of sub-phase 6c). `dotnet build ModernWMS.sln` → 0 errors.

Sub-phase 6d (`StockfreezeService`, Phase 6) added `StockfreezeServiceTests.GetAsync_BelongsToDifferentTenant_ReturnsNull`, `UpdateAsync_BelongsToDifferentTenant_ReturnsNotExists`, `DeleteAsync_BelongsToDifferentTenant_DoesNotDelete`, and `AddAsync_OnlyFreezesStockForCurrentTenant` (the last proving the new tenant filter on the stock lookup actually used to decide which rows get frozen/unfrozen). `dotnet test ModernWMS.sln` → 372 passed, 0 failed (as of sub-phase 6d). `dotnet build ModernWMS.sln` → 0 errors.

Sub-phase 6e (`StockprocessService`, Phase 6) added `StockprocessServiceTests.GetAsync_BelongsToDifferentTenant_ReturnsNull`, `UpdateAsync_BelongsToDifferentTenant_ReturnsNotExists`, `DeleteAsync_BelongsToDifferentTenant_DoesNotDelete`, `ConfirmProcess_BelongsToDifferentTenant_ReturnsNotExists`, `ConfirmAdjustment_BelongsToDifferentTenant_ReturnsNotExists` (the last proving the tenant filter on the `StockEntity` mutation lookup — the most consequential fix in this sub-phase, since it directly protects real inventory quantities), and `AddAsync_SourceStockBelongsToDifferentTenant_ReturnsDataChanged`. `dotnet test ModernWMS.sln` → 397 passed, 0 failed (as of sub-phase 6e). `dotnet build ModernWMS.sln` → 0 errors.

Sub-phase 6f (`StocktakingService`, Phase 6, completing the individual-service sub-phases) added `StocktakingServiceTests.GetAsync_BelongsToDifferentTenant_ReturnsEmptyViewModel`, `PutAsync_BelongsToDifferentTenant_ReturnsNotExists`, `ConfirmAsync_BelongsToDifferentTenant_ReturnsNotExists` (proving the `StockEntity` mutation lookup's new tenant filter), and `DeleteAsync_BelongsToDifferentTenant_DoesNotDelete`. `dotnet test ModernWMS.sln` → 413 passed, 0 failed (as of sub-phase 6f). `dotnet build ModernWMS.sln` → 0 errors.

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

---

## Issue 8 — `StockService.SafetyStockPageAsync` joined a warehouse id against a *location's* primary key, silently dropping every row in real data

**Severity:** Critical (the safety-stock report returns empty for any tenant where a warehouse's id doesn't coincidentally equal some goods-location's id — which is the normal case in any deployment with more than a trivial number of locations).

**Found in:** `StockService.SafetyStockPageAsync` (found and fixed while writing Phase 6 sub-phase 6a tests, confirmed with a probe test before being formalized as a regression test).

### What was wrong

```csharp
var query = from sg in stock_group_datas
            ...
            join gl in location_DBSet on sg.warehouse_id equals gl.id   // <- bug
            join sss in sku_safety_DBSet on ... 
            select new SafetyStockManagementViewModel
            {
                ...
                qty_available = gl.warehouse_area_property == 5 ? 0 : (...),
                ...
                warehouse_name = gl.warehouse_name,
                ...
            };
```

`sg.warehouse_id` is a real `WarehouseEntity.id`, but the join matched it against `gl.id` — a `GoodslocationEntity`'s own primary key, an entirely different auto-increment sequence. Because this is an **inner** join (no `DefaultIfEmpty()`), any row whose warehouse id didn't happen to equal some location's id was dropped from the result set entirely, not just given a wrong `warehouse_name`. `warehouse_area_property` used for the `qty_available` damage-area exclusion was also read off whatever location happened to match, not any location that actually belonged to the warehouse — a second, compounding correctness problem: this check operates at the *warehouse* aggregation level (`stock_group_datas` groups by `{sku_id, warehouse_id}` and already sums across every location in that warehouse, damage locations included), so a single location's `warehouse_area_property` was never the right thing to gate on in the first place.

### Fix

Replaced the join with a real `WarehouseEntity` lookup (`join wh in warehouse_DBSet on sg.warehouse_id equals wh.id`) for `warehouse_name`. For `qty_available`'s damage-area exclusion, extended `stock_group_datas` to compute `qty_normal`/`qty_normal_frozen` (summing only rows from non-damage locations, `gl.warehouse_area_property != 5`) at the same aggregation stage — the exact pattern `StockService.StockPageAsync` already uses correctly for the identical problem — and removed the broken per-location ternary.

### Verification

`StockServiceTests.SafetyStockPageAsync_WarehouseIdDiffersFromAnyLocationId_StillFindsCorrectWarehouseName` deliberately seeds two "junk" `WarehouseEntity` rows before the real one, so the real warehouse's id (3) is guaranteed to differ from the one location's id (1) that references it — a test where they coincidentally matched would have passed against the buggy code too. `SafetyStockPageAsync_MatchingSkuSafetyStockRow_ReturnsSafetyStockQty` covers the sibling `sku_safety_DBSet` join.

`dotnet build ModernWMS.sln` → 0 errors. `dotnet test ModernWMS.sln` → 324 passed, 0 failed.

---

## Issue 9 — `StockService.StockAgePageAsync` pushed a provider-specific SQL function into the query, crashing under every provider except MySQL

**Severity:** Critical (throws `InvalidOperationException` on any request with real `putaway_date` data, under the project's own default local-dev provider, SQLite — the endpoint was completely unusable outside a MySQL deployment).

**Found in:** `StockService.StockAgePageAsync` (found while writing Phase 6 sub-phase 6a tests, via a probe test before writing the real regression test).

### What was wrong

```csharp
var database_config = Configuration.GetSection("Database")["db"].ToUpper();
...
stock_age = sg.putaway_date == UtilConvert.MinDate ? 0
    : database_config == "MYSQL"
        ? Microsoft.EntityFrameworkCore.MySqlDbFunctionsExtensions.DateDiffDay(EF.Functions, sg.putaway_date.Date, today)
        : Microsoft.EntityFrameworkCore.SqlServerDbFunctionsExtensions.DateDiffDay(EF.Functions, sg.putaway_date.Date, today),
```

Both `DateDiffDay` overloads are provider-specific EF Core extension methods (one only translatable by the MySQL provider, the other only by the SQL Server provider). Whichever branch the runtime config selects, EF Core has to translate that branch into SQL for the query to execute at all — and neither translates under SQLite. Since `appsettings.Development.json` (this project's own checked-in default local config) sets `Database:db` to `SQLITE`, every real request against a putaway'd stock row hit the `SqlServerDbFunctionsExtensions` branch and threw `System.InvalidOperationException: The 'DateDiffDay' method is not supported because the query has switched to client-evaluation`. A request against an empty table (or where every row still has `putaway_date == MinDate`, so the ternary's data-dependent branches are never actually reached at the row level) happened not to trip this — which is likely why it shipped unnoticed.

### Fix

Removed the provider-branching and the `stock_age` computation from the translated query entirely. The query now selects the raw `putaway_date` (and everything else) as before, fetches the matching rows with `ToListAsync()`, and computes `stock_age = (today.Date - putaway_date.Date).Days` (or `0` for `MinDate`) client-side afterward — the same "compute in C#, not in SQL" shape already used for `AdvancedDispatchlistPageAsync`'s decimal-sum workaround. The `stock_age_from`/`stock_age_to` filters and the paging (`Skip`/`Take`) moved to operate on the in-memory list after that computation, since they depend on it. This also made the `Configuration`/`IConfiguration` constructor dependency on `StockService` entirely unused, so it — and the now-pointless `using Microsoft.Extensions.Configuration;` — were removed in the same pass.

### Verification

`StockServiceTests.StockAgePageAsync_ComputesStockAgeFromPutawayDate` seeds a stock row with a real `putaway_date` seven days in the past — this is exactly the request shape that threw before the fix — and asserts `stock_age == 7`. `StockAgePageAsync_NeverPutAway_StockAgeIsZero` and `StockAgePageAsync_FiltersByStockAgeRange` cover the remaining branches.

`dotnet build ModernWMS.sln` → 0 errors. `dotnet test ModernWMS.sln` → 324 passed, 0 failed.

---

## Issue 10 — `StockService.DeliveryStatistic` summed a decimal expression in SQL, crashing under SQLite

**Severity:** High (throws `System.NotSupportedException` on any non-empty result under SQLite — the delivery-amount report was unusable under this project's own default local-dev provider whenever there was real delivery data to report on).

**Found in:** `StockService.DeliveryStatistic` (found while writing Phase 6 sub-phase 6a tests — the same class of bug as Issue 4 (`SpuService.UpdateAsync`'s `Math.Round`) and the already-documented `#region sqlite cannot sum data of decimal type` workaround in `DispatchlistService.AdvancedDispatchlistPageAsync`, just not yet applied here).

### What was wrong

```csharp
delivery_amount = dg.Sum(t => t.dpp.picked_qty * t.sku.price)
```

`picked_qty` is `int` and `sku.price` is `decimal`, so the product is `decimal`, and EF Core's SQLite provider cannot translate `SUM()` over a decimal expression server-side (`System.NotSupportedException: SQLite cannot apply aggregate operator 'Sum' on expressions of type 'decimal'`). This is exactly the same provider limitation Issue 4 and `AdvancedDispatchlistPageAsync` already worked around elsewhere in the codebase — it just hadn't been hit here yet, because nothing had exercised this method against SQLite with real grouped rows before now.

### Fix

Since `sku.price` is invariant within each group (the group key already includes `sku_code`/`sku_name`, so every row in a group shares one sku), `Sum(picked_qty * price)` is mathematically equal to `price * Sum(picked_qty)` — and `Sum(picked_qty)` (an int sum) translates fine. Added `sku.price` to the `group by` key (a plain column in a `GROUP BY`, not an aggregate, so SQLite has no trouble with it) and removed the decimal `Sum` from the SQL projection entirely; the query now projects into an intermediate anonymous type carrying `sku_price` and the int `delivery_qty`, and `delivery_amount = delivery_qty * sku_price` is computed client-side while mapping that intermediate result into `DeliveryStatisticViewModel` after `ToListAsync()`.

### Verification

`StockServiceTests.DeliveryStatistic_ComputesDeliveryQtyAndAmount` seeds a delivered dispatch pick (`picked_qty = 4`, `sku.price = 10`) and asserts `delivery_amount == 40` — this is exactly the request shape that threw before the fix (a real delivered row, not an empty result set).

`dotnet build ModernWMS.sln` → 0 errors. `dotnet test ModernWMS.sln` → 324 passed, 0 failed.

---

## Issue 11 — `StockmoveService.Confirm` looked up the destination stock row with an inverted sku comparison, creating unbounded duplicate `StockEntity` rows

**Severity:** Critical (every confirmed move into a location that already holds stock of the same sku creates a brand-new duplicate `StockEntity` row instead of incrementing the existing one — with no bound on how many duplicates accumulate, since the same bug fires again on every subsequent move to that location).

**Found in:** `StockmoveService.Confirm` (found by reading the file while planning Phase 6 sub-phase 6b; confirmed and fixed once a test was written for it).

### What was wrong

```csharp
var dest_stock = await stock_DBSet.FirstOrDefaultAsync(t => t.goods_owner_id == entity.goods_owner_id
    && t.series_number == entity.series_number && t.goods_location_id == entity.dest_googs_location_id
    && t.sku_id != entity.sku_id   // <- bug: should be ==
    && t.expiry_date == entity.expiry_date && t.price == entity.price && t.putaway_date == entity.putaway_date);
```

Every sibling lookup in this method (and the equivalent lookups in `AddAsync`, and every composite-key stock lookup elsewhere in the codebase — e.g. `AsnPutawayService.PutAwayAsync`) matches on `sku_id == ...` as part of the full composite key (`sku_id`, `goods_location_id`, `goods_owner_id`, `series_number`, `expiry_date`, `price`, `putaway_date`) that identifies one logical stock "lot". This one line inverted the comparison to `!=`, so the query explicitly excluded the one row that could ever be the correct match, and instead looked for some *other* sku's stock sharing every other attribute — a combination that, in practice, essentially never exists. `dest_stock` therefore always came back `null`, and the code always took the "create a new `StockEntity`" branch: `Confirm`ing a second move into a location that already held stock of the same lot created a **second** `StockEntity` row with an identical composite key instead of incrementing the first, and a third move created a third row, and so on — an unbounded number of duplicate rows for what should be one stock record, undermining the "one row per unique lot" invariant every other stock-mutating method in the codebase (`AsnPutawayService.PutAwayAsync`, `DispatchDeliveryService.Delivery`, `StockmoveService.AddAsync`'s own `orig_stock` lookup) relies on.

### Fix

Changed `t.sku_id != entity.sku_id` to `t.sku_id == entity.sku_id`, matching every other composite-key match in the method and the file.

### Verification

`StockmoveServiceTests.Confirm_DestStockAlreadyExistsForSameSku_IncrementsExistingRowInsteadOfDuplicating` seeds an existing destination `StockEntity` row for the same lot before confirming a move into that location, then asserts there is still exactly **one** row at that location afterward (not two) and that its `qty` reflects the increment — a scenario with no pre-existing destination stock would not have exercised this bug at all, since the buggy code and the fixed code behave identically (create a new row) when there's nothing there yet.

`dotnet build ModernWMS.sln` → 0 errors. `dotnet test ModernWMS.sln` → 341 passed, 0 failed.

---

## Issue 12 — `StockadjustService.ConfirmAdjustment` built a new `StockEntity` for a first-time stock location but never added it to the DbContext, so it was silently never persisted

**Severity:** Critical (confirming any stock adjustment for a sku/location combination that has no existing stock row does nothing to real inventory, while the operation still reports success — and every subsequent confirm for that same combination repeats the same no-op, forever, since nothing was ever actually saved).

**Found in:** `StockadjustService.ConfirmAdjustment` (found by reading the file while planning Phase 6 sub-phase 6c; confirmed and fixed once a test was written for it).

### What was wrong

```csharp
var stock = await stock_DBSet.Where(...).FirstOrDefaultAsync();
if (stock == null)
{
    stock = new StockEntity
    {
        id = entity.id,   // <- also wrong: forces the new row's PK to an unrelated StockAdjustEntity's id
        sku_id = entity.sku_id,
        ...
    };
    // no stock_DBSet.AddAsync(stock) call anywhere
}
else
{
    stock.qty += entity.qty;
    ...
}
entity.is_update_stock = true;
entity.last_update_time = DateTime.Now;
var res = await _dBContext.SaveChangesAsync();
```

When an existing `StockEntity` row is found, `stock.qty += ...` mutates a tracked entity, so `SaveChangesAsync()` persists it correctly. But when no existing row is found, the `new StockEntity { ... }` object is constructed and never handed to the `DbContext` via `Add()`/`AddAsync()` — so EF Core's change tracker never sees it, and `SaveChangesAsync()` has nothing to insert. The call still returns `res > 0` and the method still reports `operation_success`, because `entity.is_update_stock`/`entity.last_update_time` *are* tracked (from the earlier query) and did change — masking the fact that the actual stock creation silently did nothing. The stray `id = entity.id` line (setting the new `StockEntity`'s primary key to the unrelated `StockAdjustEntity`'s own id) would have been a second problem even if `Add()` had been called, since `StockEntity` and `StockAdjustEntity` are independent auto-increment sequences with no reason to share a key.

### Fix

Added `await stock_DBSet.AddAsync(stock);` inside the `if (stock == null)` branch, and removed the `id = entity.id` line so the new row gets a real auto-generated id (matching every other "create new stock row" site in the codebase, e.g. `AsnPutawayService.PutAwayAsync`, `DispatchDeliveryService`'s callers, `StockmoveService.Confirm`, none of which set `id` explicitly on insert).

### Verification

`StockadjustServiceTests.ConfirmAdjustment_NewStockLocation_CreatesAndPersistsStockRow` confirms an adjustment for a sku/location combination with no pre-existing `StockEntity` row, then queries the `Stock` table directly afterward and asserts a row now exists with the expected `qty`/`sku_id`/`goods_location_id`/`tenant_id` — against the pre-fix code this assertion would fail because the table would still be empty despite the method returning success. `ConfirmAdjustment_ExistingStockLocation_IncrementsQty` covers the sibling branch, which already worked correctly.

`dotnet build ModernWMS.sln` → 0 errors. `dotnet test ModernWMS.sln` → 357 passed, 0 failed.

---

## Issue 13 — `StockprocessService.DeleteAsync` called `Remove(entity)` without checking for `null` first, throwing an unhandled exception instead of a clean "not found"

**Severity:** High (any delete request for an unknown id, or for a record that no longer matches the `process_status == false` filter — e.g. one that's already been processed — crashes the request with an unhandled exception instead of returning the same `not_exists_entity`/`status_changed`-style failure every sibling `DeleteAsync` in the codebase returns).

**Found in:** `StockprocessService.DeleteAsync` (found by reading the file while planning Phase 6 sub-phase 6e; confirmed and fixed once a test was written for it).

### What was wrong

```csharp
public async Task<(bool flag, string msg)> DeleteAsync(int id)
{
    var entity = await _dBContext.GetDbSet<StockProcessEntity>().Where(t => t.id.Equals(id) && t.process_status == false).Include(e => e.detailList).FirstOrDefaultAsync();
    _dBContext.GetDbSet<StockProcessEntity>().Remove(entity);   // <- no null check on entity
    var qty = await _dBContext.SaveChangesAsync();
    ...
}
```

Unlike every other `DeleteAsync` in the codebase (all of which check `if (entity == null) { return (false, ...); }` before touching the tracked entity), this one went straight from the query to `Remove(entity)`. EF Core's `DbSet<T>.Remove` throws `ArgumentNullException` when passed `null`. Since the query filters on `process_status == false` in addition to the id, `entity` comes back `null` in two realistic cases, not just a typo'd id: a genuinely unknown id, and — more likely to happen in real use — a legitimate id for a record that has *already been processed* (`process_status == true`), which is exactly the kind of request a user might make by clicking delete twice or after a page went stale. Both cases crashed the whole request instead of returning a normal failure result.

### Fix

Added the same `if (entity == null) { return (false, _stringLocalizer["not_exists_entity"]); }` guard used by every sibling `DeleteAsync`, before the `Remove` call.

### Verification

`StockprocessServiceTests.DeleteAsync_UnknownId_ReturnsNotExistsInsteadOfThrowing` and `DeleteAsync_AlreadyProcessed_IsBlockedInsteadOfThrowing` both call `DeleteAsync` against ids that fall into the two null-`entity` cases described above. Against the pre-fix code, both would have thrown an unhandled `ArgumentNullException` (visible in a test run as a failed test with an exception, not just a failed assertion) instead of returning `flag == false`.

`dotnet build ModernWMS.sln` → 0 errors. `dotnet test ModernWMS.sln` → 397 passed, 0 failed.

---

## Issue 14 — `StocktakingService.ConfirmAsync` had no guard against being confirmed twice, so every repeat call re-applied the stock adjustment

**Severity:** Critical (calling confirm on the same stocktaking record more than once — a double-click, a retried request, a stale browser tab — applies `difference_qty` to real `StockEntity.qty` again each time, and writes another `StockAdjustEntity` audit row each time, with no limit on how many times this can happen).

**Found in:** `StocktakingService.ConfirmAsync` (found by reading the file while planning Phase 6 sub-phase 6f, by comparing it against `StockprocessService.ConfirmProcess`/`ConfirmAdjustment` in the same family, both of which do guard against being re-run).

### What was wrong

```csharp
public async Task<(bool flag, string msg)> ConfirmAsync(int id, CurrentUser currentUser)
{
    var entity = await DbSet.FirstOrDefaultAsync(t => t.id.Equals(id));
    if (entity == null) { return (false, _stringLocalizer["not_exists_entity"]); }
    // change stock sku qty  <- straight into the mutation, no status/already-adjusted check
    var stockEntity = await Stocks.FirstOrDefaultAsync(...);
    if (stockEntity == null) { await Stocks.AddAsync(new StockEntity { qty = entity.difference_qty, ... }); }
    else { stockEntity.qty += entity.difference_qty; ... }
    await Stockadjusts.AddAsync(new StockAdjustEntity { qty = entity.difference_qty, job_type = 1, source_table_id = entity.id, ... });
    ...
}
```

Every sibling confirm-style method in the stock-movement family (`StockprocessService.ConfirmProcess`'s `process_status == true` check, `ConfirmAdjustment`'s `entity.process_status && adjusted` check, `StockmoveService.Confirm`'s `move_status` transition) refuses to re-apply its effect once already applied. `StocktakingService.ConfirmAsync` had no equivalent: nothing on `StockTakingEntity` records "already confirmed" the way `job_status` records "already counted" (set by `PutAsync`), so nothing stopped `ConfirmAsync` from running its full effect — mutate `StockEntity.qty` by `difference_qty`, insert a `StockAdjustEntity` audit row — every single time it was called for the same id. Two calls double the real quantity adjustment and leave two audit rows referencing the same `source_table_id`; N calls apply it N times.

### Fix

Added a guard mirroring how `adjust_status` is already computed for display in `PageAsync`/`GetAsync` elsewhere in this same file: before touching `StockEntity`, check whether a `StockAdjustEntity` row already exists with `job_type == 1 && source_table_id == entity.id`, and if so return `(false, "status_changed")` without touching stock. This intentionally does not add any new requirement that the record must have been counted (`job_status == true`) first, since that's a separate business question this pass didn't have enough context to decide safely — only the unambiguous "never apply the same adjustment twice" invariant was added.

### Verification

`StocktakingServiceTests.ConfirmAsync_AlreadyConfirmed_ReturnsStatusChangedAndDoesNotDoubleAdjust` calls `ConfirmAsync` twice in a row on the same seeded record and asserts the second call returns `flag == false`, that `StockEntity.qty` reflects only one application of `difference_qty`, and that exactly one `StockAdjustEntity` row exists afterward — against the pre-fix code, the second call would have succeeded and both the stock quantity and the adjust-row count would have been wrong.

`dotnet build ModernWMS.sln` → 0 errors. `dotnet test ModernWMS.sln` → 413 passed, 0 failed.
