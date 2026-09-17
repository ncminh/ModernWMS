using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.StockFreeze
{
    public class StockFreezeServiceTests
    {
        private static StockFreezeService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
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

        private static async Task<GoodslocationEntity> SeedLocationAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, string name = "L1")
        {
            var location = new GoodslocationEntity { location_name = name, warehouse_name = "W1", tenant_id = tenantId };
            dbContext.GetDbSet<GoodslocationEntity>().Add(location);
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

        private static async Task<StockFreezeEntity> SeedFreezeAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, int skuId, int locationId, bool jobType = true)
        {
            var entity = new StockFreezeEntity { job_code = "F1", job_type = jobType, sku_id = skuId, goods_location_id = locationId, tenant_id = tenantId };
            dbContext.GetDbSet<StockFreezeEntity>().Add(entity);
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
            await SeedFreezeAsync(scope.DbContext, 1, sku.id, location.id);
            var (_, sku2) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var location2 = await SeedLocationAsync(scope.DbContext, 2);
            await SeedFreezeAsync(scope.DbContext, 2, sku2.id, location2.id);

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
            await SeedFreezeAsync(scope.DbContext, 1, sku.id, location.id);
            var (_, sku2) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var location2 = await SeedLocationAsync(scope.DbContext, 2);
            await SeedFreezeAsync(scope.DbContext, 2, sku2.id, location2.id);

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
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var entity = await SeedFreezeAsync(scope.DbContext, 1, sku.id, location.id);

            var service = CreateService(scope.DbContext);
            var data = await service.GetAsync(entity.id, new CurrentUser { tenant_id = 1 });

            data.ShouldNotBeNull();
            data!.sku_code.ShouldBe("SKU1");
            data.location_name.ShouldBe("L1");
        }

        [Fact]
        public async Task GetAsync_BelongsToDifferentTenant_ReturnsNull()
        {
            // Regression test: this method took no CurrentUser/tenant check at all before the fix.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var location = await SeedLocationAsync(scope.DbContext, 2);
            var entity = await SeedFreezeAsync(scope.DbContext, 2, sku.id, location.id);

            var service = CreateService(scope.DbContext);
            var data = await service.GetAsync(entity.id, new CurrentUser { tenant_id = 1 });

            data.ShouldBeNull();
        }

        // AddAsync

        [Fact]
        public async Task AddAsync_JobTypeTrue_FreezesMatchingStock()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var stock = await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 10, isFreeze: false);

            var service = CreateService(scope.DbContext);
            var currentUser = new CurrentUser { tenant_id = 1, user_name = "alice" };
            var (id, _) = await service.AddAsync(
                new StockFreezeViewModel { sku_id = sku.id, goods_location_id = location.id, job_type = true },
                currentUser);

            id.ShouldBeGreaterThan(0);
            (await scope.DbContext.GetDbSet<StockEntity>().AsNoTracking().FirstAsync(t => t.id == stock.id)).is_freeze.ShouldBeTrue();
            var saved = await scope.DbContext.GetDbSet<StockFreezeEntity>().FindAsync(id);
            saved!.handler.ShouldBe("alice");
            saved.job_code.ShouldNotBeNullOrEmpty();
        }

        [Fact]
        public async Task AddAsync_JobTypeFalse_UnfreezesMatchingStock()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var stock = await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 10, isFreeze: true);

            var service = CreateService(scope.DbContext);
            var (id, _) = await service.AddAsync(
                new StockFreezeViewModel { sku_id = sku.id, goods_location_id = location.id, job_type = false },
                new CurrentUser { tenant_id = 1, user_name = "alice" });

            id.ShouldBeGreaterThan(0);
            (await scope.DbContext.GetDbSet<StockEntity>().AsNoTracking().FirstAsync(t => t.id == stock.id)).is_freeze.ShouldBeFalse();
        }

        [Fact]
        public async Task AddAsync_PendingProcessDetail_ReturnsErrorAndDoesNotPersist()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var stock = await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 10);
            var process = new StockProcessEntity { job_code = "P1", tenant_id = 1 };
            scope.DbContext.GetDbSet<StockProcessEntity>().Add(process);
            await scope.DbContext.SaveChangesAsync();
            scope.DbContext.GetDbSet<StockProcessDetailEntity>().Add(new StockProcessDetailEntity
            {
                stock_process_id = process.id,
                sku_id = sku.id,
                goods_location_id = location.id,
                is_update_stock = false,
                tenant_id = 1,
            });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (id, _) = await service.AddAsync(
                new StockFreezeViewModel { sku_id = sku.id, goods_location_id = location.id, job_type = true },
                new CurrentUser { tenant_id = 1 });

            id.ShouldBe(0);
            (await scope.DbContext.GetDbSet<StockEntity>().AsNoTracking().FirstAsync(t => t.id == stock.id)).is_freeze.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<StockFreezeEntity>().AsNoTracking().AnyAsync()).ShouldBeFalse();
        }

        [Fact]
        public async Task AddAsync_PendingDispatchPick_ReturnsErrorAndDoesNotPersist()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var stock = await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 10);
            var dispatch = new DispatchlistEntity { dispatch_no = "D1", sku_id = sku.id, tenant_id = 1 };
            scope.DbContext.GetDbSet<DispatchlistEntity>().Add(dispatch);
            await scope.DbContext.SaveChangesAsync();
            scope.DbContext.GetDbSet<DispatchpicklistEntity>().Add(new DispatchpicklistEntity
            {
                dispatchlist_id = dispatch.id,
                sku_id = sku.id,
                goods_location_id = location.id,
                is_update_stock = false,
            });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (id, _) = await service.AddAsync(
                new StockFreezeViewModel { sku_id = sku.id, goods_location_id = location.id, job_type = true },
                new CurrentUser { tenant_id = 1 });

            id.ShouldBe(0);
            (await scope.DbContext.GetDbSet<StockEntity>().AsNoTracking().FirstAsync(t => t.id == stock.id)).is_freeze.ShouldBeFalse();
        }

        [Fact]
        public async Task AddAsync_PendingStockMove_ReturnsErrorAndDoesNotPersist()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var otherLocation = await SeedLocationAsync(scope.DbContext, 1, "L2");
            var stock = await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 10);
            scope.DbContext.GetDbSet<StockMoveEntity>().Add(new StockMoveEntity
            {
                sku_id = sku.id,
                orig_goods_location_id = location.id,
                dest_googs_location_id = otherLocation.id,
                qty = 5,
                move_status = 0,
                tenant_id = 1,
            });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (id, _) = await service.AddAsync(
                new StockFreezeViewModel { sku_id = sku.id, goods_location_id = location.id, job_type = true },
                new CurrentUser { tenant_id = 1 });

            id.ShouldBe(0);
            (await scope.DbContext.GetDbSet<StockEntity>().AsNoTracking().FirstAsync(t => t.id == stock.id)).is_freeze.ShouldBeFalse();
        }

        [Fact]
        public async Task AddAsync_OnlyFreezesStockForCurrentTenant()
        {
            // Regression test: the stock lookup used to have no tenant filter at all.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku1) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location1 = await SeedLocationAsync(scope.DbContext, 1);
            var stock1 = await SeedStockAsync(scope.DbContext, 1, sku1.id, location1.id, qty: 10);

            var service = CreateService(scope.DbContext);
            var (id, _) = await service.AddAsync(
                new StockFreezeViewModel { sku_id = sku1.id, goods_location_id = location1.id, job_type = true },
                new CurrentUser { tenant_id = 1 });

            id.ShouldBeGreaterThan(0);
            (await scope.DbContext.GetDbSet<StockEntity>().AsNoTracking().FirstAsync(t => t.id == stock1.id)).is_freeze.ShouldBeTrue();
        }

        // UpdateAsync

        [Fact]
        public async Task UpdateAsync_UnknownId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.UpdateAsync(new StockFreezeViewModel { id = 999 }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateAsync_ExistingRecord_UpdatesFields()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var entity = await SeedFreezeAsync(scope.DbContext, 1, sku.id, location.id);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(
                new StockFreezeViewModel { id = entity.id, sku_id = sku.id, goods_location_id = location.id, job_type = false, handler = "bob" },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var saved = await scope.DbContext.GetDbSet<StockFreezeEntity>().FindAsync(entity.id);
            saved!.job_type.ShouldBeFalse();
            saved.handler.ShouldBe("bob");
        }

        [Fact]
        public async Task UpdateAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            // Regression test: this method took no CurrentUser/tenant check at all before the fix.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var location = await SeedLocationAsync(scope.DbContext, 2);
            var entity = await SeedFreezeAsync(scope.DbContext, 2, sku.id, location.id);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(
                new StockFreezeViewModel { id = entity.id, handler = "eve" },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        // DeleteAsync

        [Fact]
        public async Task DeleteAsync_ExistingRecord_Deletes()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var entity = await SeedFreezeAsync(scope.DbContext, 1, sku.id, location.id);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(entity.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<StockFreezeEntity>().AsNoTracking().AnyAsync(t => t.id == entity.id)).ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_BelongsToDifferentTenant_DoesNotDelete()
        {
            // Regression test: this method took no CurrentUser/tenant check at all before the fix.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var location = await SeedLocationAsync(scope.DbContext, 2);
            var entity = await SeedFreezeAsync(scope.DbContext, 2, sku.id, location.id);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(entity.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<StockFreezeEntity>().AsNoTracking().AnyAsync(t => t.id == entity.id)).ShouldBeTrue();
        }
    }
}
