using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.Stockprocess
{
    public class StockProcessServiceTests
    {
        private static StockProcessService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
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

        private static async Task<StockProcessEntity> SeedProcessAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, bool processStatus = false)
        {
            var process = new StockProcessEntity { job_code = "P1", tenant_id = tenantId, process_status = processStatus };
            dbContext.GetDbSet<StockProcessEntity>().Add(process);
            await dbContext.SaveChangesAsync();
            return process;
        }

        private static async Task<StockProcessDetailEntity> SeedDetailAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, int processId, int skuId, int locationId,
            int qty, bool isSource, bool isUpdateStock = false)
        {
            var detail = new StockProcessDetailEntity
            {
                stock_process_id = processId,
                sku_id = skuId,
                goods_location_id = locationId,
                qty = qty,
                is_source = isSource,
                is_update_stock = isUpdateStock,
                tenant_id = tenantId,
            };
            dbContext.GetDbSet<StockProcessDetailEntity>().Add(detail);
            await dbContext.SaveChangesAsync();
            return detail;
        }

        // PageAsync / GetAllAsync

        [Fact]
        public async Task PageAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            await SeedProcessAsync(scope.DbContext, 1);
            await SeedProcessAsync(scope.DbContext, 2);

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.PageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
        }

        [Fact]
        public async Task GetAllAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            await SeedProcessAsync(scope.DbContext, 1);
            await SeedProcessAsync(scope.DbContext, 2);

            var service = CreateService(scope.DbContext);
            var data = await service.GetAllAsync(new CurrentUser { tenant_id = 1 });

            data.ShouldHaveSingleItem();
        }

        // GetAsync

        [Fact]
        public async Task GetAsync_ReturnsSourceAndTargetDetailLists()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var process = await SeedProcessAsync(scope.DbContext, 1);
            await SeedDetailAsync(scope.DbContext, 1, process.id, sku.id, location.id, qty: 5, isSource: true);
            await SeedDetailAsync(scope.DbContext, 1, process.id, sku.id, location.id, qty: 5, isSource: false);

            var service = CreateService(scope.DbContext);
            var data = await service.GetAsync(process.id, new CurrentUser { tenant_id = 1 });

            data.ShouldNotBeNull();
            data!.source_detail_list.ShouldHaveSingleItem();
            data.target_detail_list.ShouldHaveSingleItem();
        }

        [Fact]
        public async Task GetAsync_BelongsToDifferentTenant_ReturnsNull()
        {
            // Regression test: this method took no CurrentUser/tenant check at all before the fix.
            using var scope = new SqliteTestDbContextScope();
            var process = await SeedProcessAsync(scope.DbContext, 2);

            var service = CreateService(scope.DbContext);
            var data = await service.GetAsync(process.id, new CurrentUser { tenant_id = 1 });

            data.ShouldBeNull();
        }

        // AddAsync

        [Fact]
        public async Task AddAsync_SufficientSourceStock_CreatesRecordWithDetails()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 10);

            var service = CreateService(scope.DbContext);
            var currentUser = new CurrentUser { tenant_id = 1, user_name = "alice" };
            var viewModel = new StockProcessViewModel
            {
                job_type = true,
                detailList = new List<StockProcessDetailViewModel>
                {
                    new() { sku_id = sku.id, goods_location_id = location.id, qty = 5, is_source = true },
                },
            };

            var (id, _) = await service.AddAsync(viewModel, currentUser);

            id.ShouldBeGreaterThan(0);
            var saved = await scope.DbContext.GetDbSet<StockProcessEntity>().FindAsync(id);
            saved!.creator.ShouldBe("alice");
            saved.job_code.ShouldNotBeNullOrEmpty();
            var details = await scope.DbContext.GetDbSet<StockProcessDetailEntity>().AsNoTracking().Where(t => t.stock_process_id == id).ToListAsync();
            details.ShouldHaveSingleItem().qty.ShouldBe(5);
        }

        [Fact]
        public async Task AddAsync_InsufficientSourceStock_ReturnsDataChanged()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 3);

            var service = CreateService(scope.DbContext);
            var viewModel = new StockProcessViewModel
            {
                detailList = new List<StockProcessDetailViewModel>
                {
                    new() { sku_id = sku.id, goods_location_id = location.id, qty = 5, is_source = true },
                },
            };

            var (id, _) = await service.AddAsync(viewModel, new CurrentUser { tenant_id = 1 });

            id.ShouldBe(0);
            (await scope.DbContext.GetDbSet<StockProcessEntity>().AsNoTracking().AnyAsync()).ShouldBeFalse();
        }

        [Fact]
        public async Task AddAsync_SourceStockFrozen_ReturnsError()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 10, isFreeze: true);

            var service = CreateService(scope.DbContext);
            var viewModel = new StockProcessViewModel
            {
                detailList = new List<StockProcessDetailViewModel>
                {
                    new() { sku_id = sku.id, goods_location_id = location.id, qty = 5, is_source = true },
                },
            };

            var (id, _) = await service.AddAsync(viewModel, new CurrentUser { tenant_id = 1 });

            id.ShouldBe(0);
        }

        [Fact]
        public async Task AddAsync_SourceStockBelongsToDifferentTenant_ReturnsDataChanged()
        {
            // Regression test: the stock lookup used to have no tenant filter at all.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var location = await SeedLocationAsync(scope.DbContext, 2);
            await SeedStockAsync(scope.DbContext, 2, sku.id, location.id, qty: 10);

            var service = CreateService(scope.DbContext);
            var viewModel = new StockProcessViewModel
            {
                detailList = new List<StockProcessDetailViewModel>
                {
                    new() { sku_id = sku.id, goods_location_id = location.id, qty = 5, is_source = true },
                },
            };

            var (id, _) = await service.AddAsync(viewModel, new CurrentUser { tenant_id = 1 });

            id.ShouldBe(0);
        }

        [Fact]
        public async Task AddAsync_PendingLockedProcessDetail_ReducesAvailabilityAndReturnsDataChanged()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 10);
            var otherProcess = await SeedProcessAsync(scope.DbContext, 1);
            await SeedDetailAsync(scope.DbContext, 1, otherProcess.id, sku.id, location.id, qty: 8, isSource: true, isUpdateStock: false);

            var service = CreateService(scope.DbContext);
            var viewModel = new StockProcessViewModel
            {
                detailList = new List<StockProcessDetailViewModel>
                {
                    new() { sku_id = sku.id, goods_location_id = location.id, qty = 5, is_source = true },
                },
            };

            var (id, _) = await service.AddAsync(viewModel, new CurrentUser { tenant_id = 1 });

            id.ShouldBe(0);
        }

        // UpdateAsync

        [Fact]
        public async Task UpdateAsync_UnknownId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.UpdateAsync(new StockProcessViewModel { id = 999 }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateAsync_ExistingRecord_UpdatesFields()
        {
            using var scope = new SqliteTestDbContextScope();
            var process = await SeedProcessAsync(scope.DbContext, 1);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(
                new StockProcessViewModel { id = process.id, job_code = "P2", processor = "bob" },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var saved = await scope.DbContext.GetDbSet<StockProcessEntity>().FindAsync(process.id);
            saved!.job_code.ShouldBe("P2");
            saved.processor.ShouldBe("bob");
        }

        [Fact]
        public async Task UpdateAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            // Regression test: this method took no CurrentUser/tenant check at all before the fix.
            using var scope = new SqliteTestDbContextScope();
            var process = await SeedProcessAsync(scope.DbContext, 2);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(
                new StockProcessViewModel { id = process.id, processor = "eve" },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        // DeleteAsync

        [Fact]
        public async Task DeleteAsync_UnprocessedRecord_Deletes()
        {
            using var scope = new SqliteTestDbContextScope();
            var process = await SeedProcessAsync(scope.DbContext, 1, processStatus: false);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(process.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<StockProcessEntity>().AsNoTracking().AnyAsync(t => t.id == process.id)).ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_UnknownId_ReturnsNotExistsInsteadOfThrowing()
        {
            // Regression test: this method used to call Remove(entity) without checking
            // for null first, throwing an unhandled exception instead of a clean failure.
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.DeleteAsync(999, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_AlreadyProcessed_IsBlockedInsteadOfThrowing()
        {
            // Same regression as above: an already-processed record doesn't match the
            // process_status == false filter, so entity comes back null here too.
            using var scope = new SqliteTestDbContextScope();
            var process = await SeedProcessAsync(scope.DbContext, 1, processStatus: true);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(process.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<StockProcessEntity>().AsNoTracking().AnyAsync(t => t.id == process.id)).ShouldBeTrue();
        }

        [Fact]
        public async Task DeleteAsync_BelongsToDifferentTenant_DoesNotDelete()
        {
            // Regression test: this method took no CurrentUser/tenant check at all before the fix.
            using var scope = new SqliteTestDbContextScope();
            var process = await SeedProcessAsync(scope.DbContext, 2, processStatus: false);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(process.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<StockProcessEntity>().AsNoTracking().AnyAsync(t => t.id == process.id)).ShouldBeTrue();
        }

        // ConfirmProcess

        [Fact]
        public async Task ConfirmProcess_UnconfirmedRecord_MarksProcessed()
        {
            using var scope = new SqliteTestDbContextScope();
            var process = await SeedProcessAsync(scope.DbContext, 1, processStatus: false);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ConfirmProcess(process.id, new CurrentUser { tenant_id = 1, user_name = "alice" });

            flag.ShouldBeTrue();
            var saved = await scope.DbContext.GetDbSet<StockProcessEntity>().FindAsync(process.id);
            saved!.process_status.ShouldBeTrue();
            saved.processor.ShouldBe("alice");
        }

        [Fact]
        public async Task ConfirmProcess_AlreadyProcessed_ReturnsStatusChanged()
        {
            using var scope = new SqliteTestDbContextScope();
            var process = await SeedProcessAsync(scope.DbContext, 1, processStatus: true);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ConfirmProcess(process.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task ConfirmProcess_BelongsToDifferentTenant_ReturnsNotExists()
        {
            // Regression test: this method already took CurrentUser but never used it to scope the fetch.
            using var scope = new SqliteTestDbContextScope();
            var process = await SeedProcessAsync(scope.DbContext, 2, processStatus: false);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ConfirmProcess(process.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<StockProcessEntity>().FindAsync(process.id))!.process_status.ShouldBeFalse();
        }

        // ConfirmAdjustment

        [Fact]
        public async Task ConfirmAdjustment_SourceDetail_DecrementsStockAndCreatesAdjustRecord()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var stock = await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 10);
            var process = await SeedProcessAsync(scope.DbContext, 1);
            var detail = await SeedDetailAsync(scope.DbContext, 1, process.id, sku.id, location.id, qty: 5, isSource: true);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ConfirmAdjustment(process.id, new CurrentUser { tenant_id = 1, user_name = "alice" });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<StockEntity>().FindAsync(stock.id))!.qty.ShouldBe(5);
            (await scope.DbContext.GetDbSet<StockProcessDetailEntity>().FindAsync(detail.id))!.is_update_stock.ShouldBeTrue();
            var adjust = await scope.DbContext.GetDbSet<StockAdjustEntity>().AsNoTracking().SingleAsync();
            adjust.source_table_id.ShouldBe(detail.id);
            adjust.qty.ShouldBe(-5);
            adjust.job_type.ShouldBe((byte)2);
        }

        [Fact]
        public async Task ConfirmAdjustment_TargetDetail_CreatesNewStockRow()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var process = await SeedProcessAsync(scope.DbContext, 1);
            await SeedDetailAsync(scope.DbContext, 1, process.id, sku.id, location.id, qty: 7, isSource: false);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ConfirmAdjustment(process.id, new CurrentUser { tenant_id = 1, user_name = "alice" });

            flag.ShouldBeTrue();
            var stock = await scope.DbContext.GetDbSet<StockEntity>().AsNoTracking().SingleAsync();
            stock.qty.ShouldBe(7);
            stock.tenant_id.ShouldBe(1);
        }

        [Fact]
        public async Task ConfirmAdjustment_TargetDetailExistingStock_IncrementsQty()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var stock = await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 3);
            var process = await SeedProcessAsync(scope.DbContext, 1);
            await SeedDetailAsync(scope.DbContext, 1, process.id, sku.id, location.id, qty: 4, isSource: false);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ConfirmAdjustment(process.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var rows = await scope.DbContext.GetDbSet<StockEntity>().AsNoTracking().ToListAsync();
            rows.ShouldHaveSingleItem().qty.ShouldBe(7);
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
            // Regression test: this method already took CurrentUser but never used it to scope the entity fetch,
            // and the StockEntity mutation lookup had no tenant filter at all.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var location = await SeedLocationAsync(scope.DbContext, 2);
            var stock = await SeedStockAsync(scope.DbContext, 2, sku.id, location.id, qty: 10);
            var process = await SeedProcessAsync(scope.DbContext, 2);
            await SeedDetailAsync(scope.DbContext, 2, process.id, sku.id, location.id, qty: 5, isSource: true);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ConfirmAdjustment(process.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<StockEntity>().FindAsync(stock.id))!.qty.ShouldBe(10);
        }

        [Fact]
        public async Task ConfirmAdjustment_AlreadyProcessedAndAdjusted_ReturnsStatusChanged()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var process = await SeedProcessAsync(scope.DbContext, 1, processStatus: true);
            var detail = await SeedDetailAsync(scope.DbContext, 1, process.id, sku.id, location.id, qty: 5, isSource: true, isUpdateStock: true);
            scope.DbContext.GetDbSet<StockAdjustEntity>().Add(new StockAdjustEntity
            {
                sku_id = sku.id,
                source_table_id = detail.id,
                job_type = 2,
                goods_location_id = location.id,
                qty = -5,
                tenant_id = 1,
            });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ConfirmAdjustment(process.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }
    }
}
