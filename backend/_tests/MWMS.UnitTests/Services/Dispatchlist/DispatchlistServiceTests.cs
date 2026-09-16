using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.Dispatchlist
{
    public class DispatchlistServiceTests
    {
        private static DispatchlistService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
            new(dbContext, new FakeStringLocalizer<MultiLanguage>(), TestFunctionHelperFactory.Create(dbContext));

        private static async Task<(SpuEntity spu, SkuEntity sku)> SeedSpuSkuAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, byte volumeUnit = 1, byte weightUnit = 2)
        {
            var spu = new SpuEntity { spu_code = "SPU1", tenant_id = tenantId, volume_unit = volumeUnit, weight_unit = weightUnit };
            dbContext.GetDbSet<SpuEntity>().Add(spu);
            await dbContext.SaveChangesAsync();
            var sku = new SkuEntity { spu_id = spu.id, sku_code = "SKU1", volume = 2, weight = 3 };
            dbContext.GetDbSet<SkuEntity>().Add(sku);
            await dbContext.SaveChangesAsync();
            return (spu, sku);
        }

        private static async Task<DispatchlistEntity> SeedDispatchlistAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, int skuId,
            string dispatchNo = "DISP1", byte dispatchStatus = 0, int qty = 10, int pickedQty = 0,
            decimal volume = 0, decimal weight = 0)
        {
            var entity = new DispatchlistEntity
            {
                dispatch_no = dispatchNo,
                dispatch_status = dispatchStatus,
                sku_id = skuId,
                qty = qty,
                picked_qty = pickedQty,
                volume = volume,
                weight = weight,
                tenant_id = tenantId,
            };
            dbContext.GetDbSet<DispatchlistEntity>().Add(entity);
            await dbContext.SaveChangesAsync();
            return entity;
        }

        [Fact]
        public async Task PageAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            await SeedDispatchlistAsync(scope.DbContext, 1, sku.id);
            await SeedDispatchlistAsync(scope.DbContext, 2, sku.id);

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.PageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
        }

        [Fact]
        public async Task PageAsync_FiltersByExactDispatchStatus()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchStatus: 1);
            await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchStatus: 2);

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.PageAsync(new PageSearch { sqlTitle = "dispatch_status:1" }, new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
            data.ShouldHaveSingleItem().dispatch_status.ShouldBe((byte)1);
        }

        [Fact]
        public async Task PageAsync_PackageFilter_MatchesFullyPickedStatusThreeRow()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchStatus: 3, qty: 5, pickedQty: 5);
            await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchStatus: 1, qty: 5, pickedQty: 0);

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.PageAsync(new PageSearch { sqlTitle = "package" }, new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
            data.ShouldHaveSingleItem().dispatch_status.ShouldBe((byte)3);
        }

        [Fact]
        public async Task AdvancedDispatchlistPageAsync_GroupsByDispatchNoAndSumsQtyVolumeWeight()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1, volumeUnit: 1, weightUnit: 2);
            await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchNo: "D1", qty: 4, volume: 10, weight: 20);
            await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchNo: "D1", qty: 6, volume: 15, weight: 25);
            await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchNo: "D2", qty: 1, volume: 1, weight: 1);

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.AdvancedDispatchlistPageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(2);
            var d1 = data.Single(t => t.dispatch_no == "D1");
            d1.qty.ShouldBe(10);
            d1.volume.ShouldBe(25);
            d1.weight.ShouldBe(45);
        }

        [Fact]
        public async Task AdvancedDispatchlistPageAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchNo: "D1");
            await SeedDispatchlistAsync(scope.DbContext, 2, sku.id, dispatchNo: "D2");

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.AdvancedDispatchlistPageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
        }

        [Fact]
        public async Task AddAsync_NewRecord_StampsAuditColumnsAndComputesVolumeWeight()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var service = CreateService(scope.DbContext);
            var currentUser = new CurrentUser { tenant_id = 1, user_name = "alice" };

            var (flag, _) = await service.AddAsync(
                new List<DispatchlistAddViewModel> { new() { sku_id = sku.id, qty = 4 } }, currentUser);

            flag.ShouldBeTrue();
            var saved = await scope.DbContext.GetDbSet<DispatchlistEntity>().AsNoTracking().FirstAsync();
            saved.creator.ShouldBe("alice");
            saved.tenant_id.ShouldBe(1);
            saved.dispatch_no.ShouldNotBeNullOrEmpty();
            saved.volume.ShouldBe(sku.volume * 4);
            saved.weight.ShouldBe(sku.weight * 4);
        }

        [Fact]
        public async Task GetByDispatchlistNo_ReturnsJoinedSkuAndSpuData()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchNo: "D1");

            var service = CreateService(scope.DbContext);
            var data = await service.GetByDispatchlistNo("D1", new CurrentUser { tenant_id = 1 });

            data.ShouldHaveSingleItem().sku_code.ShouldBe("SKU1");
        }

        [Fact]
        public async Task GetByDispatchlistNo_BelongsToDifferentTenant_ReturnsEmpty()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchNo: "D1");

            var service = CreateService(scope.DbContext);
            var data = await service.GetByDispatchlistNo("D1", new CurrentUser { tenant_id = 2 });

            data.ShouldBeEmpty();
        }

        [Fact]
        public async Task UpdateAsycn_ExistingStatusNotEditable_ReturnsError()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var entity = await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchNo: "D1", dispatchStatus: 2);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsycn(
                new List<DispatchlistViewModel> { new() { id = entity.id, dispatch_no = "D1", sku_id = sku.id, qty = 5 } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateAsycn_PositiveId_UpdatesExistingRow()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var entity = await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchNo: "D1", dispatchStatus: 0, qty: 5);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsycn(
                new List<DispatchlistViewModel> { new() { id = entity.id, dispatch_no = "D1", dispatch_status = 0, sku_id = sku.id, qty = 9 } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<DispatchlistEntity>().FindAsync(entity.id))!.qty.ShouldBe(9);
        }

        [Fact]
        public async Task UpdateAsycn_ZeroId_AddsNewRow()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var entity = await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchNo: "D1", dispatchStatus: 0);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsycn(
                new List<DispatchlistViewModel>
                {
                    new() { id = entity.id, dispatch_no = "D1", dispatch_status = 0, sku_id = sku.id, qty = entity.qty },
                    new() { id = 0, dispatch_no = "D1", dispatch_status = 0, sku_id = 999, qty = 3 },
                },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var rows = await scope.DbContext.GetDbSet<DispatchlistEntity>().AsNoTracking().Where(t => t.dispatch_no == "D1").ToListAsync();
            rows.Count.ShouldBe(2);
        }

        [Fact]
        public async Task UpdateAsycn_NegativeId_RemovesRow()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var entity = await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchNo: "D1", dispatchStatus: 0);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsycn(
                new List<DispatchlistViewModel> { new() { id = -entity.id, dispatch_no = "D1", sku_id = sku.id, qty = entity.qty } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<DispatchlistEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == entity.id)).ShouldBeNull();
        }

        [Fact]
        public async Task UpdateAsycn_DuplicateSkuAcrossRows_ReturnsError()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var entity = await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchNo: "D1", dispatchStatus: 0);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsycn(
                new List<DispatchlistViewModel>
                {
                    new() { id = entity.id, dispatch_no = "D1", dispatch_status = 0, sku_id = sku.id, qty = entity.qty },
                    new() { id = 0, dispatch_no = "D1", dispatch_status = 0, sku_id = sku.id, qty = 1 },
                },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_StatusZeroOrOne_RemovesTheRows()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchNo: "D1", dispatchStatus: 1);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync("D1", new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<DispatchlistEntity>().AsNoTracking().AnyAsync(t => t.dispatch_no == "D1")).ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_StatusAboveOne_IsBlocked()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchNo: "D1", dispatchStatus: 2);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync("D1", new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<DispatchlistEntity>().AsNoTracking().AnyAsync(t => t.dispatch_no == "D1")).ShouldBeTrue();
        }

        [Fact]
        public async Task DeleteAsync_BelongsToDifferentTenant_DoesNotDelete()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchNo: "D1", dispatchStatus: 0);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync("D1", new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<DispatchlistEntity>().AsNoTracking().AnyAsync(t => t.dispatch_no == "D1")).ShouldBeTrue();
        }

        [Fact]
        public async Task Import_NewRecords_CreatesRowsGroupedByImportGroup()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var customer = new CustomerEntity { customer_name = "Cust1", tenant_id = 1 };
            scope.DbContext.GetDbSet<CustomerEntity>().Add(customer);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var currentUser = new CurrentUser { tenant_id = 1, user_name = "alice" };
            var (flag, _) = await service.Import(
                new List<DispatchlistImportViewModel>
                {
                    new() { import_group = 1, customer_name = "Cust1", sku_code = "SKU1", qty = 2 },
                },
                currentUser);

            flag.ShouldBeTrue();
            var saved = await scope.DbContext.GetDbSet<DispatchlistEntity>().AsNoTracking().FirstAsync();
            saved.customer_id.ShouldBe(customer.id);
            saved.sku_id.ShouldBe(sku.id);
            saved.tenant_id.ShouldBe(1);
            saved.creator.ShouldBe("alice");
        }

        [Fact]
        public async Task Import_UnknownCustomer_ReturnsError()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.Import(
                new List<DispatchlistImportViewModel>
                {
                    new() { import_group = 1, customer_name = "Unknown", sku_code = "SKU1", qty = 2 },
                },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task Import_UnknownSku_ReturnsError()
        {
            using var scope = new SqliteTestDbContextScope();
            var customer = new CustomerEntity { customer_name = "Cust1", tenant_id = 1 };
            scope.DbContext.GetDbSet<CustomerEntity>().Add(customer);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.Import(
                new List<DispatchlistImportViewModel>
                {
                    new() { import_group = 1, customer_name = "Cust1", sku_code = "UNKNOWN", qty = 2 },
                },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }
    }
}
