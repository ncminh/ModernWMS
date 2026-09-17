using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Utility;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.Asn
{
    public class AsnPutawayServiceTests
    {
        private static AsnPutawayService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
            new(dbContext, new FakeStringLocalizer<MultiLanguage>());

        private static async Task<GoodsLocationEntity> SeedLocationAsync(ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, byte warehouseAreaProperty = 0)
        {
            var location = new GoodsLocationEntity { location_name = "L1", tenant_id = tenantId, warehouse_area_property = warehouseAreaProperty };
            dbContext.GetDbSet<GoodsLocationEntity>().Add(location);
            await dbContext.SaveChangesAsync();
            return location;
        }

        private static async Task<AsnEntity> SeedAsnAsync(ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, byte asnStatus = 3, int sortedQty = 10, int actualQty = 0, int skuId = 1)
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
                actual_qty = actualQty,
                sku_id = skuId,
                tenant_id = tenantId,
            };
            dbContext.GetDbSet<AsnEntity>().Add(asn);
            await dbContext.SaveChangesAsync();
            return asn;
        }

        [Fact]
        public async Task GetPendingPutawayDataAsync_ReturnsRowsWithRemainingSortedQty()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1);
            scope.DbContext.GetDbSet<AsnSortEntity>().Add(new AsnSortEntity { asn_id = asn.id, sorted_qty = 10, putaway_qty = 3, series_number = "SN1", tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetPendingPutawayDataAsync(asn.id, new CurrentUser { tenant_id = 1 });

            var row = result.ShouldHaveSingleItem();
            row.sorted_qty.ShouldBe(7);
            row.series_number.ShouldBe("SN1");
        }

        [Fact]
        public async Task GetPendingPutawayDataAsync_ExcludesFullyPutAwayRows()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1);
            scope.DbContext.GetDbSet<AsnSortEntity>().Add(new AsnSortEntity { asn_id = asn.id, sorted_qty = 10, putaway_qty = 10, series_number = "SN1", tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetPendingPutawayDataAsync(asn.id, new CurrentUser { tenant_id = 1 });

            result.ShouldBeEmpty();
        }

        [Fact]
        public async Task GetPendingPutawayDataAsync_BelongsToDifferentTenant_ReturnsEmptyList()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1);
            scope.DbContext.GetDbSet<AsnSortEntity>().Add(new AsnSortEntity { asn_id = asn.id, sorted_qty = 10, putaway_qty = 3, series_number = "SN1", tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetPendingPutawayDataAsync(asn.id, new CurrentUser { tenant_id = 2 });

            result.ShouldBeEmpty();
        }

        [Fact]
        public async Task PutAwayAsync_MissingGoodsLocationId_IsRejected()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.PutAwayAsync(
                new List<AsnPutAwayInputViewModel> { new() { asn_id = 1, putaway_qty = 5, goods_location_id = 0 } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task PutAwayAsync_UnknownLocation_IsRejected()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.PutAwayAsync(
                new List<AsnPutAwayInputViewModel> { new() { asn_id = 1, putaway_qty = 5, goods_location_id = 999 } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task PutAwayAsync_LocationBelongsToDifferentTenant_IsRejected()
        {
            using var scope = new SqliteTestDbContextScope();
            var location = await SeedLocationAsync(scope.DbContext, 2);
            var asn = await SeedAsnAsync(scope.DbContext, 1);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.PutAwayAsync(
                new List<AsnPutAwayInputViewModel> { new() { asn_id = asn.id, putaway_qty = 5, goods_location_id = location.id } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<StockEntity>().AsNoTracking().CountAsync()).ShouldBe(0);
        }

        [Fact]
        public async Task PutAwayAsync_UnknownAsn_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var location = await SeedLocationAsync(scope.DbContext, 1);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.PutAwayAsync(
                new List<AsnPutAwayInputViewModel> { new() { asn_id = 999, putaway_qty = 5, goods_location_id = location.id } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task PutAwayAsync_NotSortedStatus_IsRejected()
        {
            using var scope = new SqliteTestDbContextScope();
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var asn = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 2);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.PutAwayAsync(
                new List<AsnPutAwayInputViewModel> { new() { asn_id = asn.id, putaway_qty = 5, goods_location_id = location.id } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task PutAwayAsync_ExceedsSortedQty_IsRejected()
        {
            using var scope = new SqliteTestDbContextScope();
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var asn = await SeedAsnAsync(scope.DbContext, 1, sortedQty: 5, actualQty: 0);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.PutAwayAsync(
                new List<AsnPutAwayInputViewModel> { new() { asn_id = asn.id, putaway_qty = 6, goods_location_id = location.id } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task PutAwayAsync_CreatesNewStockRowAndIncrementsActualQty()
        {
            using var scope = new SqliteTestDbContextScope();
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var asn = await SeedAsnAsync(scope.DbContext, 1, sortedQty: 10, actualQty: 0, skuId: 7);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.PutAwayAsync(
                new List<AsnPutAwayInputViewModel> { new() { asn_id = asn.id, putaway_qty = 4, goods_location_id = location.id, goods_owner_id = 3 } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var stock = (await scope.DbContext.GetDbSet<StockEntity>().AsNoTracking().ToListAsync()).ShouldHaveSingleItem();
            stock.sku_id.ShouldBe(7);
            stock.qty.ShouldBe(4);
            stock.goods_location_id.ShouldBe(location.id);
            (await scope.DbContext.GetDbSet<AsnEntity>().FindAsync(asn.id))!.actual_qty.ShouldBe(4);
        }

        [Fact]
        public async Task PutAwayAsync_ExistingMatchingStockRow_IncrementsQtyInsteadOfCreatingNew()
        {
            using var scope = new SqliteTestDbContextScope();
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var asn = await SeedAsnAsync(scope.DbContext, 1, sortedQty: 10, actualQty: 0, skuId: 7);
            var today = DateTime.Now.ToString("yyyy-MM-dd").ObjToDate();
            scope.DbContext.GetDbSet<StockEntity>().Add(new StockEntity
            {
                sku_id = 7,
                goods_location_id = location.id,
                goods_owner_id = 0,
                series_number = string.Empty,
                expiry_date = UtilConvert.MinDate,
                price = 0,
                putaway_date = today,
                qty = 2,
                tenant_id = 1,
            });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.PutAwayAsync(
                new List<AsnPutAwayInputViewModel> { new() { asn_id = asn.id, putaway_qty = 4, goods_location_id = location.id, goods_owner_id = 0 } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var stock = (await scope.DbContext.GetDbSet<StockEntity>().AsNoTracking().ToListAsync()).ShouldHaveSingleItem();
            stock.qty.ShouldBe(6);
        }

        [Fact]
        public async Task PutAwayAsync_ReachesSortedQty_SetsStatusToPutAway()
        {
            using var scope = new SqliteTestDbContextScope();
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var asn = await SeedAsnAsync(scope.DbContext, 1, sortedQty: 5, actualQty: 0);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.PutAwayAsync(
                new List<AsnPutAwayInputViewModel> { new() { asn_id = asn.id, putaway_qty = 5, goods_location_id = location.id } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<AsnEntity>().FindAsync(asn.id))!.asn_status.ShouldBe((byte)4);
        }

        [Fact]
        public async Task PutAwayAsync_DamageLocation_IncrementsDamageQty()
        {
            using var scope = new SqliteTestDbContextScope();
            var location = await SeedLocationAsync(scope.DbContext, 1, warehouseAreaProperty: 5);
            var asn = await SeedAsnAsync(scope.DbContext, 1, sortedQty: 10, actualQty: 0);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.PutAwayAsync(
                new List<AsnPutAwayInputViewModel> { new() { asn_id = asn.id, putaway_qty = 3, goods_location_id = location.id } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<AsnEntity>().FindAsync(asn.id))!.damage_qty.ShouldBe(3);
        }

        [Fact]
        public async Task PutAwayAsync_UpdatesAsnsortPutawayQtyBySeriesNumber()
        {
            using var scope = new SqliteTestDbContextScope();
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var asn = await SeedAsnAsync(scope.DbContext, 1, sortedQty: 10, actualQty: 0);
            var sort = new AsnSortEntity { asn_id = asn.id, sorted_qty = 10, putaway_qty = 0, series_number = "SN1", tenant_id = 1 };
            scope.DbContext.GetDbSet<AsnSortEntity>().Add(sort);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.PutAwayAsync(
                new List<AsnPutAwayInputViewModel> { new() { asn_id = asn.id, putaway_qty = 4, goods_location_id = location.id, series_number = "SN1" } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<AsnSortEntity>().FindAsync(sort.id))!.putaway_qty.ShouldBe(4);
        }

        [Fact]
        public async Task PutAwayAsync_AsnBelongsToDifferentTenant_ReturnsNotExists()
        {
            // Location belongs to tenant 1 (so the location check passes) - isolates the
            // Asn tenant check specifically, rather than failing earlier on the location.
            using var scope = new SqliteTestDbContextScope();
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var asn = await SeedAsnAsync(scope.DbContext, 2, sortedQty: 10, actualQty: 0);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.PutAwayAsync(
                new List<AsnPutAwayInputViewModel> { new() { asn_id = asn.id, putaway_qty = 4, goods_location_id = location.id } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<StockEntity>().AsNoTracking().CountAsync()).ShouldBe(0);
        }
    }
}
