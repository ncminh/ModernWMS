using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.Warehouse
{
    public class WarehouseServiceTests
    {
        private static WarehouseService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
            new(dbContext, new FakeStringLocalizer<MultiLanguage>());

        [Fact]
        public async Task PageAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<WarehouseEntity>().AddRange(
                new WarehouseEntity { warehouse_name = "Tenant1-WH", tenant_id = 1 },
                new WarehouseEntity { warehouse_name = "Tenant2-WH", tenant_id = 2 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.PageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
            data.ShouldHaveSingleItem().warehouse_name.ShouldBe("Tenant1-WH");
        }

        [Fact]
        public async Task AddAsync_NewName_PersistsAndStampsAuditColumns()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);
            var currentUser = new CurrentUser { tenant_id = 1, user_name = "alice" };

            var (id, msg) = await service.AddAsync(new WarehouseViewModel { warehouse_name = "Main WH" }, currentUser);

            id.ShouldBeGreaterThan(0);
            var saved = await scope.DbContext.GetDbSet<WarehouseEntity>().FindAsync(id);
            saved.ShouldNotBeNull();
            saved!.creator.ShouldBe("alice");
            saved.tenant_id.ShouldBe(1);
            saved.create_time.ShouldNotBe(default);
            saved.last_update_time.ShouldNotBe(default);
        }

        [Fact]
        public async Task AddAsync_DuplicateNameInSameTenant_IsRejected()
        {
            using var scope = new SqliteTestDbContextScope();
            var currentUser = new CurrentUser { tenant_id = 1 };
            scope.DbContext.GetDbSet<WarehouseEntity>().Add(new WarehouseEntity { warehouse_name = "Main WH", tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (id, _) = await service.AddAsync(new WarehouseViewModel { warehouse_name = "Main WH" }, currentUser);

            id.ShouldBe(0);
            (await scope.DbContext.GetDbSet<WarehouseEntity>().CountAsync()).ShouldBe(1);
        }

        [Fact]
        public async Task AddAsync_SameNameInDifferentTenant_IsAllowed()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<WarehouseEntity>().Add(new WarehouseEntity { warehouse_name = "Main WH", tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (id, _) = await service.AddAsync(new WarehouseViewModel { warehouse_name = "Main WH" }, new CurrentUser { tenant_id = 2 });

            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task UpdateAsync_UnknownId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.UpdateAsync(new WarehouseViewModel { id = 999 }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateAsync_PropagatesValidityToChildWarehouseareasAndGoodslocations()
        {
            using var scope = new SqliteTestDbContextScope();
            var warehouse = new WarehouseEntity { warehouse_name = "Main WH", tenant_id = 1, is_valid = true };
            scope.DbContext.GetDbSet<WarehouseEntity>().Add(warehouse);
            await scope.DbContext.SaveChangesAsync();

            var area = new WarehouseareaEntity { warehouse_id = warehouse.id, area_name = "A1", tenant_id = 1, is_valid = true };
            scope.DbContext.GetDbSet<WarehouseareaEntity>().Add(area);
            await scope.DbContext.SaveChangesAsync();

            var location = new GoodslocationEntity { warehouse_area_id = area.id, location_name = "L1", tenant_id = 1, is_valid = true };
            scope.DbContext.GetDbSet<GoodslocationEntity>().Add(location);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var viewModel = new WarehouseViewModel { id = warehouse.id, warehouse_name = "Main WH", is_valid = false };
            var (flag, _) = await service.UpdateAsync(viewModel, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<WarehouseareaEntity>().FindAsync(area.id))!.is_valid.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<GoodslocationEntity>().FindAsync(location.id))!.is_valid.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_WarehouseWithGoodslocations_IsBlocked()
        {
            using var scope = new SqliteTestDbContextScope();
            var warehouse = new WarehouseEntity { warehouse_name = "Main WH", tenant_id = 1 };
            scope.DbContext.GetDbSet<WarehouseEntity>().Add(warehouse);
            await scope.DbContext.SaveChangesAsync();
            scope.DbContext.GetDbSet<GoodslocationEntity>().Add(new GoodslocationEntity { warehouse_id = warehouse.id, location_name = "L1", tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(warehouse.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<WarehouseEntity>().FindAsync(warehouse.id)).ShouldNotBeNull();
        }

        [Fact]
        public async Task DeleteAsync_WarehouseWithoutGoodslocations_Succeeds()
        {
            using var scope = new SqliteTestDbContextScope();
            var warehouse = new WarehouseEntity { warehouse_name = "Main WH", tenant_id = 1 };
            scope.DbContext.GetDbSet<WarehouseEntity>().Add(warehouse);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(warehouse.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            // ExecuteDeleteAsync bypasses the change tracker, so FindAsync would return the
            // stale tracked instance from the earlier Add; query untracked to see the real DB state.
            (await scope.DbContext.GetDbSet<WarehouseEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == warehouse.id)).ShouldBeNull();
        }

        [Fact]
        public async Task GetAsync_BelongsToDifferentTenant_ReturnsNull()
        {
            using var scope = new SqliteTestDbContextScope();
            var warehouse = new WarehouseEntity { warehouse_name = "Tenant1-WH", tenant_id = 1 };
            scope.DbContext.GetDbSet<WarehouseEntity>().Add(warehouse);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetAsync(warehouse.id, new CurrentUser { tenant_id = 2 });

            result.ShouldBeNull();
        }

        [Fact]
        public async Task UpdateAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var warehouse = new WarehouseEntity { warehouse_name = "Tenant1-WH", tenant_id = 1 };
            scope.DbContext.GetDbSet<WarehouseEntity>().Add(warehouse);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(new WarehouseViewModel { id = warehouse.id, warehouse_name = "Hijacked" }, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<WarehouseEntity>().AsNoTracking().FirstAsync(t => t.id == warehouse.id)).warehouse_name.ShouldBe("Tenant1-WH");
        }

        [Fact]
        public async Task DeleteAsync_BelongsToDifferentTenant_DoesNotDelete()
        {
            using var scope = new SqliteTestDbContextScope();
            var warehouse = new WarehouseEntity { warehouse_name = "Tenant1-WH", tenant_id = 1 };
            scope.DbContext.GetDbSet<WarehouseEntity>().Add(warehouse);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(warehouse.id, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<WarehouseEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == warehouse.id)).ShouldNotBeNull();
        }
    }
}
