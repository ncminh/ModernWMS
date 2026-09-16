using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.Dispatchlist
{
    public class DispatchConfirmServiceTests
    {
        private static DispatchConfirmService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
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

        private static async Task<DispatchlistEntity> SeedDispatchlistAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, int skuId,
            string dispatchNo = "D1", byte dispatchStatus = 0, int qty = 5, int lockQty = 0, int pickedQty = 0)
        {
            var entity = new DispatchlistEntity
            {
                dispatch_no = dispatchNo,
                dispatch_status = dispatchStatus,
                sku_id = skuId,
                qty = qty,
                lock_qty = lockQty,
                picked_qty = pickedQty,
                tenant_id = tenantId,
            };
            dbContext.GetDbSet<DispatchlistEntity>().Add(entity);
            await dbContext.SaveChangesAsync();
            return entity;
        }

        private static async Task<GoodslocationEntity> SeedLocationAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId)
        {
            var location = new GoodslocationEntity { location_name = "L1", warehouse_name = "W1", warehouse_area_name = "A1", warehouse_area_property = 1, tenant_id = tenantId };
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

        private static async Task<DispatchpicklistEntity> SeedPicklistAsync(
            ModernWMS.Core.DBContext.SqlDBContext dbContext, int dispatchlistId, int skuId,
            int pickQty = 5, int pickedQty = 0, int pickerId = 0)
        {
            var pick = new DispatchpicklistEntity { dispatchlist_id = dispatchlistId, sku_id = skuId, pick_qty = pickQty, picked_qty = pickedQty, picker_id = pickerId };
            dbContext.GetDbSet<DispatchpicklistEntity>().Add(pick);
            await dbContext.SaveChangesAsync();
            return pick;
        }

        // ConfirmOrderCheck

        [Fact]
        public async Task ConfirmOrderCheck_SufficientStock_ReturnsConfirmTrueWithPickList()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 10);
            var dispatchlist = await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, qty: 5);

            var service = CreateService(scope.DbContext);
            var result = await service.ConfirmOrderCheck("D1", new CurrentUser { tenant_id = 1, user_id = 42 });

            var r = result.ShouldHaveSingleItem();
            r.confirm.ShouldBeTrue();
            r.qty_available.ShouldBe(10);
            r.pick_list.ShouldHaveSingleItem().pick_qty.ShouldBe(5);
        }

        [Fact]
        public async Task ConfirmOrderCheck_InsufficientStock_ReturnsConfirmFalse()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 2);
            await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, qty: 5);

            var service = CreateService(scope.DbContext);
            var result = await service.ConfirmOrderCheck("D1", new CurrentUser { tenant_id = 1, user_id = 1 });

            result.ShouldHaveSingleItem().confirm.ShouldBeFalse();
        }

        [Fact]
        public async Task ConfirmOrderCheck_CurrentUserIdDiffersFromTenantId_StockIsStillFound()
        {
            // Regression test for Issue 6: the stock-availability query used to compare
            // stock.tenant_id against currentUser.user_id instead of currentUser.tenant_id.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 10);
            await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, qty: 5);

            var service = CreateService(scope.DbContext);
            // tenant_id (1) deliberately does not equal user_id (999) - pre-fix this made qty_available come back as 0.
            var result = await service.ConfirmOrderCheck("D1", new CurrentUser { tenant_id = 1, user_id = 999 });

            var r = result.ShouldHaveSingleItem();
            r.qty_available.ShouldBe(10);
            r.confirm.ShouldBeTrue();
        }

        [Fact]
        public async Task ConfirmOrderCheck_BelongsToDifferentTenant_ReturnsEmpty()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, qty: 5);

            var service = CreateService(scope.DbContext);
            var result = await service.ConfirmOrderCheck("D1", new CurrentUser { tenant_id = 2, user_id = 2 });

            result.ShouldBeEmpty();
        }

        // ConfirmOrder

        [Fact]
        public async Task ConfirmOrder_Confirmed_LocksQtyAndCreatesPickRow()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var stock = await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 10);
            var dispatchlist = await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, qty: 5);

            var service = CreateService(scope.DbContext);
            var viewModel = new DispatchlistConfirmDetailViewModel
            {
                dispatchlist_id = dispatchlist.id,
                sku_id = sku.id,
                qty = 5,
                confirm = true,
                pick_list = new List<DispatchlistConfirmPickDetailViewModel>
                {
                    new() { stock_id = stock.id, dispatchlist_id = dispatchlist.id, goods_location_id = location.id, pick_qty = 5 },
                },
            };

            var (flag, _) = await service.ConfirmOrder(new List<DispatchlistConfirmDetailViewModel> { viewModel }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var saved = (await scope.DbContext.GetDbSet<DispatchlistEntity>().FindAsync(dispatchlist.id))!;
            saved.dispatch_status.ShouldBe((byte)2);
            saved.lock_qty.ShouldBe(5);
            var pick = await scope.DbContext.GetDbSet<DispatchpicklistEntity>().AsNoTracking().FirstAsync();
            pick.dispatchlist_id.ShouldBe(dispatchlist.id);
            pick.pick_qty.ShouldBe(5);
        }

        [Fact]
        public async Task ConfirmOrder_ConfirmedWithRemainder_SplitsIntoNewDispatchlist()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var stock = await SeedStockAsync(scope.DbContext, 1, sku.id, location.id, qty: 10);
            var dispatchlist = await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, qty: 10);

            var service = CreateService(scope.DbContext);
            var viewModel = new DispatchlistConfirmDetailViewModel
            {
                dispatchlist_id = dispatchlist.id,
                sku_id = sku.id,
                qty = 10,
                confirm = true,
                pick_list = new List<DispatchlistConfirmPickDetailViewModel>
                {
                    new() { stock_id = stock.id, dispatchlist_id = dispatchlist.id, goods_location_id = location.id, pick_qty = 6 },
                },
            };

            var (flag, _) = await service.ConfirmOrder(new List<DispatchlistConfirmDetailViewModel> { viewModel }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var rows = await scope.DbContext.GetDbSet<DispatchlistEntity>().AsNoTracking().ToListAsync();
            rows.Count.ShouldBe(2);
            rows.Sum(t => t.qty).ShouldBe(10);
            rows.ShouldContain(t => t.dispatch_status == 2 && t.qty == 6);
            rows.ShouldContain(t => t.dispatch_status == 1 && t.qty == 4);
        }

        [Fact]
        public async Task ConfirmOrder_NotConfirmed_RequeuesRemainderAndRemovesOriginal()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var dispatchlist = await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, qty: 5);

            var service = CreateService(scope.DbContext);
            var viewModel = new DispatchlistConfirmDetailViewModel
            {
                dispatchlist_id = dispatchlist.id,
                sku_id = sku.id,
                qty = 5,
                confirm = false,
            };

            var (flag, _) = await service.ConfirmOrder(new List<DispatchlistConfirmDetailViewModel> { viewModel }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<DispatchlistEntity>().AsNoTracking().AnyAsync(t => t.id == dispatchlist.id)).ShouldBeFalse();
            var requeued = await scope.DbContext.GetDbSet<DispatchlistEntity>().AsNoTracking().FirstAsync();
            requeued.dispatch_status.ShouldBe((byte)1);
            requeued.qty.ShouldBe(5);
        }

        [Fact]
        public async Task ConfirmOrder_BelongsToDifferentTenant_ReturnsDataChanged()
        {
            // Regression test: the dispatchlist_datas fetch used to have no tenant filter at all.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var dispatchlist = await SeedDispatchlistAsync(scope.DbContext, 2, sku.id, qty: 5);

            var service = CreateService(scope.DbContext);
            var viewModel = new DispatchlistConfirmDetailViewModel { dispatchlist_id = dispatchlist.id, sku_id = sku.id, qty = 5, confirm = true };

            var (flag, _) = await service.ConfirmOrder(new List<DispatchlistConfirmDetailViewModel> { viewModel }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<DispatchlistEntity>().FindAsync(dispatchlist.id))!.dispatch_status.ShouldBe((byte)0);
        }

        // ConfirmPickByDispatchNo

        [Fact]
        public async Task ConfirmPickByDispatchNo_StatusTwoRows_PicksConfirmedAndStamped()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var dispatchlist = await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchStatus: 2, qty: 5, lockQty: 5);
            var pick = await SeedPicklistAsync(scope.DbContext, dispatchlist.id, sku.id, pickQty: 5);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ConfirmPickByDispatchNo("D1", new CurrentUser { tenant_id = 1, user_id = 7, user_name = "alice" });

            flag.ShouldBeTrue();
            var saved = (await scope.DbContext.GetDbSet<DispatchlistEntity>().FindAsync(dispatchlist.id))!;
            saved.dispatch_status.ShouldBe((byte)3);
            saved.picked_qty.ShouldBe(5);
            saved.pick_checker.ShouldBe("alice");
            saved.pick_checker_id.ShouldBe(7);
            (await scope.DbContext.GetDbSet<DispatchpicklistEntity>().FindAsync(pick.id))!.picked_qty.ShouldBe(5);
        }

        [Fact]
        public async Task ConfirmPickByDispatchNo_BelongsToDifferentTenant_LeavesRowUntouched()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var dispatchlist = await SeedDispatchlistAsync(scope.DbContext, 2, sku.id, dispatchStatus: 2, qty: 5, lockQty: 5);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ConfirmPickByDispatchNo("D1", new CurrentUser { tenant_id = 1, user_id = 7, user_name = "alice" });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<DispatchlistEntity>().FindAsync(dispatchlist.id))!.dispatch_status.ShouldBe((byte)2);
        }

        // ConfirmPickDetail

        [Fact]
        public async Task ConfirmPickDetail_UnpickedRow_StampsPickerAndPickerId()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var dispatchlist = await SeedDispatchlistAsync(scope.DbContext, 1, sku.id);
            var pick = await SeedPicklistAsync(scope.DbContext, dispatchlist.id, sku.id);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ConfirmPickDetail(new List<int> { pick.id }, new CurrentUser { tenant_id = 1, user_id = 3, user_name = "bob" });

            flag.ShouldBeTrue();
            var saved = (await scope.DbContext.GetDbSet<DispatchpicklistEntity>().FindAsync(pick.id))!;
            saved.picker.ShouldBe("bob");
            saved.picker_id.ShouldBe(3);
        }

        [Fact]
        public async Task ConfirmPickDetail_AlreadyPicked_ReturnsDataChanged()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var dispatchlist = await SeedDispatchlistAsync(scope.DbContext, 1, sku.id);
            var pick = await SeedPicklistAsync(scope.DbContext, dispatchlist.id, sku.id, pickerId: 5);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ConfirmPickDetail(new List<int> { pick.id }, new CurrentUser { tenant_id = 1, user_id = 3, user_name = "bob" });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task ConfirmPickDetail_BelongsToDifferentTenant_IsIgnored()
        {
            // Regression test: CurrentUser was already a parameter but was never used to scope the query.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var dispatchlist = await SeedDispatchlistAsync(scope.DbContext, 2, sku.id);
            var pick = await SeedPicklistAsync(scope.DbContext, dispatchlist.id, sku.id);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ConfirmPickDetail(new List<int> { pick.id }, new CurrentUser { tenant_id = 1, user_id = 3, user_name = "bob" });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<DispatchpicklistEntity>().FindAsync(pick.id))!.picker_id.ShouldBe(0);
        }

        // CancelConfirmPickDetail

        [Fact]
        public async Task CancelConfirmPickDetail_PickedRow_ClearsPicker()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var dispatchlist = await SeedDispatchlistAsync(scope.DbContext, 1, sku.id);
            var pick = await SeedPicklistAsync(scope.DbContext, dispatchlist.id, sku.id, pickerId: 5);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.CancelConfirmPickDetail(new List<int> { pick.id }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var saved = (await scope.DbContext.GetDbSet<DispatchpicklistEntity>().FindAsync(pick.id))!;
            saved.picker.ShouldBe("");
            saved.picker_id.ShouldBe(0);
        }

        [Fact]
        public async Task CancelConfirmPickDetail_NotYetPicked_ReturnsDataChanged()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var dispatchlist = await SeedDispatchlistAsync(scope.DbContext, 1, sku.id);
            var pick = await SeedPicklistAsync(scope.DbContext, dispatchlist.id, sku.id, pickerId: 0);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.CancelConfirmPickDetail(new List<int> { pick.id }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task CancelConfirmPickDetail_BelongsToDifferentTenant_IsIgnored()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var dispatchlist = await SeedDispatchlistAsync(scope.DbContext, 2, sku.id);
            var pick = await SeedPicklistAsync(scope.DbContext, dispatchlist.id, sku.id, pickerId: 5);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.CancelConfirmPickDetail(new List<int> { pick.id }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<DispatchpicklistEntity>().FindAsync(pick.id))!.picker_id.ShouldBe(5);
        }

        // CancelOrderOpration

        [Fact]
        public async Task CancelOrderOpration_StatusThree_ResetsPickedQtyAndStatusToTwo()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var dispatchlist = await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchStatus: 3, pickedQty: 5);
            var pick = await SeedPicklistAsync(scope.DbContext, dispatchlist.id, sku.id, pickedQty: 5);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.CancelOrderOpration(new CancelOrderOprationViewModel { dispatch_no = "D1", dispatch_status = 3 }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var saved = (await scope.DbContext.GetDbSet<DispatchlistEntity>().FindAsync(dispatchlist.id))!;
            saved.dispatch_status.ShouldBe((byte)2);
            saved.picked_qty.ShouldBe(0);
            (await scope.DbContext.GetDbSet<DispatchpicklistEntity>().FindAsync(pick.id))!.picked_qty.ShouldBe(0);
        }

        [Fact]
        public async Task CancelOrderOpration_StatusTwo_RemovesPickRowsAndResetsLockQty()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var dispatchlist = await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchStatus: 2, lockQty: 5);
            await SeedPicklistAsync(scope.DbContext, dispatchlist.id, sku.id);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.CancelOrderOpration(new CancelOrderOprationViewModel { dispatch_no = "D1", dispatch_status = 2 }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var saved = (await scope.DbContext.GetDbSet<DispatchlistEntity>().FindAsync(dispatchlist.id))!;
            saved.dispatch_status.ShouldBe((byte)1);
            saved.lock_qty.ShouldBe(0);
            (await scope.DbContext.GetDbSet<DispatchpicklistEntity>().AsNoTracking().AnyAsync()).ShouldBeFalse();
        }

        [Fact]
        public async Task CancelOrderOpration_NoMatchingRows_ReturnsStatusChanged()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.CancelOrderOpration(new CancelOrderOprationViewModel { dispatch_no = "MISSING", dispatch_status = 3 }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task CancelOrderOpration_BelongsToDifferentTenant_ReturnsStatusChanged()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 2);
            await SeedDispatchlistAsync(scope.DbContext, 2, sku.id, dispatchStatus: 3);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.CancelOrderOpration(new CancelOrderOprationViewModel { dispatch_no = "D1", dispatch_status = 3 }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        // CancelDispatchlistDetailOpration

        [Fact]
        public async Task CancelDispatchlistDetailOpration_StatusFourNoWeighing_RevertsToThree()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var dispatchlist = await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchStatus: 4);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.CancelDispatchlistDetailOpration(dispatchlist.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<DispatchlistEntity>().FindAsync(dispatchlist.id))!.dispatch_status.ShouldBe((byte)3);
        }

        [Fact]
        public async Task CancelDispatchlistDetailOpration_StatusFourWithWeighing_RevertsToFive()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var dispatchlist = await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchStatus: 4);
            dispatchlist.weighing_no = "W1";
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.CancelDispatchlistDetailOpration(dispatchlist.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<DispatchlistEntity>().FindAsync(dispatchlist.id))!.dispatch_status.ShouldBe((byte)5);
        }

        [Fact]
        public async Task CancelDispatchlistDetailOpration_StatusFiveNoPackage_RevertsToThree()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var dispatchlist = await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchStatus: 5);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.CancelDispatchlistDetailOpration(dispatchlist.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<DispatchlistEntity>().FindAsync(dispatchlist.id))!.dispatch_status.ShouldBe((byte)3);
        }

        [Fact]
        public async Task CancelDispatchlistDetailOpration_StatusFiveWithPackage_RevertsToFour()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var dispatchlist = await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchStatus: 5);
            dispatchlist.package_no = "P1";
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.CancelDispatchlistDetailOpration(dispatchlist.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<DispatchlistEntity>().FindAsync(dispatchlist.id))!.dispatch_status.ShouldBe((byte)4);
        }

        [Fact]
        public async Task CancelDispatchlistDetailOpration_UnsupportedStatus_ReturnsStatusChanged()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var dispatchlist = await SeedDispatchlistAsync(scope.DbContext, 1, sku.id, dispatchStatus: 2);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.CancelDispatchlistDetailOpration(dispatchlist.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task CancelDispatchlistDetailOpration_UnknownId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.CancelDispatchlistDetailOpration(999, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task CancelDispatchlistDetailOpration_BelongsToDifferentTenant_ReturnsNotExists()
        {
            // Regression test: this method took no CurrentUser at all before the Phase 5 fix.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var dispatchlist = await SeedDispatchlistAsync(scope.DbContext, 2, sku.id, dispatchStatus: 4);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.CancelDispatchlistDetailOpration(dispatchlist.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<DispatchlistEntity>().FindAsync(dispatchlist.id))!.dispatch_status.ShouldBe((byte)4);
        }

        // GetPickListByDispatchID

        [Fact]
        public async Task GetPickListByDispatchID_ReturnsJoinedLocationAndSkuData()
        {
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 1);
            var location = await SeedLocationAsync(scope.DbContext, 1);
            var dispatchlist = await SeedDispatchlistAsync(scope.DbContext, 1, sku.id);
            var pick = new DispatchpicklistEntity { dispatchlist_id = dispatchlist.id, sku_id = sku.id, goods_location_id = location.id, pick_qty = 5 };
            scope.DbContext.GetDbSet<DispatchpicklistEntity>().Add(pick);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var data = await service.GetPickListByDispatchID(dispatchlist.id, new CurrentUser { tenant_id = 1 });

            var row = data.ShouldHaveSingleItem();
            row.sku_code.ShouldBe("SKU1");
            row.location_name.ShouldBe("L1");
        }

        [Fact]
        public async Task GetPickListByDispatchID_BelongsToDifferentTenant_ReturnsEmpty()
        {
            // Regression test: this method took no CurrentUser at all before the Phase 5 fix.
            using var scope = new SqliteTestDbContextScope();
            var (_, sku) = await SeedSpuSkuAsync(scope.DbContext, 2);
            var location = await SeedLocationAsync(scope.DbContext, 2);
            var dispatchlist = await SeedDispatchlistAsync(scope.DbContext, 2, sku.id);
            var pick = new DispatchpicklistEntity { dispatchlist_id = dispatchlist.id, sku_id = sku.id, goods_location_id = location.id, pick_qty = 5 };
            scope.DbContext.GetDbSet<DispatchpicklistEntity>().Add(pick);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var data = await service.GetPickListByDispatchID(dispatchlist.id, new CurrentUser { tenant_id = 1 });

            data.ShouldBeEmpty();
        }
    }
}
