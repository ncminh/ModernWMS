using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.Asn
{
    public class AsnSortingServiceTests
    {
        private static AsnSortingService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
            new(dbContext, new FakeStringLocalizer<MultiLanguage>(), TestFunctionHelperFactory.Create(dbContext));

        private static async Task<AsnEntity> SeedAsnAsync(ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, byte asnStatus = 2, int sortedQty = 0, int asnQty = 0, int actualQty = 0)
        {
            var asnmaster = new AsnMasterEntity { asn_no = "ASNM1", tenant_id = tenantId };
            dbContext.GetDbSet<AsnMasterEntity>().Add(asnmaster);
            await dbContext.SaveChangesAsync();
            var asn = new AsnEntity
            {
                asnmaster_id = asnmaster.id,
                asn_no = "ASN1",
                asn_status = asnStatus,
                sorted_qty = sortedQty,
                asn_qty = asnQty,
                actual_qty = actualQty,
                tenant_id = tenantId,
            };
            dbContext.GetDbSet<AsnEntity>().Add(asn);
            await dbContext.SaveChangesAsync();
            return asn;
        }

        [Fact]
        public async Task SortingAsync_NoMatchingIds_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.SortingAsync(new List<AsnSortInputViewModel> { new() { asn_id = 999 } }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task SortingAsync_NotPreSortStatus_IsBlocked()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 1);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.SortingAsync(new List<AsnSortInputViewModel> { new() { asn_id = asn.id, sorted_qty = 1 } }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task SortingAsync_SingleUnit_CreatesOneRowAndIncrementsSortedQty()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 2);

            var service = CreateService(scope.DbContext);
            var viewModel = new AsnSortInputViewModel { asn_id = asn.id, sorted_qty = 5, is_auto_num = false };
            var (flag, _) = await service.SortingAsync(new List<AsnSortInputViewModel> { viewModel }, new CurrentUser { tenant_id = 1, user_name = "alice" });

            flag.ShouldBeTrue();
            var sorts = await scope.DbContext.GetDbSet<AsnSortEntity>().AsNoTracking().Where(t => t.asn_id == asn.id).ToListAsync();
            var sort = sorts.ShouldHaveSingleItem();
            sort.sorted_qty.ShouldBe(5);
            sort.series_number.ShouldNotBeNullOrEmpty();
            (await scope.DbContext.GetDbSet<AsnEntity>().FindAsync(asn.id))!.sorted_qty.ShouldBe(5);
        }

        [Fact]
        public async Task SortingAsync_MultiUnitAutoNum_CreatesOneRowPerUnit()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 2);

            var service = CreateService(scope.DbContext);
            var viewModel = new AsnSortInputViewModel { asn_id = asn.id, sorted_qty = 3, is_auto_num = true };
            var (flag, _) = await service.SortingAsync(new List<AsnSortInputViewModel> { viewModel }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var sorts = await scope.DbContext.GetDbSet<AsnSortEntity>().AsNoTracking().Where(t => t.asn_id == asn.id).ToListAsync();
            sorts.Count.ShouldBe(3);
            sorts.ShouldAllBe(t => t.sorted_qty == 1);
            sorts.Select(t => t.series_number).Distinct().Count().ShouldBe(3);
        }

        [Fact]
        public async Task SortingAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 2);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.SortingAsync(new List<AsnSortInputViewModel> { new() { asn_id = asn.id, sorted_qty = 1 } }, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<AsnEntity>().AsNoTracking().FirstAsync(t => t.id == asn.id)).sorted_qty.ShouldBe(0);
        }

        [Fact]
        public async Task GetAsnsortsAsync_ReturnsMatchingRows()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1);
            scope.DbContext.GetDbSet<AsnSortEntity>().Add(new AsnSortEntity { asn_id = asn.id, sorted_qty = 4, tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetAsnsortsAsync(asn.id, new CurrentUser { tenant_id = 1 });

            result.ShouldHaveSingleItem().sorted_qty.ShouldBe(4);
        }

        [Fact]
        public async Task GetAsnsortsAsync_BelongsToDifferentTenant_ReturnsEmptyList()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1);
            scope.DbContext.GetDbSet<AsnSortEntity>().Add(new AsnSortEntity { asn_id = asn.id, sorted_qty = 4, tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetAsnsortsAsync(asn.id, new CurrentUser { tenant_id = 2 });

            result.ShouldBeEmpty();
        }

        [Fact]
        public async Task ModifyAsnsortsAsync_NegativeId_DeletesRowAndRecalculatesAsnSortedQty()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, sortedQty: 4);
            var toDelete = new AsnSortEntity { asn_id = asn.id, sorted_qty = 4, tenant_id = 1 };
            scope.DbContext.GetDbSet<AsnSortEntity>().Add(toDelete);
            await scope.DbContext.SaveChangesAsync();
            scope.DbContext.ChangeTracker.Clear();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ModifyAsnsortsAsync(new List<AsnSortEntity> { new() { id = -toDelete.id, asn_id = asn.id } }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<AsnSortEntity>().AsNoTracking().CountAsync(t => t.asn_id == asn.id)).ShouldBe(0);
            (await scope.DbContext.GetDbSet<AsnEntity>().FindAsync(asn.id))!.sorted_qty.ShouldBe(0);
        }

        [Fact]
        public async Task ModifyAsnsortsAsync_PositiveId_UpdatesRowAndRecalculatesAsnSortedQty()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, sortedQty: 4);
            var existing = new AsnSortEntity { asn_id = asn.id, sorted_qty = 4, tenant_id = 1 };
            scope.DbContext.GetDbSet<AsnSortEntity>().Add(existing);
            await scope.DbContext.SaveChangesAsync();
            scope.DbContext.ChangeTracker.Clear();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ModifyAsnsortsAsync(new List<AsnSortEntity> { new() { id = existing.id, asn_id = asn.id, sorted_qty = 9 } }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<AsnSortEntity>().FindAsync(existing.id))!.sorted_qty.ShouldBe(9);
            (await scope.DbContext.GetDbSet<AsnEntity>().FindAsync(asn.id))!.sorted_qty.ShouldBe(9);
        }

        [Fact]
        public async Task ModifyAsnsortsAsync_RowBelongsToDifferentTenant_IsNotDeletedOrUpdated()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, sortedQty: 4);
            var existing = new AsnSortEntity { asn_id = asn.id, sorted_qty = 4, tenant_id = 1 };
            scope.DbContext.GetDbSet<AsnSortEntity>().Add(existing);
            await scope.DbContext.SaveChangesAsync();
            scope.DbContext.ChangeTracker.Clear();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ModifyAsnsortsAsync(
                new List<AsnSortEntity> { new() { id = existing.id, asn_id = asn.id, sorted_qty = 99 } },
                new CurrentUser { tenant_id = 2 });

            (await scope.DbContext.GetDbSet<AsnSortEntity>().AsNoTracking().FirstAsync(t => t.id == existing.id)).sorted_qty.ShouldBe(4);
        }

        [Fact]
        public async Task SortedAsync_NoSortedQty_IsBlocked()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, sortedQty: 0);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.SortedAsync(new List<int> { asn.id }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task SortedAsync_SortedQtyExceedsAsnQty_SetsMoreQty()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, sortedQty: 10, asnQty: 8);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.SortedAsync(new List<int> { asn.id }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var updated = await scope.DbContext.GetDbSet<AsnEntity>().FindAsync(asn.id);
            updated!.asn_status.ShouldBe((byte)3);
            updated.more_qty.ShouldBe(2);
            updated.shortage_qty.ShouldBe(0);
        }

        [Fact]
        public async Task SortedAsync_SortedQtyBelowAsnQty_SetsShortageQty()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, sortedQty: 5, asnQty: 8);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.SortedAsync(new List<int> { asn.id }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var updated = await scope.DbContext.GetDbSet<AsnEntity>().FindAsync(asn.id);
            updated!.shortage_qty.ShouldBe(3);
            updated.more_qty.ShouldBe(0);
        }

        [Fact]
        public async Task SortedAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, sortedQty: 5, asnQty: 5);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.SortedAsync(new List<int> { asn.id }, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<AsnEntity>().AsNoTracking().FirstAsync(t => t.id == asn.id)).asn_status.ShouldBe((byte)2);
        }

        [Fact]
        public async Task SortedCancelAsync_AlreadyPutAway_IsBlocked()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 3, sortedQty: 5, actualQty: 1);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.SortedCancelAsync(new List<int> { asn.id }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task SortedCancelAsync_NotSorted_IsBlocked()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 3, sortedQty: 0);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.SortedCancelAsync(new List<int> { asn.id }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task SortedCancelAsync_Success_ResetsFieldsAndDeletesAsnsorts()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 3, sortedQty: 5, asnQty: 5);
            scope.DbContext.GetDbSet<AsnSortEntity>().Add(new AsnSortEntity { asn_id = asn.id, sorted_qty = 5, tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.SortedCancelAsync(new List<int> { asn.id }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var updated = await scope.DbContext.GetDbSet<AsnEntity>().FindAsync(asn.id);
            updated!.asn_status.ShouldBe((byte)2);
            updated.sorted_qty.ShouldBe(0);
            (await scope.DbContext.GetDbSet<AsnSortEntity>().AsNoTracking().CountAsync(t => t.asn_id == asn.id)).ShouldBe(0);
        }

        [Fact]
        public async Task SortedCancelAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 3, sortedQty: 5, asnQty: 5);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.SortedCancelAsync(new List<int> { asn.id }, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<AsnEntity>().AsNoTracking().FirstAsync(t => t.id == asn.id)).asn_status.ShouldBe((byte)3);
        }

        [Fact]
        public async Task GetAsnPrintSeriesNumberAsync_ReturnsMatchingRows()
        {
            using var scope = new SqliteTestDbContextScope();
            var spu = new SpuEntity { spu_code = "SPU1", tenant_id = 1 };
            scope.DbContext.GetDbSet<SpuEntity>().Add(spu);
            await scope.DbContext.SaveChangesAsync();
            var sku = new SkuEntity { spu_id = spu.id, sku_code = "SKU1" };
            scope.DbContext.GetDbSet<SkuEntity>().Add(sku);
            await scope.DbContext.SaveChangesAsync();
            var asnmaster = new AsnMasterEntity { asn_no = "ASNM1", tenant_id = 1 };
            scope.DbContext.GetDbSet<AsnMasterEntity>().Add(asnmaster);
            await scope.DbContext.SaveChangesAsync();
            var asn = new AsnEntity { asnmaster_id = asnmaster.id, asn_no = "ASN1", spu_id = spu.id, sku_id = sku.id, tenant_id = 1 };
            scope.DbContext.GetDbSet<AsnEntity>().Add(asn);
            await scope.DbContext.SaveChangesAsync();
            scope.DbContext.GetDbSet<AsnSortEntity>().Add(new AsnSortEntity { asn_id = asn.id, series_number = "SN1", tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetAsnPrintSeriesNumberAsync(new List<int> { asn.id }, new CurrentUser { tenant_id = 1 });

            result.ShouldHaveSingleItem().series_number.ShouldBe("SN1");
        }

        [Fact]
        public async Task GetAsnPrintSeriesNumberAsync_BelongsToDifferentTenant_ReturnsEmptyList()
        {
            using var scope = new SqliteTestDbContextScope();
            var spu = new SpuEntity { spu_code = "SPU1", tenant_id = 1 };
            scope.DbContext.GetDbSet<SpuEntity>().Add(spu);
            await scope.DbContext.SaveChangesAsync();
            var sku = new SkuEntity { spu_id = spu.id, sku_code = "SKU1" };
            scope.DbContext.GetDbSet<SkuEntity>().Add(sku);
            await scope.DbContext.SaveChangesAsync();
            var asnmaster = new AsnMasterEntity { asn_no = "ASNM1", tenant_id = 1 };
            scope.DbContext.GetDbSet<AsnMasterEntity>().Add(asnmaster);
            await scope.DbContext.SaveChangesAsync();
            var asn = new AsnEntity { asnmaster_id = asnmaster.id, asn_no = "ASN1", spu_id = spu.id, sku_id = sku.id, tenant_id = 1 };
            scope.DbContext.GetDbSet<AsnEntity>().Add(asn);
            await scope.DbContext.SaveChangesAsync();
            scope.DbContext.GetDbSet<AsnSortEntity>().Add(new AsnSortEntity { asn_id = asn.id, series_number = "SN1", tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetAsnPrintSeriesNumberAsync(new List<int> { asn.id }, new CurrentUser { tenant_id = 2 });

            result.ShouldBeEmpty();
        }
    }
}
