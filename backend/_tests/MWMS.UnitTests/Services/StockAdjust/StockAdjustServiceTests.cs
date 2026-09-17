using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.StockAdjust
{
    public class StockAdjustServiceTests
    {
        private static StockAdjustService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
            new(dbContext, new FakeStringLocalizer<MultiLanguage>());

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

        private static async Task<StockAdjustEntity> SeedStockadjustAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, int skuId, int locationId,
            int qty = 5, byte jobType = 1, int sourceTableId = 0)
        {
            var entity = new StockAdjustEntity
            {
                job_code = "J1",
                sku_id = skuId,
                goods_location_id = locationId,
                qty = qty,
                job_type = jobType,
                source_table_id = sourceTableId,
                tenant_id = tenantId,
            };
            dbContext.GetDbSet<StockAdjustEntity>().Add(entity);
            await dbContext.SaveChangesAsync();
            return entity;
        }

        // PageAsync / GetAllAsync

        [Fact]
        public async Task PageAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            await SeedStockadjustAsync(scope.DbContext, 1, sku.id, location.id);
            var (_, sku2) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var location2 = await SeedLocationAsync(scope.DbContext, 2);
            await SeedStockadjustAsync(scope.DbContext, 2, sku2.id, location2.id);

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.PageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
        }

        [Fact]
        public async Task GetAllAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            await SeedStockadjustAsync(scope.DbContext, 1, sku.id, location.id);
            var (_, sku2) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var location2 = await SeedLocationAsync(scope.DbContext, 2);
            await SeedStockadjustAsync(scope.DbContext, 2, sku2.id, location2.id);

            var service = CreateService(scope.DbContext);
            var data = await service.GetAllAsync(new CurrentUser { tenant_id = 1 });

            data.ShouldHaveSingleItem();
        }

        // GetAsync

        [Fact]
        public async Task GetAsync_ReturnsRecord()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var entity = await SeedStockadjustAsync(scope.DbContext, 1, sku.id, location.id, qty: 7);

            var service = CreateService(scope.DbContext);
            var data = await service.GetAsync(entity.id, new CurrentUser { tenant_id = 1 });

            data.ShouldNotBeNull();
            data!.qty.ShouldBe(7);
        }

        [Fact]
        public async Task GetAsync_BelongsToDifferentTenant_ReturnsNull()
        {
            // Regression test: this method took no CurrentUser/tenant check at all before the fix.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var location = await SeedLocationAsync(scope.DbContext, 2);
            var entity = await SeedStockadjustAsync(scope.DbContext, 2, sku.id, location.id);

            var service = CreateService(scope.DbContext);
            var data = await service.GetAsync(entity.id, new CurrentUser { tenant_id = 1 });

            data.ShouldBeNull();
        }

        // AddAsync

        [Fact]
        public async Task AddAsync_NewRecord_StampsAuditColumns()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var service = CreateService(scope.DbContext);
            var currentUser = new CurrentUser { tenant_id = 1, user_name = "alice" };

            var (id, _) = await service.AddAsync(
                new StockAdjustViewModel { sku_id = sku.id, goods_location_id = location.id, qty = 5, job_type = 1 },
                currentUser);

            id.ShouldBeGreaterThan(0);
            var saved = await scope.DbContext.GetDbSet<StockAdjustEntity>().FindAsync(id);
            saved!.creator.ShouldBe("alice");
            saved.tenant_id.ShouldBe(1);
        }

        // UpdateAsync

        [Fact]
        public async Task UpdateAsync_UnknownId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.UpdateAsync(new StockAdjustViewModel { id = 999 }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateAsync_ExistingRecord_UpdatesQty()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var entity = await SeedStockadjustAsync(scope.DbContext, 1, sku.id, location.id, qty: 5);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(
                new StockAdjustViewModel { id = entity.id, sku_id = sku.id, goods_location_id = location.id, qty = 9 },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<StockAdjustEntity>().FindAsync(entity.id))!.qty.ShouldBe(9);
        }

        [Fact]
        public async Task UpdateAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            // Regression test: this method took no CurrentUser/tenant check at all before the fix.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var location = await SeedLocationAsync(scope.DbContext, 2);
            var entity = await SeedStockadjustAsync(scope.DbContext, 2, sku.id, location.id, qty: 5);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(
                new StockAdjustViewModel { id = entity.id, qty = 99 },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<StockAdjustEntity>().FindAsync(entity.id))!.qty.ShouldBe(5);
        }

        // DeleteAsync

        [Fact]
        public async Task DeleteAsync_ExistingRecord_Deletes()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var entity = await SeedStockadjustAsync(scope.DbContext, 1, sku.id, location.id);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(entity.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<StockAdjustEntity>().AsNoTracking().AnyAsync(t => t.id == entity.id)).ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_UnknownId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.DeleteAsync(999, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_BelongsToDifferentTenant_DoesNotDelete()
        {
            // Regression test: this method took no CurrentUser/tenant check at all before the fix.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var location = await SeedLocationAsync(scope.DbContext, 2);
            var entity = await SeedStockadjustAsync(scope.DbContext, 2, sku.id, location.id);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(entity.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<StockAdjustEntity>().AsNoTracking().AnyAsync(t => t.id == entity.id)).ShouldBeTrue();
        }

        // ConfirmAdjustment

        [Fact]
        public async Task ConfirmAdjustment_NewStockLocation_CreatesAndPersistsStockRow()
        {
            // Regression test: the "no existing stock" branch used to build a new StockEntity
            // but never call Add() on it, so it was silently never persisted - confirming an
            // adjustment for a brand-new sku/location combo did nothing to real inventory.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var entity = await SeedStockadjustAsync(scope.DbContext, 1, sku.id, location.id, qty: 8);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ConfirmAdjustment(entity.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var stock = await scope.DbContext.GetDbSet<StockEntity>().AsNoTracking().SingleAsync();
            stock.sku_id.ShouldBe(sku.id);
            stock.goods_location_id.ShouldBe(location.id);
            stock.qty.ShouldBe(8);
            stock.tenant_id.ShouldBe(1);
            (await scope.DbContext.GetDbSet<StockAdjustEntity>().FindAsync(entity.id))!.is_update_stock.ShouldBeTrue();
        }

        [Fact]
        public async Task ConfirmAdjustment_ExistingStockLocation_IncrementsQty()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var stock = new StockEntity { sku_id = sku.id, goods_location_id = location.id, qty = 10, tenant_id = 1 };
            scope.DbContext.GetDbSet<StockEntity>().Add(stock);
            await scope.DbContext.SaveChangesAsync();
            var entity = await SeedStockadjustAsync(scope.DbContext, 1, sku.id, location.id, qty: 3);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ConfirmAdjustment(entity.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var rows = await scope.DbContext.GetDbSet<StockEntity>().AsNoTracking().ToListAsync();
            rows.ShouldHaveSingleItem().qty.ShouldBe(13);
        }

        [Fact]
        public async Task ConfirmAdjustment_JobTypeProcess_MarksProcessDetailUpdated()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var process = new StockProcessEntity { job_code = "P1", tenant_id = 1 };
            scope.DbContext.GetDbSet<StockProcessEntity>().Add(process);
            await scope.DbContext.SaveChangesAsync();
            var processDetail = new StockProcessDetailEntity { stock_process_id = process.id, sku_id = sku.id, goods_location_id = location.id, qty = 3, is_update_stock = false, tenant_id = 1 };
            scope.DbContext.GetDbSet<StockProcessDetailEntity>().Add(processDetail);
            await scope.DbContext.SaveChangesAsync();
            var entity = await SeedStockadjustAsync(scope.DbContext, 1, sku.id, location.id, qty: 3, jobType: 2, sourceTableId: processDetail.id);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ConfirmAdjustment(entity.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<StockProcessDetailEntity>().FindAsync(processDetail.id))!.is_update_stock.ShouldBeTrue();
        }

        [Fact]
        public async Task ConfirmAdjustment_UnknownId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.ConfirmAdjustment(999, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task ConfirmAdjustment_BelongsToDifferentTenant_ReturnsNotExists()
        {
            // Regression test: this method took no CurrentUser/tenant check at all before the fix.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var location = await SeedLocationAsync(scope.DbContext, 2);
            var entity = await SeedStockadjustAsync(scope.DbContext, 2, sku.id, location.id, qty: 8);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ConfirmAdjustment(entity.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<StockEntity>().AsNoTracking().AnyAsync()).ShouldBeFalse();
        }
    }
}
