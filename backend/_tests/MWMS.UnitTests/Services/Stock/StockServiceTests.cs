using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Entities.ViewModels.Stock;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.Stock
{
    public class StockServiceTests
    {
        private static StockService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
            new(dbContext, new FakeStringLocalizer<MultiLanguage>());

        private static async Task<(SpuEntity spu, SkuEntity sku)> SeedSpuSkuAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId)
        {
            var spu = new SpuEntity { spu_code = "SPU1", spu_name = "SpuOne", tenant_id = tenantId };
            dbContext.GetDbSet<SpuEntity>().Add(spu);
            await dbContext.SaveChangesAsync();
            var sku = new SkuEntity { spu_id = spu.id, sku_code = "SKU1", sku_name = "SkuOne", price = 10 };
            dbContext.GetDbSet<SkuEntity>().Add(sku);
            await dbContext.SaveChangesAsync();
            return (spu, sku);
        }

        private static async Task<GoodsLocationEntity> SeedLocationAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, int warehouseId = 0,
            string warehouseName = "W1", string locationName = "L1", byte areaProperty = 1)
        {
            var location = new GoodsLocationEntity
            {
                location_name = locationName,
                warehouse_id = warehouseId,
                warehouse_name = warehouseName,
                warehouse_area_property = areaProperty,
                tenant_id = tenantId,
            };
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

        private static async Task SeedDispatchLockAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, int skuId, int locationId, int pickQty)
        {
            var dispatch = new DispatchListEntity { dispatch_no = "D1", dispatch_status = 2, sku_id = skuId, lock_qty = pickQty, tenant_id = tenantId };
            dbContext.GetDbSet<DispatchListEntity>().Add(dispatch);
            await dbContext.SaveChangesAsync();
            dbContext.GetDbSet<DispatchPickListEntity>().Add(new DispatchPickListEntity
            {
                dispatchlist_id = dispatch.id,
                sku_id = skuId,
                goods_location_id = locationId,
                pick_qty = pickQty,
            });
            await dbContext.SaveChangesAsync();
        }

        private static async Task SeedProcessLockAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, int skuId, int locationId, int qty)
        {
            var process = new StockProcessEntity { job_code = "P1", tenant_id = tenantId };
            dbContext.GetDbSet<StockProcessEntity>().Add(process);
            await dbContext.SaveChangesAsync();
            dbContext.GetDbSet<StockProcessDetailEntity>().Add(new StockProcessDetailEntity
            {
                stock_process_id = process.id,
                sku_id = skuId,
                goods_location_id = locationId,
                qty = qty,
                is_source = true,
                is_update_stock = false,
                tenant_id = tenantId,
            });
            await dbContext.SaveChangesAsync();
        }

        private static async Task SeedMoveLockAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, int skuId, int origLocationId, int qty)
        {
            dbContext.GetDbSet<StockMoveEntity>().Add(new StockMoveEntity
            {
                sku_id = skuId,
                orig_goods_location_id = origLocationId,
                qty = qty,
                move_status = 0,
                tenant_id = tenantId,
            });
            await dbContext.SaveChangesAsync();
        }

        // StockPageAsync

        [Fact]
        public async Task StockPageAsync_ComputesAvailableQtyAcrossAllLocksAndFrozen()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 20);
            await SeedDispatchLockAsync(scope.DbContext, 1, sku.id, location.id, pickQty: 3);
            await SeedProcessLockAsync(scope.DbContext, 1, sku.id, location.id, qty: 2);
            await SeedMoveLockAsync(scope.DbContext, 1, sku.id, location.id, qty: 1);

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.StockPageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
            var row = data.ShouldHaveSingleItem();
            row.qty.ShouldBe(20);
            row.qty_locked.ShouldBe(6);
            row.qty_available.ShouldBe(14);
        }

        [Fact]
        public async Task StockPageAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku1) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location1 = await SeedLocationAsync(scope.DbContext, 1);
            await SeedStockAsync(scope.DbContext, 1, sku1.id, location1.id, qty: 5);
            var (_, sku2) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var location2 = await SeedLocationAsync(scope.DbContext, 2);
            await SeedStockAsync(scope.DbContext, 2, sku2.id, location2.id, qty: 5);

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.StockPageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
        }

        // LocationStockPageAsync

        [Fact]
        public async Task LocationStockPageAsync_ComputesPerLocationAvailableQty()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 10);
            await SeedDispatchLockAsync(scope.DbContext, 1, sku.id, location.id, pickQty: 4);

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.LocationStockPageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
            var row = data.ShouldHaveSingleItem();
            row.qty.ShouldBe(10);
            row.qty_locked.ShouldBe(4);
            row.qty_available.ShouldBe(6);
        }

        [Fact]
        public async Task LocationStockPageAsync_DamageLocation_QtyAvailableIsZero()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1, areaProperty: 5);
            await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 10);

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.LocationStockPageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            data.ShouldHaveSingleItem().qty_available.ShouldBe(0);
        }

        // SafetyStockPageAsync

        [Fact]
        public async Task SafetyStockPageAsync_WarehouseIdDiffersFromAnyLocationId_StillFindsCorrectWarehouseName()
        {
            // Regression test for Issue 8: the join used to match sg.warehouse_id against
            // gl.id (a location's own primary key) instead of an actual WarehouseEntity,
            // silently dropping every row whose warehouse id didn't coincidentally equal
            // some location's id.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            for (int i = 0; i < 2; i++)
            {
                scope.DbContext.GetDbSet<WarehouseEntity>().Add(new WarehouseEntity { warehouse_name = $"junk{i}", tenant_id = 1 });
            }
            await scope.DbContext.SaveChangesAsync();
            var warehouse = new WarehouseEntity { warehouse_name = "RealWarehouse", tenant_id = 1 };
            scope.DbContext.GetDbSet<WarehouseEntity>().Add(warehouse);
            await scope.DbContext.SaveChangesAsync();
            var location = await SeedLocationAsync(scope.DbContext, 1, warehouseId: warehouse.id, warehouseName: warehouse.warehouse_name);
            await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 10);

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.SafetyStockPageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
            var row = data.ShouldHaveSingleItem();
            row.warehouse_name.ShouldBe("RealWarehouse");
            row.qty_available.ShouldBe(10);
        }

        [Fact]
        public async Task SafetyStockPageAsync_MatchingSkuSafetyStockRow_ReturnsSafetyStockQty()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var warehouse = new WarehouseEntity { warehouse_name = "W1", tenant_id = 1 };
            scope.DbContext.GetDbSet<WarehouseEntity>().Add(warehouse);
            await scope.DbContext.SaveChangesAsync();
            var location = await SeedLocationAsync(scope.DbContext, 1, warehouseId: warehouse.id, warehouseName: warehouse.warehouse_name);
            await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 10);
            scope.DbContext.GetDbSet<SkuSafetyStockEntity>().Add(new SkuSafetyStockEntity { sku_id = sku.id, warehouse_id = warehouse.id, safety_stock_qty = 15 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.SafetyStockPageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            data.ShouldHaveSingleItem().safety_stock_qty.ShouldBe(15);
        }

        // SelectPageAsync

        [Fact]
        public async Task SelectPageAsync_DefaultFilter_OnlyReturnsRowsWithAvailableQty()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 10);
            await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 0);

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.SelectPageAsync(new PageSearch { sqlTitle = "" }, new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
            data.ShouldHaveSingleItem().qty.ShouldBe(10);
        }

        [Fact]
        public async Task SelectPageAsync_FrozenFilter_OnlyReturnsFrozenRows()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 10, isFreeze: true);
            await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 5, isFreeze: false);

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.SelectPageAsync(new PageSearch { sqlTitle = "frozen" }, new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
            data.ShouldHaveSingleItem().is_freeze.ShouldBeTrue();
        }

        [Fact]
        public async Task SelectPageAsync_AllFilter_ReturnsEveryRowRegardlessOfAvailability()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 0);

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.SelectPageAsync(new PageSearch { sqlTitle = "all" }, new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
        }

        // SkuSelectPageAsync

        [Fact]
        public async Task SkuSelectPageAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            await SeedSpuSkuAsync(scope.DbContext, 1);
            await SeedSpuSkuAsync(scope.DbContext, 2);

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.SkuSelectPageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
        }

        // LocationStockForPhoneAsync

        [Fact]
        public async Task LocationStockForPhoneAsync_FiltersBySkuId()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku1) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            await SeedStockAsync(scope.DbContext, 1, sku1.id, location.id, qty: 10);
            var sku2 = new SkuEntity { spu_id = sku1.spu_id, sku_code = "SKU2" };
            scope.DbContext.GetDbSet<SkuEntity>().Add(sku2);
            await scope.DbContext.SaveChangesAsync();
            await SeedStockAsync(scope.DbContext, 1, sku2.id, location.id, qty: 5);

            var service = CreateService(scope.DbContext);
            var data = await service.LocationStockForPhoneAsync(new LocationStockForPhoneSearchViewModel { sku_id = sku1.id }, new CurrentUser { tenant_id = 1 });

            data.ShouldHaveSingleItem().qty.ShouldBe(10);
        }

        [Fact]
        public async Task LocationStockForPhoneAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku1) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location1 = await SeedLocationAsync(scope.DbContext, 1);
            await SeedStockAsync(scope.DbContext, 1, sku1.id, location1.id, qty: 10);
            var (_, sku2) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var location2 = await SeedLocationAsync(scope.DbContext, 2);
            await SeedStockAsync(scope.DbContext, 2, sku2.id, location2.id, qty: 10);

            var service = CreateService(scope.DbContext);
            var data = await service.LocationStockForPhoneAsync(new LocationStockForPhoneSearchViewModel(), new CurrentUser { tenant_id = 1 });

            data.ShouldHaveSingleItem();
        }

        // DeliveryStatistic

        [Fact]
        public async Task DeliveryStatistic_ComputesDeliveryQtyAndAmount()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var warehouse = new WarehouseEntity { warehouse_name = "W1", tenant_id = 1 };
            scope.DbContext.GetDbSet<WarehouseEntity>().Add(warehouse);
            await scope.DbContext.SaveChangesAsync();
            var location = await SeedLocationAsync(scope.DbContext, 1, warehouseId: warehouse.id, warehouseName: warehouse.warehouse_name);
            var owner = new GoodsOwnerEntity { goods_owner_name = "Owner1", tenant_id = 1 };
            scope.DbContext.GetDbSet<GoodsOwnerEntity>().Add(owner);
            await scope.DbContext.SaveChangesAsync();
            var dispatch = new DispatchListEntity { dispatch_no = "D1", dispatch_status = 6, sku_id = sku.id, customer_name = "Cust1", tenant_id = 1 };
            scope.DbContext.GetDbSet<DispatchListEntity>().Add(dispatch);
            await scope.DbContext.SaveChangesAsync();
            scope.DbContext.GetDbSet<DispatchPickListEntity>().Add(new DispatchPickListEntity
            {
                dispatchlist_id = dispatch.id,
                sku_id = sku.id,
                goods_location_id = location.id,
                goods_owner_id = owner.id,
                picked_qty = 4,
            });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.DeliveryStatistic(new DeliveryStatisticSearchViewModel(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
            var row = data.ShouldHaveSingleItem();
            row.delivery_qty.ShouldBe(4);
            row.delivery_amount.ShouldBe(40); // 4 * sku.price(10)
        }

        [Fact]
        public async Task DeliveryStatistic_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var warehouse = new WarehouseEntity { warehouse_name = "W1", tenant_id = 2 };
            scope.DbContext.GetDbSet<WarehouseEntity>().Add(warehouse);
            await scope.DbContext.SaveChangesAsync();
            var location = await SeedLocationAsync(scope.DbContext, 2, warehouseId: warehouse.id, warehouseName: warehouse.warehouse_name);
            var owner = new GoodsOwnerEntity { goods_owner_name = "Owner1", tenant_id = 2 };
            scope.DbContext.GetDbSet<GoodsOwnerEntity>().Add(owner);
            await scope.DbContext.SaveChangesAsync();
            var dispatch = new DispatchListEntity { dispatch_no = "D1", dispatch_status = 6, sku_id = sku.id, tenant_id = 2 };
            scope.DbContext.GetDbSet<DispatchListEntity>().Add(dispatch);
            await scope.DbContext.SaveChangesAsync();
            scope.DbContext.GetDbSet<DispatchPickListEntity>().Add(new DispatchPickListEntity
            {
                dispatchlist_id = dispatch.id,
                sku_id = sku.id,
                goods_location_id = location.id,
                goods_owner_id = owner.id,
                picked_qty = 4,
            });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.DeliveryStatistic(new DeliveryStatisticSearchViewModel(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(0);
        }

        // StockAgePageAsync

        [Fact]
        public async Task StockAgePageAsync_ComputesStockAgeFromPutawayDate()
        {
            // Regression test: this used to throw InvalidOperationException under every
            // provider except MySQL (including SQLite) whenever a real putaway_date was
            // used, because it pushed a provider-specific DateDiffDay function into the
            // translated query.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            await scope.DbContext.GetDbSet<StockEntity>().AddAsync(new StockEntity
            {
                sku_id = sku.id,
                goods_location_id = location.id,
                qty = 5,
                tenant_id = 1,
                putaway_date = DateTime.Today.AddDays(-7),
            });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.StockAgePageAsync(new StockAgeSearchViewModel(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
            data.ShouldHaveSingleItem().stock_age.ShouldBe(7);
        }

        [Fact]
        public async Task StockAgePageAsync_NeverPutAway_StockAgeIsZero()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 5);

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.StockAgePageAsync(new StockAgeSearchViewModel(), new CurrentUser { tenant_id = 1 });

            data.ShouldHaveSingleItem().stock_age.ShouldBe(0);
        }

        [Fact]
        public async Task StockAgePageAsync_FiltersByStockAgeRange()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            await scope.DbContext.GetDbSet<StockEntity>().AddAsync(new StockEntity { sku_id = sku.id, goods_location_id = location.id, qty = 5, tenant_id = 1, putaway_date = DateTime.Today.AddDays(-2) });
            await scope.DbContext.GetDbSet<StockEntity>().AddAsync(new StockEntity { sku_id = sku.id, goods_location_id = location.id, qty = 5, tenant_id = 1, putaway_date = DateTime.Today.AddDays(-20) });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.StockAgePageAsync(new StockAgeSearchViewModel { stock_age_from = 10 }, new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
            data.ShouldHaveSingleItem().stock_age.ShouldBe(20);
        }

        [Fact]
        public async Task StockAgePageAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku1) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location1 = await SeedLocationAsync(scope.DbContext, 1);
            await SeedStockAsync(scope.DbContext, 1, sku1.id, location1.id, qty: 5);
            var (_, sku2) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var location2 = await SeedLocationAsync(scope.DbContext, 2);
            await SeedStockAsync(scope.DbContext, 2, sku2.id, location2.id, qty: 5);

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.StockAgePageAsync(new StockAgeSearchViewModel(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
        }
    }
}
