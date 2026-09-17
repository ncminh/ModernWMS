using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.Asn
{
    public class AsnmasterServiceTests
    {
        private static AsnMasterService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
            new(dbContext, new FakeStringLocalizer<MultiLanguage>(), TestFunctionHelperFactory.Create(dbContext));

        private static async Task<(SpuEntity spu, SkuEntity sku)> SeedSpuSkuAsync(ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId)
        {
            var spu = new SpuEntity { spu_code = "SPU1", tenant_id = tenantId };
            dbContext.GetDbSet<SpuEntity>().Add(spu);
            await dbContext.SaveChangesAsync();
            var sku = new SkuEntity { spu_id = spu.id, sku_code = "SKU1" };
            dbContext.GetDbSet<SkuEntity>().Add(sku);
            await dbContext.SaveChangesAsync();
            return (spu, sku);
        }

        private static async Task<AsnMasterEntity> SeedAsnmasterWithDetailAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, byte asnStatus = 0)
        {
            var (spu, sku) = await SeedSpuSkuAsync(dbContext, tenantId);
            var asnmaster = new AsnMasterEntity { asn_no = "ASNM1", tenant_id = tenantId, asn_status = asnStatus };
            dbContext.GetDbSet<AsnMasterEntity>().Add(asnmaster);
            await dbContext.SaveChangesAsync();
            var asn = new AsnEntity
            {
                asnmaster_id = asnmaster.id,
                asn_no = asnmaster.asn_no,
                spu_id = spu.id,
                sku_id = sku.id,
                tenant_id = tenantId,
            };
            dbContext.GetDbSet<AsnEntity>().Add(asn);
            await dbContext.SaveChangesAsync();
            return asnmaster;
        }

        [Fact]
        public async Task PageAsnmasterAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            await SeedAsnmasterWithDetailAsync(scope.DbContext, 1);
            await SeedAsnmasterWithDetailAsync(scope.DbContext, 2);

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.PageAsnmasterAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
        }

        [Fact]
        public async Task PageAsnmasterAsync_FiltersByAsnStatus()
        {
            using var scope = new SqliteTestDbContextScope();
            await SeedAsnmasterWithDetailAsync(scope.DbContext, 1, asnStatus: 1);
            await SeedAsnmasterWithDetailAsync(scope.DbContext, 1, asnStatus: 2);

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.PageAsnmasterAsync(new PageSearch { sqlTitle = "asn_status:1" }, new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
            data.ShouldHaveSingleItem().asn_status.ShouldBe((byte)1);
        }

        [Fact]
        public async Task GetAsnmasterAsync_ReturnsDetailList()
        {
            using var scope = new SqliteTestDbContextScope();
            var asnmaster = await SeedAsnmasterWithDetailAsync(scope.DbContext, 1);

            var service = CreateService(scope.DbContext);
            var result = await service.GetAsnmasterAsync(asnmaster.id, new CurrentUser { tenant_id = 1 });

            result.id.ShouldBe(asnmaster.id);
            result.detailList.ShouldHaveSingleItem().spu_code.ShouldBe("SPU1");
        }

        [Fact]
        public async Task GetAsnmasterAsync_BelongsToDifferentTenant_ReturnsEmptyViewModel()
        {
            using var scope = new SqliteTestDbContextScope();
            var asnmaster = await SeedAsnmasterWithDetailAsync(scope.DbContext, 1);

            var service = CreateService(scope.DbContext);
            var result = await service.GetAsnmasterAsync(asnmaster.id, new CurrentUser { tenant_id = 2 });

            result.id.ShouldBe(0);
        }

        [Fact]
        public async Task AddAsnmasterAsync_NewRecord_StampsAuditColumnsAndCreatesDetailRows()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);
            var currentUser = new CurrentUser { tenant_id = 1, user_name = "alice" };
            var viewModel = new AsnMasterBothViewModel { asn_batch = "Batch1" };
            viewModel.detailList.Add(new AsnMasterDetailViewModel { spu_id = 1, sku_id = 1, asn_qty = 10 });

            var (id, _) = await service.AddAsnmasterAsync(viewModel, currentUser);

            id.ShouldBeGreaterThan(0);
            var saved = await scope.DbContext.GetDbSet<AsnMasterEntity>().FindAsync(id);
            saved!.creator.ShouldBe("alice");
            saved.tenant_id.ShouldBe(1);
            saved.asn_no.ShouldNotBeNullOrEmpty();
            (await scope.DbContext.GetDbSet<AsnEntity>().AsNoTracking().CountAsync(t => t.asnmaster_id == id)).ShouldBe(1);
        }

        [Fact]
        public async Task UpdateAsnmasterAsync_UnknownId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.UpdateAsnmasterAsync(new AsnMasterBothViewModel { id = 999 }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateAsnmasterAsync_AddsUpdatesAndRemovesDetailRows()
        {
            using var scope = new SqliteTestDbContextScope();
            var asnmaster = await SeedAsnmasterWithDetailAsync(scope.DbContext, 1);
            var existingAsn = (await scope.DbContext.GetDbSet<AsnEntity>().AsNoTracking().FirstAsync(t => t.asnmaster_id == asnmaster.id));
            var toDelete = new AsnEntity { asnmaster_id = asnmaster.id, asn_no = asnmaster.asn_no, spu_id = existingAsn.spu_id, sku_id = existingAsn.sku_id, tenant_id = 1 };
            scope.DbContext.GetDbSet<AsnEntity>().Add(toDelete);
            await scope.DbContext.SaveChangesAsync();
            scope.DbContext.ChangeTracker.Clear();

            var service = CreateService(scope.DbContext);
            var viewModel = new AsnMasterBothViewModel { id = asnmaster.id, asn_batch = "Updated" };
            viewModel.detailList.Add(new AsnMasterDetailViewModel { id = existingAsn.id, spu_id = existingAsn.spu_id, sku_id = existingAsn.sku_id, asn_qty = 55 });
            viewModel.detailList.Add(new AsnMasterDetailViewModel { id = -toDelete.id });
            viewModel.detailList.Add(new AsnMasterDetailViewModel { id = 0, spu_id = existingAsn.spu_id, sku_id = existingAsn.sku_id, asn_qty = 7 });

            var (flag, _) = await service.UpdateAsnmasterAsync(viewModel, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var remaining = await scope.DbContext.GetDbSet<AsnEntity>().AsNoTracking().Where(t => t.asnmaster_id == asnmaster.id).ToListAsync();
            remaining.Count.ShouldBe(2);
            remaining.ShouldContain(t => t.id == existingAsn.id && t.asn_qty == 55);
            remaining.ShouldContain(t => t.asn_qty == 7);
            remaining.ShouldNotContain(t => t.id == toDelete.id);
        }

        [Fact]
        public async Task UpdateAsnmasterAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var asnmaster = await SeedAsnmasterWithDetailAsync(scope.DbContext, 1);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsnmasterAsync(new AsnMasterBothViewModel { id = asnmaster.id, asn_batch = "Hijacked" }, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsnmasterAsync_ExistingRecord_DeletesMasterAndChildren()
        {
            using var scope = new SqliteTestDbContextScope();
            var asnmaster = await SeedAsnmasterWithDetailAsync(scope.DbContext, 1);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsnmasterAsync(asnmaster.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<AsnMasterEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == asnmaster.id)).ShouldBeNull();
            (await scope.DbContext.GetDbSet<AsnEntity>().AsNoTracking().CountAsync(t => t.asnmaster_id == asnmaster.id)).ShouldBe(0);
        }

        [Fact]
        public async Task DeleteAsnmasterAsync_BelongsToDifferentTenant_DoesNotDelete()
        {
            using var scope = new SqliteTestDbContextScope();
            var asnmaster = await SeedAsnmasterWithDetailAsync(scope.DbContext, 1);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsnmasterAsync(asnmaster.id, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<AsnMasterEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == asnmaster.id)).ShouldNotBeNull();
            (await scope.DbContext.GetDbSet<AsnEntity>().AsNoTracking().CountAsync(t => t.asnmaster_id == asnmaster.id)).ShouldBe(1);
        }
    }
}
