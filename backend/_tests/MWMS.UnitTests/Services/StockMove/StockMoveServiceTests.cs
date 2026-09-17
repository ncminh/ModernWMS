using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.Stockmove
{
    public class StockMoveServiceTests
    {
        private static StockMoveService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
            new(dbContext, new FakeStringLocalizer<MultiLanguage>(), TestFunctionHelperFactory.Create(dbContext));

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

        private static async Task<GoodsLocationEntity> SeedLocationAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, string name = "L1")
        {
            var location = new GoodsLocationEntity { location_name = name, warehouse_name = "W1", tenant_id = tenantId };
            dbContext.GetDbSet<GoodsLocationEntity>().Add(location);
            await dbContext.SaveChangesAsync();
            return location;
        }

        private static async Task<StockEntity> SeedStockAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, int skuId, int locationId, int qty, bool isFreeze = false)
        {
            var stock = new StockEntity { sku_id = skuId, goods_location_id = locationId, qty = qty, is_freeze = isFreeze, tenant_id = tenantId };
            dbContext.GetDbSet<StockEntity>().Add(stock);
            await dbContext.SaveChangesAsync();
            return stock;
        }

        private static async Task<StockMoveEntity> SeedMoveAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, int skuId, int origLocationId, int destLocationId,
            int qty, byte moveStatus = 0)
        {
            var move = new StockMoveEntity
            {
                job_code = "M1",
                move_status = moveStatus,
                sku_id = skuId,
                orig_goods_location_id = origLocationId,
                dest_googs_location_id = destLocationId,
                qty = qty,
                tenant_id = tenantId,
            };
            dbContext.GetDbSet<StockMoveEntity>().Add(move);
            await dbContext.SaveChangesAsync();
            return move;
        }

        // PageAsync / GetAllAsync

        [Fact]
        public async Task PageAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var orig = await SeedLocationAsync(scope.DbContext, 1, "Orig");
            var dest = await SeedLocationAsync(scope.DbContext, 1, "Dest");
            await SeedMoveAsync(scope.DbContext, 1, sku.id, orig.id, dest.id, qty: 5);
            var (_, sku2) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var orig2 = await SeedLocationAsync(scope.DbContext, 2, "Orig2");
            var dest2 = await SeedLocationAsync(scope.DbContext, 2, "Dest2");
            await SeedMoveAsync(scope.DbContext, 2, sku2.id, orig2.id, dest2.id, qty: 5);

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.PageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
        }

        [Fact]
        public async Task GetAllAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var orig = await SeedLocationAsync(scope.DbContext, 1, "Orig");
            var dest = await SeedLocationAsync(scope.DbContext, 1, "Dest");
            await SeedMoveAsync(scope.DbContext, 1, sku.id, orig.id, dest.id, qty: 5);
            var (_, sku2) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var orig2 = await SeedLocationAsync(scope.DbContext, 2, "Orig2");
            var dest2 = await SeedLocationAsync(scope.DbContext, 2, "Dest2");
            await SeedMoveAsync(scope.DbContext, 2, sku2.id, orig2.id, dest2.id, qty: 5);

            var service = CreateService(scope.DbContext);
            var data = await service.GetAllAsync(new CurrentUser { tenant_id = 1 });

            data.ShouldHaveSingleItem();
        }

        // GetAsync

        [Fact]
        public async Task GetAsync_ReturnsJoinedLocationAndSkuData()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var orig = await SeedLocationAsync(scope.DbContext, 1, "Orig");
            var dest = await SeedLocationAsync(scope.DbContext, 1, "Dest");
            var move = await SeedMoveAsync(scope.DbContext, 1, sku.id, orig.id, dest.id, qty: 5);

            var service = CreateService(scope.DbContext);
            var data = await service.GetAsync(move.id, new CurrentUser { tenant_id = 1 });

            data.ShouldNotBeNull();
            data!.sku_code.ShouldBe("SKU1");
            data.orig_goods_location_name.ShouldBe("Orig");
            data.dest_googs_location_name.ShouldBe("Dest");
        }

        [Fact]
        public async Task GetAsync_BelongsToDifferentTenant_ReturnsNull()
        {
            // Regression test: this method took no CurrentUser/tenant check at all before the Phase 6 fix.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var orig = await SeedLocationAsync(scope.DbContext, 2, "Orig");
            var dest = await SeedLocationAsync(scope.DbContext, 2, "Dest");
            var move = await SeedMoveAsync(scope.DbContext, 2, sku.id, orig.id, dest.id, qty: 5);

            var service = CreateService(scope.DbContext);
            var data = await service.GetAsync(move.id, new CurrentUser { tenant_id = 1 });

            data.ShouldBeNull();
        }

        // AddAsync

        [Fact]
        public async Task AddAsync_SufficientAvailableQty_CreatesMoveAndStampsAuditColumns()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var orig = await SeedLocationAsync(scope.DbContext, 1, "Orig");
            var dest = await SeedLocationAsync(scope.DbContext, 1, "Dest");
            await SeedStockAsync(scope.DbContext, 1, sku.id, orig.id, qty: 10);

            var service = CreateService(scope.DbContext);
            var currentUser = new CurrentUser { tenant_id = 1, user_name = "alice" };
            var (id, _) = await service.AddAsync(
                new StockMoveViewModel { sku_id = sku.id, orig_goods_location_id = orig.id, dest_googs_location_id = dest.id, qty = 5 },
                currentUser);

            id.ShouldBeGreaterThan(0);
            var saved = await scope.DbContext.GetDbSet<StockMoveEntity>().FindAsync(id);
            saved!.creator.ShouldBe("alice");
            saved.tenant_id.ShouldBe(1);
            saved.job_code.ShouldNotBeNullOrEmpty();
            saved.move_status.ShouldBe((byte)0);
        }

        [Fact]
        public async Task AddAsync_InsufficientAvailableQty_ReturnsError()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var orig = await SeedLocationAsync(scope.DbContext, 1, "Orig");
            var dest = await SeedLocationAsync(scope.DbContext, 1, "Dest");
            await SeedStockAsync(scope.DbContext, 1, sku.id, orig.id, qty: 3);

            var service = CreateService(scope.DbContext);
            var (id, _) = await service.AddAsync(
                new StockMoveViewModel { sku_id = sku.id, orig_goods_location_id = orig.id, dest_googs_location_id = dest.id, qty = 5 },
                new CurrentUser { tenant_id = 1 });

            id.ShouldBe(0);
        }

        [Fact]
        public async Task AddAsync_DestStockFrozen_ReturnsError()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var orig = await SeedLocationAsync(scope.DbContext, 1, "Orig");
            var dest = await SeedLocationAsync(scope.DbContext, 1, "Dest");
            await SeedStockAsync(scope.DbContext, 1, sku.id, orig.id, qty: 10);
            await SeedStockAsync(scope.DbContext, 1, sku.id, dest.id, qty: 2, isFreeze: true);

            var service = CreateService(scope.DbContext);
            var (id, _) = await service.AddAsync(
                new StockMoveViewModel { sku_id = sku.id, orig_goods_location_id = orig.id, dest_googs_location_id = dest.id, qty = 5 },
                new CurrentUser { tenant_id = 1 });

            id.ShouldBe(0);
        }

        [Fact]
        public async Task AddAsync_DestLocationBelongsToDifferentTenant_ReturnsError()
        {
            // Regression test: neither orig_goods_location_id nor dest_googs_location_id
            // were validated against the caller's tenant before the Phase 6 fix.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var orig = await SeedLocationAsync(scope.DbContext, 1, "Orig");
            var otherTenantDest = await SeedLocationAsync(scope.DbContext, 2, "OtherTenantDest");
            await SeedStockAsync(scope.DbContext, 1, sku.id, orig.id, qty: 10);

            var service = CreateService(scope.DbContext);
            var (id, _) = await service.AddAsync(
                new StockMoveViewModel { sku_id = sku.id, orig_goods_location_id = orig.id, dest_googs_location_id = otherTenantDest.id, qty = 5 },
                new CurrentUser { tenant_id = 1 });

            id.ShouldBe(0);
        }

        // Confirm

        [Fact]
        public async Task Confirm_OrigStockFullyConsumed_RemovesOrigStockRow()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var orig = await SeedLocationAsync(scope.DbContext, 1, "Orig");
            var dest = await SeedLocationAsync(scope.DbContext, 1, "Dest");
            var origStock = await SeedStockAsync(scope.DbContext, 1, sku.id, orig.id, qty: 5);
            var move = await SeedMoveAsync(scope.DbContext, 1, sku.id, orig.id, dest.id, qty: 5);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.Confirm(move.id, new CurrentUser { tenant_id = 1, user_name = "alice" });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<StockEntity>().AsNoTracking().AnyAsync(t => t.id == origStock.id)).ShouldBeFalse();
            var destStock = await scope.DbContext.GetDbSet<StockEntity>().AsNoTracking().SingleAsync();
            destStock.goods_location_id.ShouldBe(dest.id);
            destStock.qty.ShouldBe(5);
            (await scope.DbContext.GetDbSet<StockMoveEntity>().FindAsync(move.id))!.move_status.ShouldBe((byte)1);
        }

        [Fact]
        public async Task Confirm_OrigStockPartiallyConsumed_DecrementsQty()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var orig = await SeedLocationAsync(scope.DbContext, 1, "Orig");
            var dest = await SeedLocationAsync(scope.DbContext, 1, "Dest");
            var origStock = await SeedStockAsync(scope.DbContext, 1, sku.id, orig.id, qty: 10);
            var move = await SeedMoveAsync(scope.DbContext, 1, sku.id, orig.id, dest.id, qty: 4);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.Confirm(move.id, new CurrentUser { tenant_id = 1, user_name = "alice" });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<StockEntity>().FindAsync(origStock.id))!.qty.ShouldBe(6);
        }

        [Fact]
        public async Task Confirm_DestStockAlreadyExistsForSameSku_IncrementsExistingRowInsteadOfDuplicating()
        {
            // Regression test: the dest_stock lookup used to filter "t.sku_id != entity.sku_id"
            // (backwards), which meant it could never find the correct existing row and always
            // created a brand-new duplicate StockEntity row instead of incrementing.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var orig = await SeedLocationAsync(scope.DbContext, 1, "Orig");
            var dest = await SeedLocationAsync(scope.DbContext, 1, "Dest");
            await SeedStockAsync(scope.DbContext, 1, sku.id, orig.id, qty: 10);
            var existingDestStock = await SeedStockAsync(scope.DbContext, 1, sku.id, dest.id, qty: 3);
            var move = await SeedMoveAsync(scope.DbContext, 1, sku.id, orig.id, dest.id, qty: 4);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.Confirm(move.id, new CurrentUser { tenant_id = 1, user_name = "alice" });

            flag.ShouldBeTrue();
            var destRows = await scope.DbContext.GetDbSet<StockEntity>().AsNoTracking().Where(t => t.goods_location_id == dest.id).ToListAsync();
            destRows.ShouldHaveSingleItem().id.ShouldBe(existingDestStock.id);
            destRows[0].qty.ShouldBe(7);
        }

        [Fact]
        public async Task Confirm_DestStockDoesNotExist_CreatesNewRow()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var orig = await SeedLocationAsync(scope.DbContext, 1, "Orig");
            var dest = await SeedLocationAsync(scope.DbContext, 1, "Dest");
            await SeedStockAsync(scope.DbContext, 1, sku.id, orig.id, qty: 10);
            var move = await SeedMoveAsync(scope.DbContext, 1, sku.id, orig.id, dest.id, qty: 4);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.Confirm(move.id, new CurrentUser { tenant_id = 1, user_name = "alice" });

            flag.ShouldBeTrue();
            var destStock = await scope.DbContext.GetDbSet<StockEntity>().AsNoTracking().SingleAsync(t => t.goods_location_id == dest.id);
            destStock.qty.ShouldBe(4);
            destStock.tenant_id.ShouldBe(1);
        }

        [Fact]
        public async Task Confirm_UnknownId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.Confirm(999, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task Confirm_BelongsToDifferentTenant_ReturnsNotExists()
        {
            // Regression test: this method had no tenant check on the entity fetch before the Phase 6 fix.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var orig = await SeedLocationAsync(scope.DbContext, 2, "Orig");
            var dest = await SeedLocationAsync(scope.DbContext, 2, "Dest");
            await SeedStockAsync(scope.DbContext, 2, sku.id, orig.id, qty: 10);
            var move = await SeedMoveAsync(scope.DbContext, 2, sku.id, orig.id, dest.id, qty: 4);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.Confirm(move.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<StockMoveEntity>().FindAsync(move.id))!.move_status.ShouldBe((byte)0);
        }

        // DeleteAsync

        [Fact]
        public async Task DeleteAsync_StatusZero_DeletesRow()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var orig = await SeedLocationAsync(scope.DbContext, 1, "Orig");
            var dest = await SeedLocationAsync(scope.DbContext, 1, "Dest");
            var move = await SeedMoveAsync(scope.DbContext, 1, sku.id, orig.id, dest.id, qty: 5, moveStatus: 0);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(move.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<StockMoveEntity>().AsNoTracking().AnyAsync(t => t.id == move.id)).ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_NonZeroStatus_IsBlocked()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var orig = await SeedLocationAsync(scope.DbContext, 1, "Orig");
            var dest = await SeedLocationAsync(scope.DbContext, 1, "Dest");
            var move = await SeedMoveAsync(scope.DbContext, 1, sku.id, orig.id, dest.id, qty: 5, moveStatus: 1);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(move.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<StockMoveEntity>().AsNoTracking().AnyAsync(t => t.id == move.id)).ShouldBeTrue();
        }

        [Fact]
        public async Task DeleteAsync_BelongsToDifferentTenant_DoesNotDelete()
        {
            // Regression test: this method took no CurrentUser/tenant check at all before the Phase 6 fix.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var orig = await SeedLocationAsync(scope.DbContext, 2, "Orig");
            var dest = await SeedLocationAsync(scope.DbContext, 2, "Dest");
            var move = await SeedMoveAsync(scope.DbContext, 2, sku.id, orig.id, dest.id, qty: 5, moveStatus: 0);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(move.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<StockMoveEntity>().AsNoTracking().AnyAsync(t => t.id == move.id)).ShouldBeTrue();
        }
    }
}
