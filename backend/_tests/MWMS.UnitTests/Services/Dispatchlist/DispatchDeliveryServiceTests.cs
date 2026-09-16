using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.Dispatchlist
{
    public class DispatchDeliveryServiceTests
    {
        private static DispatchDeliveryService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
            new(dbContext, new FakeStringLocalizer<MultiLanguage>());

        private static async Task<DispatchlistEntity> SeedDispatchlistAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId,
            byte dispatchStatus = 3, int pickedQty = 10, decimal weight = 5, decimal volume = 2)
        {
            var entity = new DispatchlistEntity
            {
                dispatch_no = "D1",
                dispatch_status = dispatchStatus,
                picked_qty = pickedQty,
                weight = weight,
                volume = volume,
                tenant_id = tenantId,
            };
            dbContext.GetDbSet<DispatchlistEntity>().Add(entity);
            await dbContext.SaveChangesAsync();
            return entity;
        }

        private static async Task<DispatchpicklistEntity> SeedPicklistAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, int dispatchlistId, int pickedQty)
        {
            var pick = new DispatchpicklistEntity { dispatchlist_id = dispatchlistId, picked_qty = pickedQty };
            dbContext.GetDbSet<DispatchpicklistEntity>().Add(pick);
            await dbContext.SaveChangesAsync();
            return pick;
        }

        private static async Task<StockEntity> SeedStockAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, int qty)
        {
            var stock = new StockEntity { qty = qty, tenant_id = tenantId };
            dbContext.GetDbSet<StockEntity>().Add(stock);
            await dbContext.SaveChangesAsync();
            return stock;
        }

        // Package

        [Fact]
        public async Task Package_ValidRow_UpdatesQtyStatusPersonAndCode()
        {
            using var scope = new SqliteTestDbContextScope();
            var entity = await SeedDispatchlistAsync(scope.DbContext, 1, dispatchStatus: 3, pickedQty: 10);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.Package(
                new List<DispatchlistPackageViewModel> { new() { id = entity.id, dispatch_status = 3, package_qty = 10 } },
                new CurrentUser { tenant_id = 1, user_name = "alice" });

            flag.ShouldBeTrue();
            var saved = (await scope.DbContext.GetDbSet<DispatchlistEntity>().FindAsync(entity.id))!;
            saved.package_qty.ShouldBe(10);
            saved.dispatch_status.ShouldBe((byte)4);
            saved.package_person.ShouldBe("alice");
            saved.package_no.ShouldNotBeNullOrEmpty();
        }

        [Fact]
        public async Task Package_ExceedsPickedQty_ReturnsError()
        {
            using var scope = new SqliteTestDbContextScope();
            var entity = await SeedDispatchlistAsync(scope.DbContext, 1, dispatchStatus: 3, pickedQty: 5);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.Package(
                new List<DispatchlistPackageViewModel> { new() { id = entity.id, dispatch_status = 3, package_qty = 10 } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task Package_StatusMismatch_ReturnsDataChanged()
        {
            using var scope = new SqliteTestDbContextScope();
            var entity = await SeedDispatchlistAsync(scope.DbContext, 1, dispatchStatus: 4, pickedQty: 10);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.Package(
                new List<DispatchlistPackageViewModel> { new() { id = entity.id, dispatch_status = 3, package_qty = 5 } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task Package_BelongsToDifferentTenant_ReturnsDataChanged()
        {
            // Regression test for the tenant filter added during the Phase 5 Option B split.
            using var scope = new SqliteTestDbContextScope();
            var entity = await SeedDispatchlistAsync(scope.DbContext, 2, dispatchStatus: 3, pickedQty: 10);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.Package(
                new List<DispatchlistPackageViewModel> { new() { id = entity.id, dispatch_status = 3, package_qty = 10 } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<DispatchlistEntity>().FindAsync(entity.id))!.package_qty.ShouldBe(0);
        }

        // Weight

        [Fact]
        public async Task Weight_ValidRow_UpdatesQtyWeightStatusPersonAndCode()
        {
            using var scope = new SqliteTestDbContextScope();
            var entity = await SeedDispatchlistAsync(scope.DbContext, 1, dispatchStatus: 3, pickedQty: 10);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.Weight(
                new List<DispatchlistWeightViewModel> { new() { id = entity.id, dispatch_status = 3, weighing_qty = 10, weighing_weight = 25.5m } },
                new CurrentUser { tenant_id = 1, user_name = "bob" });

            flag.ShouldBeTrue();
            var saved = (await scope.DbContext.GetDbSet<DispatchlistEntity>().FindAsync(entity.id))!;
            saved.weighing_qty.ShouldBe(10);
            saved.weighing_weight.ShouldBe(25.5m);
            saved.dispatch_status.ShouldBe((byte)5);
            saved.weighing_person.ShouldBe("bob");
            saved.weighing_no.ShouldNotBeNullOrEmpty();
        }

        [Fact]
        public async Task Weight_ExceedsPickedQty_ReturnsError()
        {
            using var scope = new SqliteTestDbContextScope();
            var entity = await SeedDispatchlistAsync(scope.DbContext, 1, dispatchStatus: 3, pickedQty: 5);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.Weight(
                new List<DispatchlistWeightViewModel> { new() { id = entity.id, dispatch_status = 3, weighing_qty = 10, weighing_weight = 1 } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task Weight_BelongsToDifferentTenant_ReturnsDataChanged()
        {
            using var scope = new SqliteTestDbContextScope();
            var entity = await SeedDispatchlistAsync(scope.DbContext, 2, dispatchStatus: 3, pickedQty: 10);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.Weight(
                new List<DispatchlistWeightViewModel> { new() { id = entity.id, dispatch_status = 3, weighing_qty = 10, weighing_weight = 1 } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<DispatchlistEntity>().FindAsync(entity.id))!.weighing_qty.ShouldBe(0);
        }

        // Delivery

        [Fact]
        public async Task Delivery_ValidRow_DecrementsStockAndSetsActualQty()
        {
            using var scope = new SqliteTestDbContextScope();
            var entity = await SeedDispatchlistAsync(scope.DbContext, 1, dispatchStatus: 3, pickedQty: 5);
            await SeedPicklistAsync(scope.DbContext, entity.id, pickedQty: 5);
            var stock = await SeedStockAsync(scope.DbContext, 1, qty: 10);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.Delivery(
                new List<DispatchlistDeliveryViewModel> { new() { id = entity.id, dispatch_status = 3, picked_qty = 5 } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var saved = (await scope.DbContext.GetDbSet<DispatchlistEntity>().FindAsync(entity.id))!;
            saved.dispatch_status.ShouldBe((byte)6);
            saved.actual_qty.ShouldBe(5);
            saved.intrasit_qty.ShouldBe(5);
            saved.lock_qty.ShouldBe(0);
            (await scope.DbContext.GetDbSet<StockEntity>().FindAsync(stock.id))!.qty.ShouldBe(5);
            (await scope.DbContext.GetDbSet<DispatchpicklistEntity>().AsNoTracking().FirstAsync()).is_update_stock.ShouldBeTrue();
        }

        [Fact]
        public async Task Delivery_InvalidStatus_ReturnsDataChanged()
        {
            using var scope = new SqliteTestDbContextScope();
            var entity = await SeedDispatchlistAsync(scope.DbContext, 1, dispatchStatus: 1, pickedQty: 5);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.Delivery(
                new List<DispatchlistDeliveryViewModel> { new() { id = entity.id, dispatch_status = 1, picked_qty = 5 } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task Delivery_MissingMatchingStockRow_ReturnsDataChanged()
        {
            using var scope = new SqliteTestDbContextScope();
            var entity = await SeedDispatchlistAsync(scope.DbContext, 1, dispatchStatus: 3, pickedQty: 5);
            await SeedPicklistAsync(scope.DbContext, entity.id, pickedQty: 5);
            // deliberately no matching StockEntity seeded

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.Delivery(
                new List<DispatchlistDeliveryViewModel> { new() { id = entity.id, dispatch_status = 3, picked_qty = 5 } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task Delivery_BelongsToDifferentTenant_ReturnsDataChanged()
        {
            using var scope = new SqliteTestDbContextScope();
            var entity = await SeedDispatchlistAsync(scope.DbContext, 2, dispatchStatus: 3, pickedQty: 5);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.Delivery(
                new List<DispatchlistDeliveryViewModel> { new() { id = entity.id, dispatch_status = 3, picked_qty = 5 } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<DispatchlistEntity>().FindAsync(entity.id))!.dispatch_status.ShouldBe((byte)3);
        }

        // SetFreightfee

        [Fact]
        public async Task SetFreightfee_NotYetWeighed_ComputesFromWeightAndVolume()
        {
            using var scope = new SqliteTestDbContextScope();
            var entity = await SeedDispatchlistAsync(scope.DbContext, 1, weight: 10, volume: 2);
            var freightfee = new FreightfeeEntity { carrier = "DHL", price_per_weight = 3, price_per_volume = 1, min_payment = 5, tenant_id = 1 };
            scope.DbContext.GetDbSet<FreightfeeEntity>().Add(freightfee);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.SetFreightfee(
                new List<DispatchlistFreightfeeViewModel> { new() { id = entity.id, freightfee_id = freightfee.id, waybill_no = "WB1" } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var saved = (await scope.DbContext.GetDbSet<DispatchlistEntity>().FindAsync(entity.id))!;
            saved.carrier.ShouldBe("DHL");
            saved.waybill_no.ShouldBe("WB1");
            saved.freightfee.ShouldBe(30m); // max(10*3, 2*1, 5) = 30
        }

        [Fact]
        public async Task SetFreightfee_AlreadyWeighed_ComputesFromWeighingWeight()
        {
            using var scope = new SqliteTestDbContextScope();
            var entity = await SeedDispatchlistAsync(scope.DbContext, 1, weight: 10, volume: 2);
            entity.weighing_no = "W1";
            entity.weighing_weight = 20;
            await scope.DbContext.SaveChangesAsync();
            var freightfee = new FreightfeeEntity { carrier = "DHL", price_per_weight = 3, price_per_volume = 1, min_payment = 5, tenant_id = 1 };
            scope.DbContext.GetDbSet<FreightfeeEntity>().Add(freightfee);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.SetFreightfee(
                new List<DispatchlistFreightfeeViewModel> { new() { id = entity.id, freightfee_id = freightfee.id, waybill_no = "WB1" } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<DispatchlistEntity>().FindAsync(entity.id))!.freightfee.ShouldBe(60m); // 20 * 3
        }

        [Fact]
        public async Task SetFreightfee_BelongsToDifferentTenant_LeavesRowUntouched()
        {
            // Regression test: this method took no CurrentUser at all before the Phase 5 fix.
            using var scope = new SqliteTestDbContextScope();
            var entity = await SeedDispatchlistAsync(scope.DbContext, 2, weight: 10, volume: 2);
            var freightfee = new FreightfeeEntity { carrier = "DHL", price_per_weight = 3, price_per_volume = 1, min_payment = 5, tenant_id = 2 };
            scope.DbContext.GetDbSet<FreightfeeEntity>().Add(freightfee);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.SetFreightfee(
                new List<DispatchlistFreightfeeViewModel> { new() { id = entity.id, freightfee_id = freightfee.id, waybill_no = "WB1" } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<DispatchlistEntity>().FindAsync(entity.id))!.carrier.ShouldBe("");
        }

        // SignForArrival

        [Fact]
        public async Task SignForArrival_ValidRow_ComputesSignQtyAndSetsStatus()
        {
            using var scope = new SqliteTestDbContextScope();
            var entity = await SeedDispatchlistAsync(scope.DbContext, 1, dispatchStatus: 6);
            entity.actual_qty = 10;
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.SignForArrival(
                new List<DispatchlistSignViewModel> { new() { id = entity.id, dispatch_status = 6, damage_qty = 2 } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var saved = (await scope.DbContext.GetDbSet<DispatchlistEntity>().FindAsync(entity.id))!;
            saved.dispatch_status.ShouldBe((byte)7);
            saved.damage_qty.ShouldBe(2);
            saved.sign_qty.ShouldBe(8);
        }

        [Fact]
        public async Task SignForArrival_MultipleRowsSameStatus_EachGetsItsOwnDamageQty()
        {
            // Regression test for Issue 7: the id match used to be "t.id == t.id" (always true),
            // so every row with a matching dispatch_status silently got whichever viewmodel matched first.
            using var scope = new SqliteTestDbContextScope();
            var entity1 = await SeedDispatchlistAsync(scope.DbContext, 1, dispatchStatus: 6);
            entity1.actual_qty = 10;
            var entity2 = new DispatchlistEntity { dispatch_no = "D2", dispatch_status = 6, actual_qty = 20, tenant_id = 1 };
            scope.DbContext.GetDbSet<DispatchlistEntity>().Add(entity2);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.SignForArrival(
                new List<DispatchlistSignViewModel>
                {
                    new() { id = entity1.id, dispatch_status = 6, damage_qty = 1 },
                    new() { id = entity2.id, dispatch_status = 6, damage_qty = 5 },
                },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<DispatchlistEntity>().FindAsync(entity1.id))!.damage_qty.ShouldBe(1);
            (await scope.DbContext.GetDbSet<DispatchlistEntity>().FindAsync(entity2.id))!.damage_qty.ShouldBe(5);
        }

        [Fact]
        public async Task SignForArrival_StatusMismatch_ReturnsDataChanged()
        {
            using var scope = new SqliteTestDbContextScope();
            var entity = await SeedDispatchlistAsync(scope.DbContext, 1, dispatchStatus: 6);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.SignForArrival(
                new List<DispatchlistSignViewModel> { new() { id = entity.id, dispatch_status = 5, damage_qty = 0 } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task SignForArrival_BelongsToDifferentTenant_LeavesRowUntouched()
        {
            // Regression test: this method took no CurrentUser at all before the Phase 5 fix.
            using var scope = new SqliteTestDbContextScope();
            var entity = await SeedDispatchlistAsync(scope.DbContext, 2, dispatchStatus: 6);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.SignForArrival(
                new List<DispatchlistSignViewModel> { new() { id = entity.id, dispatch_status = 6, damage_qty = 3 } },
                new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<DispatchlistEntity>().FindAsync(entity.id))!.damage_qty.ShouldBe(0);
        }
    }
}
