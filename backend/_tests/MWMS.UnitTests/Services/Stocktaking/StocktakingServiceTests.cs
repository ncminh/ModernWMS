using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.Stocktaking
{
    public class StocktakingServiceTests
    {
        private static StocktakingService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
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
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, int skuId, int locationId, int qty)
        {
            var stock = new StockEntity { sku_id = skuId, goods_location_id = locationId, qty = qty, tenant_id = tenantId };
            dbContext.GetDbSet<StockEntity>().Add(stock);
            await dbContext.SaveChangesAsync();
            return stock;
        }

        private static async Task<StockTakingEntity> SeedStocktakingAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, int skuId, int locationId,
            int bookQty = 10, int countedQty = 0, int differenceQty = 0, bool jobStatus = false)
        {
            var entity = new StockTakingEntity
            {
                job_code = "S1",
                sku_id = skuId,
                goods_location_id = locationId,
                book_qty = bookQty,
                counted_qty = countedQty,
                difference_qty = differenceQty,
                job_status = jobStatus,
                tenant_id = tenantId,
            };
            dbContext.GetDbSet<StockTakingEntity>().Add(entity);
            await dbContext.SaveChangesAsync();
            return entity;
        }

        // PageAsync

        [Fact]
        public async Task PageAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            await SeedStocktakingAsync(scope.DbContext, 1, sku.id, location.id);
            var (_, sku2) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var location2 = await SeedLocationAsync(scope.DbContext, 2);
            await SeedStocktakingAsync(scope.DbContext, 2, sku2.id, location2.id);

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.PageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
        }

        [Fact]
        public async Task PageAsync_ComputesAdjustStatusFromExistingStockadjustRow()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var entity = await SeedStocktakingAsync(scope.DbContext, 1, sku.id, location.id);
            scope.DbContext.GetDbSet<StockAdjustEntity>().Add(new StockAdjustEntity
            {
                sku_id = sku.id,
                goods_location_id = location.id,
                job_type = 1,
                source_table_id = entity.id,
                tenant_id = 1,
            });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.PageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            data.ShouldHaveSingleItem().adjust_status.ShouldBeTrue();
        }

        // GetAsync

        [Fact]
        public async Task GetAsync_ReturnsJoinedLocationAndSkuData()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var entity = await SeedStocktakingAsync(scope.DbContext, 1, sku.id, location.id, bookQty: 12);

            var service = CreateService(scope.DbContext);
            var data = await service.GetAsync(entity.id, new CurrentUser { tenant_id = 1 });

            data.id.ShouldBe(entity.id);
            data.sku_code.ShouldBe("SKU1");
            data.location_name.ShouldBe("L1");
            data.book_qty.ShouldBe(12);
        }

        [Fact]
        public async Task GetAsync_UnknownId_ReturnsEmptyViewModel()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var data = await service.GetAsync(999, new CurrentUser { tenant_id = 1 });

            data.ShouldNotBeNull();
            data.id.ShouldBe(0);
        }

        [Fact]
        public async Task GetAsync_BelongsToDifferentTenant_ReturnsEmptyViewModel()
        {
            // Regression test: this method took no CurrentUser/tenant check at all before the fix.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var location = await SeedLocationAsync(scope.DbContext, 2);
            var entity = await SeedStocktakingAsync(scope.DbContext, 2, sku.id, location.id);

            var service = CreateService(scope.DbContext);
            var data = await service.GetAsync(entity.id, new CurrentUser { tenant_id = 1 });

            data.id.ShouldBe(0);
        }

        // AddAsync

        [Fact]
        public async Task AddAsync_NewRecord_StampsAuditColumnsAndGeneratesJobCode()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var service = CreateService(scope.DbContext);
            var currentUser = new CurrentUser { tenant_id = 1, user_name = "alice" };

            var (id, _) = await service.AddAsync(
                new StockTakingBasicViewModel { sku_id = sku.id, goods_location_id = location.id, book_qty = 10 },
                currentUser);

            id.ShouldBeGreaterThan(0);
            var saved = await scope.DbContext.GetDbSet<StockTakingEntity>().FindAsync(id);
            saved!.creator.ShouldBe("alice");
            saved.tenant_id.ShouldBe(1);
            saved.job_code.ShouldNotBeNullOrEmpty();
        }

        // PutAsync

        [Fact]
        public async Task PutAsync_UnknownId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.PutAsync(new StockTakingConfirmViewModel { id = 999, counted_qty = 5 }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task PutAsync_ExistingRecord_UpdatesCountedQtyAndComputesDifference()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var entity = await SeedStocktakingAsync(scope.DbContext, 1, sku.id, location.id, bookQty: 10);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.PutAsync(
                new StockTakingConfirmViewModel { id = entity.id, counted_qty = 7 },
                new CurrentUser { tenant_id = 1, user_name = "bob" });

            flag.ShouldBeTrue();
            var saved = await scope.DbContext.GetDbSet<StockTakingEntity>().FindAsync(entity.id);
            saved!.counted_qty.ShouldBe(7);
            saved.difference_qty.ShouldBe(-3);
            saved.job_status.ShouldBeTrue();
            saved.handler.ShouldBe("bob");
        }

        [Fact]
        public async Task PutAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            // Regression test: this method already took CurrentUser but never used it to scope the fetch.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var location = await SeedLocationAsync(scope.DbContext, 2);
            var entity = await SeedStocktakingAsync(scope.DbContext, 2, sku.id, location.id);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.PutAsync(
                new StockTakingConfirmViewModel { id = entity.id, counted_qty = 7 },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        // ConfirmAsync

        [Fact]
        public async Task ConfirmAsync_NewStockLocation_CreatesStockRowAndAdjustRecord()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var entity = await SeedStocktakingAsync(scope.DbContext, 1, sku.id, location.id, bookQty: 10, countedQty: 12, differenceQty: 2, jobStatus: true);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ConfirmAsync(entity.id, new CurrentUser { tenant_id = 1, user_name = "alice" });

            flag.ShouldBeTrue();
            var stock = await scope.DbContext.GetDbSet<StockEntity>().AsNoTracking().SingleAsync();
            stock.qty.ShouldBe(2);
            stock.tenant_id.ShouldBe(1);
            var adjust = await scope.DbContext.GetDbSet<StockAdjustEntity>().AsNoTracking().SingleAsync();
            adjust.source_table_id.ShouldBe(entity.id);
            adjust.qty.ShouldBe(2);
            adjust.job_type.ShouldBe((byte)1);
        }

        [Fact]
        public async Task ConfirmAsync_ExistingStockLocation_AdjustsQty()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var stock = await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 10);
            var entity = await SeedStocktakingAsync(scope.DbContext, 1, sku.id, location.id, bookQty: 10, countedQty: 8, differenceQty: -2, jobStatus: true);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ConfirmAsync(entity.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<StockEntity>().FindAsync(stock.id))!.qty.ShouldBe(8);
        }

        [Fact]
        public async Task ConfirmAsync_AlreadyConfirmed_ReturnsStatusChangedAndDoesNotDoubleAdjust()
        {
            // Regression test: ConfirmAsync used to have no guard against being called twice,
            // which would apply difference_qty to real stock again on every repeated call.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var stock = await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 10);
            var entity = await SeedStocktakingAsync(scope.DbContext, 1, sku.id, location.id, bookQty: 10, countedQty: 8, differenceQty: -2, jobStatus: true);

            var service = CreateService(scope.DbContext);
            var (firstFlag, _) = await service.ConfirmAsync(entity.id, new CurrentUser { tenant_id = 1 });
            var (secondFlag, _) = await service.ConfirmAsync(entity.id, new CurrentUser { tenant_id = 1 });

            firstFlag.ShouldBeTrue();
            secondFlag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<StockEntity>().FindAsync(stock.id))!.qty.ShouldBe(8);
            (await scope.DbContext.GetDbSet<StockAdjustEntity>().AsNoTracking().CountAsync()).ShouldBe(1);
        }

        [Fact]
        public async Task ConfirmAsync_UnknownId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.ConfirmAsync(999, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task ConfirmAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            // Regression test: this method already took CurrentUser but never used it to scope the
            // entity fetch, and the StockEntity mutation lookup had no tenant filter at all.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var location = await SeedLocationAsync(scope.DbContext, 2);
            var stock = await SeedStockAsync(scope.DbContext, 2, sku.id, location.id, qty: 10);
            var entity = await SeedStocktakingAsync(scope.DbContext, 2, sku.id, location.id, differenceQty: -2, jobStatus: true);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ConfirmAsync(entity.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<StockEntity>().FindAsync(stock.id))!.qty.ShouldBe(10);
        }

        // DeleteAsync

        [Fact]
        public async Task DeleteAsync_ExistingRecord_Deletes()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var entity = await SeedStocktakingAsync(scope.DbContext, 1, sku.id, location.id);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(entity.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<StockTakingEntity>().AsNoTracking().AnyAsync(t => t.id == entity.id)).ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_BelongsToDifferentTenant_DoesNotDelete()
        {
            // Regression test: this method took no CurrentUser/tenant check at all before the fix.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var location = await SeedLocationAsync(scope.DbContext, 2);
            var entity = await SeedStocktakingAsync(scope.DbContext, 2, sku.id, location.id);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(entity.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<StockTakingEntity>().AsNoTracking().AnyAsync(t => t.id == entity.id)).ShouldBeTrue();
        }
    }
}
