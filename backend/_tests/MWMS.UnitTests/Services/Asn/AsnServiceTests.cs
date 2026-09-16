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
    public class AsnServiceTests
    {
        private static AsnService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
            new(dbContext, new FakeStringLocalizer<MultiLanguage>(), TestFunctionHelperFactory.Create(dbContext));

        private static async Task<(SpuEntity spu, SkuEntity sku, AsnmasterEntity asnmaster)> SeedAsnPrerequisitesAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId)
        {
            var spu = new SpuEntity { spu_code = "SPU1", tenant_id = tenantId };
            dbContext.GetDbSet<SpuEntity>().Add(spu);
            await dbContext.SaveChangesAsync();
            var sku = new SkuEntity { spu_id = spu.id, sku_code = "SKU1" };
            dbContext.GetDbSet<SkuEntity>().Add(sku);
            await dbContext.SaveChangesAsync();
            var asnmaster = new AsnmasterEntity { asn_no = "ASNM1", tenant_id = tenantId };
            dbContext.GetDbSet<AsnmasterEntity>().Add(asnmaster);
            await dbContext.SaveChangesAsync();
            return (spu, sku, asnmaster);
        }

        private static async Task<AsnEntity> SeedAsnAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, byte asnStatus = 0)
        {
            var (spu, sku, asnmaster) = await SeedAsnPrerequisitesAsync(dbContext, tenantId);
            var asn = new AsnEntity
            {
                asnmaster_id = asnmaster.id,
                asn_no = "ASN1",
                spu_id = spu.id,
                sku_id = sku.id,
                asn_status = asnStatus,
                tenant_id = tenantId,
            };
            dbContext.GetDbSet<AsnEntity>().Add(asn);
            await dbContext.SaveChangesAsync();
            return asn;
        }

        [Fact]
        public async Task PageAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            await SeedAsnAsync(scope.DbContext, 1);
            await SeedAsnAsync(scope.DbContext, 2);

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.PageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
        }

        [Fact]
        public async Task PageAsync_FiltersByExactAsnStatus()
        {
            using var scope = new SqliteTestDbContextScope();
            await SeedAsnAsync(scope.DbContext, 1, asnStatus: 1);
            await SeedAsnAsync(scope.DbContext, 1, asnStatus: 2);

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.PageAsync(new PageSearch { sqlTitle = "asn_status:1" }, new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
            data.ShouldHaveSingleItem().asn_status.ShouldBe((byte)1);
        }

        [Fact]
        public async Task PageAsync_AllTodoFilter_ExcludesCompletedRows()
        {
            using var scope = new SqliteTestDbContextScope();
            await SeedAsnAsync(scope.DbContext, 1, asnStatus: 3);
            await SeedAsnAsync(scope.DbContext, 1, asnStatus: 4);

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.PageAsync(new PageSearch { sqlTitle = "asn_status:alltodo" }, new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
            data.ShouldHaveSingleItem().asn_status.ShouldBe((byte)3);
        }

        [Fact]
        public async Task GetAsync_ReturnsJoinedSpuAndSkuData()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1);

            var service = CreateService(scope.DbContext);
            var result = await service.GetAsync(asn.id, new CurrentUser { tenant_id = 1 });

            result.id.ShouldBe(asn.id);
            result.spu_code.ShouldBe("SPU1");
            result.sku_code.ShouldBe("SKU1");
        }

        [Fact]
        public async Task GetAsync_BelongsToDifferentTenant_ReturnsEmptyViewModel()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1);

            var service = CreateService(scope.DbContext);
            var result = await service.GetAsync(asn.id, new CurrentUser { tenant_id = 2 });

            result.id.ShouldBe(0);
        }

        [Fact]
        public async Task AddAsync_NewRecord_StampsAuditColumnsAndGeneratesAsnNo()
        {
            using var scope = new SqliteTestDbContextScope();
            var (spu, sku, asnmaster) = await SeedAsnPrerequisitesAsync(scope.DbContext, 1);
            var service = CreateService(scope.DbContext);
            var currentUser = new CurrentUser { tenant_id = 1, user_name = "alice" };

            var (id, _) = await service.AddAsync(new AsnViewModel { asnmaster_id = asnmaster.id, spu_id = spu.id, sku_id = sku.id }, currentUser);

            id.ShouldBeGreaterThan(0);
            var saved = await scope.DbContext.GetDbSet<AsnEntity>().FindAsync(id);
            saved!.creator.ShouldBe("alice");
            saved.tenant_id.ShouldBe(1);
            saved.asn_no.ShouldNotBeNullOrEmpty();
        }

        [Fact]
        public async Task UpdateAsync_UnknownId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.UpdateAsync(new AsnViewModel { id = 999 }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateAsync_ExistingRecord_UpdatesFields()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(new AsnViewModel { id = asn.id, asn_qty = 42 }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<AsnEntity>().FindAsync(asn.id))!.asn_qty.ShouldBe(42);
        }

        [Fact]
        public async Task UpdateAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(new AsnViewModel { id = asn.id, asn_qty = 99 }, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
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
        public async Task DeleteAsync_StatusZero_RemovesTheRow()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 0);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(asn.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<AsnEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == asn.id)).ShouldBeNull();
        }

        [Fact]
        public async Task DeleteAsync_StatusEight_IsBlocked()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 8);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(asn.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<AsnEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == asn.id)).ShouldNotBeNull();
        }

        [Fact]
        public async Task DeleteAsync_MidStatus_DecrementsStatusByOneInsteadOfDeleting()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 3);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(asn.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<AsnEntity>().FindAsync(asn.id))!.asn_status.ShouldBe((byte)2);
        }

        [Fact]
        public async Task DeleteAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 0);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(asn.id, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<AsnEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == asn.id)).ShouldNotBeNull();
        }

        [Fact]
        public async Task BulkModifyGoodsownerAsync_UpdatesMatchingRowsInCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn1 = await SeedAsnAsync(scope.DbContext, 1);
            var asn2 = await SeedAsnAsync(scope.DbContext, 1);

            var service = CreateService(scope.DbContext);
            var viewModel = new AsnBulkModifyGoodsOwnerViewModel
            {
                goods_owner_id = 7,
                goods_owner_name = "Owner7",
                idList = new List<int> { asn1.id, asn2.id },
            };
            var (flag, _) = await service.BulkModifyGoodsownerAsync(viewModel, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<AsnEntity>().FindAsync(asn1.id))!.goods_owner_id.ShouldBe(7);
            (await scope.DbContext.GetDbSet<AsnEntity>().FindAsync(asn2.id))!.goods_owner_id.ShouldBe(7);
        }

        [Fact]
        public async Task BulkModifyGoodsownerAsync_RowBelongsToDifferentTenant_IsNotUpdated()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1);

            var service = CreateService(scope.DbContext);
            var viewModel = new AsnBulkModifyGoodsOwnerViewModel
            {
                goods_owner_id = 7,
                goods_owner_name = "Owner7",
                idList = new List<int> { asn.id },
            };
            var (flag, _) = await service.BulkModifyGoodsownerAsync(viewModel, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<AsnEntity>().FindAsync(asn.id))!.goods_owner_id.ShouldBe(0);
        }
    }
}
