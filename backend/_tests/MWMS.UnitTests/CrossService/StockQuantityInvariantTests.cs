using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Entities.ViewModels.Stock;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.CrossService
{
    /// <summary>
    /// Phase 6 sub-phase 6g: cross-service quantity invariants. CLAUDE.md flags that a change to
    /// quantities usually has to touch several of Stock/Stockmove/Stockadjust/Stockfreeze/Stockprocess/
    /// Stocktaking consistently - these tests exercise a mutating service and a reporting/blocking
    /// service together, instead of only testing each service against its own isolated fixture.
    /// </summary>
    public class StockQuantityInvariantTests
    {
        private static async Task<(SpuEntity spu, SkuEntity sku)> SeedSpuSkuAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId)
        {
            var spu = new SpuEntity { spu_code = "SPU1", tenant_id = tenantId };
            dbContext.GetDbSet<SpuEntity>().Add(spu);
            await dbContext.SaveChangesAsync();
            var sku = new SkuEntity { spu_id = spu.id, sku_code = "SKU1" };
            dbContext.GetDbSet<SkuEntity>().Add(sku);
            await dbContext.SaveChangesAsync();
            return (spu, sku);
        }

        private static async Task<GoodslocationEntity> SeedLocationAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, string name)
        {
            var location = new GoodslocationEntity { location_name = name, warehouse_name = "W1", tenant_id = tenantId };
            dbContext.GetDbSet<GoodslocationEntity>().Add(location);
            await dbContext.SaveChangesAsync();
            return location;
        }

        private static async Task<StockEntity> SeedStockAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, int skuId, int locationId, int qty)
        {
            var stock = new StockEntity { sku_id = skuId, goods_location_id = locationId, qty = qty, tenant_id = tenantId };
            dbContext.GetDbSet<StockEntity>().Add(stock);
            await dbContext.SaveChangesAsync();
            return stock;
        }

        [Fact]
        public async Task StockmoveConfirm_ConservesTotalOnHandQtyAcrossLocations()
        {
            // StockMoveService moves qty between two locations; StockService's aggregate
            // report must show the same total on-hand qty for the sku before and after,
            // just redistributed between the two locations.
            using var scope = new SqliteTestDbContextScope();
            var currentUser = new CurrentUser { tenant_id = 1, user_name = "alice" };
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var orig = await SeedLocationAsync(scope.DbContext, 1, "Orig");
            var dest = await SeedLocationAsync(scope.DbContext, 1, "Dest");
            await SeedStockAsync(scope.DbContext, 1, sku.id, orig.id, qty: 10);

            var moveService = new StockMoveService(scope.DbContext, new FakeStringLocalizer<MultiLanguage>(), TestFunctionHelperFactory.Create(scope.DbContext));
            var (moveId, _) = await moveService.AddAsync(
                new StockMoveViewModel { sku_id = sku.id, orig_goods_location_id = orig.id, dest_googs_location_id = dest.id, qty = 6 },
                currentUser);
            moveId.ShouldBeGreaterThan(0);
            var (confirmFlag, _) = await moveService.Confirm(moveId, currentUser);
            confirmFlag.ShouldBeTrue();

            var stockService = new StockService(scope.DbContext, new FakeStringLocalizer<MultiLanguage>());
            var (rows, totals) = await stockService.StockPageAsync(new PageSearch(), currentUser);

            totals.ShouldBe(1);
            var row = rows.ShouldHaveSingleItem();
            row.qty.ShouldBe(10); // 4 left at Orig + 6 now at Dest, same sku aggregated by StockPageAsync
        }

        [Fact]
        public async Task StockmovePendingLock_ReducesAvailableQtyInStockPageAsync()
        {
            // An unconfirmed StockMoveEntity (move_status == 0) locks qty at the origin.
            // StockService.StockPageAsync's qty_available computation must account for
            // that lock even though the move itself was created by a different service.
            using var scope = new SqliteTestDbContextScope();
            var currentUser = new CurrentUser { tenant_id = 1, user_name = "alice" };
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var orig = await SeedLocationAsync(scope.DbContext, 1, "Orig");
            var dest = await SeedLocationAsync(scope.DbContext, 1, "Dest");
            await SeedStockAsync(scope.DbContext, 1, sku.id, orig.id, qty: 10);

            var moveService = new StockMoveService(scope.DbContext, new FakeStringLocalizer<MultiLanguage>(), TestFunctionHelperFactory.Create(scope.DbContext));
            var (moveId, _) = await moveService.AddAsync(
                new StockMoveViewModel { sku_id = sku.id, orig_goods_location_id = orig.id, dest_googs_location_id = dest.id, qty = 6 },
                currentUser);
            moveId.ShouldBeGreaterThan(0); // left pending, not confirmed

            var stockService = new StockService(scope.DbContext, new FakeStringLocalizer<MultiLanguage>());
            var (rows, _) = await stockService.StockPageAsync(new PageSearch(), currentUser);

            var row = rows.ShouldHaveSingleItem();
            row.qty.ShouldBe(10);
            row.qty_locked.ShouldBe(6);
            row.qty_available.ShouldBe(4);
        }

        [Fact]
        public async Task StockfreezeAddAsync_BlocksStockmoveAddAsyncFromMovingFrozenStock()
        {
            // StockFreezeService.AddAsync toggles StockEntity.is_freeze directly. A later
            // StockMoveService.AddAsync sourcing from that same stock row must see it as
            // unavailable, even though the freeze was applied by a different service.
            using var scope = new SqliteTestDbContextScope();
            var currentUser = new CurrentUser { tenant_id = 1, user_name = "alice" };
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var orig = await SeedLocationAsync(scope.DbContext, 1, "Orig");
            var dest = await SeedLocationAsync(scope.DbContext, 1, "Dest");
            await SeedStockAsync(scope.DbContext, 1, sku.id, orig.id, qty: 10);

            var freezeService = new StockFreezeService(scope.DbContext, new FakeStringLocalizer<MultiLanguage>(), TestFunctionHelperFactory.Create(scope.DbContext));
            var (freezeId, _) = await freezeService.AddAsync(
                new StockFreezeViewModel { sku_id = sku.id, goods_location_id = orig.id, job_type = true },
                currentUser);
            freezeId.ShouldBeGreaterThan(0);

            var moveService = new StockMoveService(scope.DbContext, new FakeStringLocalizer<MultiLanguage>(), TestFunctionHelperFactory.Create(scope.DbContext));
            var (moveId, _) = await moveService.AddAsync(
                new StockMoveViewModel { sku_id = sku.id, orig_goods_location_id = orig.id, dest_googs_location_id = dest.id, qty = 5 },
                currentUser);

            moveId.ShouldBe(0);
            (await scope.DbContext.GetDbSet<StockMoveEntity>().AsNoTracking().AnyAsync()).ShouldBeFalse();
        }

        [Fact]
        public async Task StockadjustConfirmAdjustment_ChangeIsReflectedInStockPageAsyncTotals()
        {
            // StockAdjustService.ConfirmAdjustment mutates StockEntity.qty directly.
            // StockService's aggregate report (a different service, different query
            // shape entirely) must reflect that change immediately.
            using var scope = new SqliteTestDbContextScope();
            var currentUser = new CurrentUser { tenant_id = 1 };
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1, "L1");
            await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 10);

            var adjustService = new StockAdjustService(scope.DbContext, new FakeStringLocalizer<MultiLanguage>());
            var (adjustId, _) = await adjustService.AddAsync(
                new StockAdjustViewModel { sku_id = sku.id, goods_location_id = location.id, qty = -3 },
                currentUser);
            adjustId.ShouldBeGreaterThan(0);
            var (confirmFlag, _) = await adjustService.ConfirmAdjustment(adjustId, currentUser);
            confirmFlag.ShouldBeTrue();

            var stockService = new StockService(scope.DbContext, new FakeStringLocalizer<MultiLanguage>());
            var (rows, _) = await stockService.StockPageAsync(new PageSearch(), currentUser);

            rows.ShouldHaveSingleItem().qty.ShouldBe(7);
        }

        [Fact]
        public async Task StockprocessConfirmAdjustment_TargetLocationCreationReflectedAcrossLocationsInStockPageAsync()
        {
            // StockProcessService moves qty from a source location to a target location
            // (repackaging/consolidation) via ConfirmAdjustment. StockService's aggregate
            // qty for the sku must be conserved across both locations, same invariant as
            // the plain Stockmove case but exercised through a different mutating service.
            using var scope = new SqliteTestDbContextScope();
            var currentUser = new CurrentUser { tenant_id = 1, user_name = "alice" };
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var source = await SeedLocationAsync(scope.DbContext, 1, "Source");
            var target = await SeedLocationAsync(scope.DbContext, 1, "Target");
            await SeedStockAsync(scope.DbContext, 1, sku.id, source.id, qty: 10);

            var processService = new StockProcessService(scope.DbContext, new FakeStringLocalizer<MultiLanguage>(), TestFunctionHelperFactory.Create(scope.DbContext));
            var viewModel = new StockProcessViewModel
            {
                detailList = new List<StockProcessDetailViewModel>
                {
                    new() { sku_id = sku.id, goods_location_id = source.id, qty = 6, is_source = true },
                    new() { sku_id = sku.id, goods_location_id = target.id, qty = 6, is_source = false },
                },
            };
            var (processId, _) = await processService.AddAsync(viewModel, currentUser);
            processId.ShouldBeGreaterThan(0);
            var (adjustFlag, _) = await processService.ConfirmAdjustment(processId, currentUser);
            adjustFlag.ShouldBeTrue();

            var stockService = new StockService(scope.DbContext, new FakeStringLocalizer<MultiLanguage>());
            var (rows, totals) = await stockService.StockPageAsync(new PageSearch(), currentUser);

            totals.ShouldBe(1);
            rows.ShouldHaveSingleItem().qty.ShouldBe(10); // 4 left at Source + 6 now at Target
        }
    }
}
