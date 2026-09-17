using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.WarehouseArea
{
    public class WarehouseAreaServiceTests
    {
        private static WarehouseAreaService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
            new(dbContext, new FakeStringLocalizer<MultiLanguage>());

        private static async Task<WarehouseEntity> SeedWarehouseAsync(ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId)
        {
            var warehouse = new WarehouseEntity { warehouse_name = "WH", tenant_id = tenantId };
            dbContext.GetDbSet<WarehouseEntity>().Add(warehouse);
            await dbContext.SaveChangesAsync();
            return warehouse;
        }

        [Fact]
        public async Task PageAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            var wh1 = await SeedWarehouseAsync(scope.DbContext, 1);
            var wh2 = await SeedWarehouseAsync(scope.DbContext, 2);
            scope.DbContext.GetDbSet<WarehouseAreaEntity>().AddRange(
                new WarehouseAreaEntity { warehouse_id = wh1.id, area_name = "Tenant1-Area", tenant_id = 1 },
                new WarehouseAreaEntity { warehouse_id = wh2.id, area_name = "Tenant2-Area", tenant_id = 2 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.PageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
            data.ShouldHaveSingleItem().area_name.ShouldBe("Tenant1-Area");
        }

        [Fact]
        public async Task AddAsync_NewAreaInWarehouse_Persists()
        {
            using var scope = new SqliteTestDbContextScope();
            var warehouse = await SeedWarehouseAsync(scope.DbContext, 1);
            var service = CreateService(scope.DbContext);

            var (id, _) = await service.AddAsync(
                new WarehouseAreaViewModel { warehouse_id = warehouse.id, area_name = "A1" },
                new CurrentUser { tenant_id = 1 });

            id.ShouldBeGreaterThan(0);
            (await scope.DbContext.GetDbSet<WarehouseAreaEntity>().FindAsync(id))!.tenant_id.ShouldBe(1);
        }

        [Fact]
        public async Task AddAsync_DuplicateAreaNameInSameWarehouse_IsRejected()
        {
            using var scope = new SqliteTestDbContextScope();
            var warehouse = await SeedWarehouseAsync(scope.DbContext, 1);
            scope.DbContext.GetDbSet<WarehouseAreaEntity>().Add(new WarehouseAreaEntity { warehouse_id = warehouse.id, area_name = "A1", tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (id, _) = await service.AddAsync(
                new WarehouseAreaViewModel { warehouse_id = warehouse.id, area_name = "A1" },
                new CurrentUser { tenant_id = 1 });

            id.ShouldBe(0);
        }

        [Fact]
        public async Task AddAsync_SameAreaNameInDifferentWarehouse_IsAllowed()
        {
            using var scope = new SqliteTestDbContextScope();
            var warehouse1 = await SeedWarehouseAsync(scope.DbContext, 1);
            var warehouse2 = await SeedWarehouseAsync(scope.DbContext, 1);
            scope.DbContext.GetDbSet<WarehouseAreaEntity>().Add(new WarehouseAreaEntity { warehouse_id = warehouse1.id, area_name = "A1", tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (id, _) = await service.AddAsync(
                new WarehouseAreaViewModel { warehouse_id = warehouse2.id, area_name = "A1" },
                new CurrentUser { tenant_id = 1 });

            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task UpdateAsync_PropagatesToChildGoodslocations()
        {
            using var scope = new SqliteTestDbContextScope();
            var warehouse = await SeedWarehouseAsync(scope.DbContext, 1);
            var area = new WarehouseAreaEntity { warehouse_id = warehouse.id, area_name = "A1", tenant_id = 1, is_valid = true, area_property = 1 };
            scope.DbContext.GetDbSet<WarehouseAreaEntity>().Add(area);
            await scope.DbContext.SaveChangesAsync();
            var location = new GoodsLocationEntity { warehouse_area_id = area.id, location_name = "L1", tenant_id = 1, is_valid = true };
            scope.DbContext.GetDbSet<GoodsLocationEntity>().Add(location);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var viewModel = new WarehouseAreaViewModel { id = area.id, warehouse_id = warehouse.id, area_name = "A1-renamed", is_valid = false, area_property = 2 };
            var (flag, _) = await service.UpdateAsync(viewModel, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var updatedLocation = await scope.DbContext.GetDbSet<GoodsLocationEntity>().FindAsync(location.id);
            updatedLocation!.warehouse_area_name.ShouldBe("A1-renamed");
            updatedLocation.warehouse_area_property.ShouldBe((byte)2);
            updatedLocation.is_valid.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateAsync_UnknownId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.UpdateAsync(new WarehouseAreaViewModel { id = 999 }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_AreaWithGoodslocations_IsBlocked()
        {
            using var scope = new SqliteTestDbContextScope();
            var warehouse = await SeedWarehouseAsync(scope.DbContext, 1);
            var area = new WarehouseAreaEntity { warehouse_id = warehouse.id, area_name = "A1", tenant_id = 1 };
            scope.DbContext.GetDbSet<WarehouseAreaEntity>().Add(area);
            await scope.DbContext.SaveChangesAsync();
            scope.DbContext.GetDbSet<GoodsLocationEntity>().Add(new GoodsLocationEntity { warehouse_area_id = area.id, location_name = "L1", tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(area.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_AreaWithoutGoodslocations_Succeeds()
        {
            using var scope = new SqliteTestDbContextScope();
            var warehouse = await SeedWarehouseAsync(scope.DbContext, 1);
            var area = new WarehouseAreaEntity { warehouse_id = warehouse.id, area_name = "A1", tenant_id = 1 };
            scope.DbContext.GetDbSet<WarehouseAreaEntity>().Add(area);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(area.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<WarehouseAreaEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == area.id)).ShouldBeNull();
        }

        [Fact]
        public async Task GetAsync_BelongsToDifferentTenant_ReturnsNull()
        {
            using var scope = new SqliteTestDbContextScope();
            var warehouse = await SeedWarehouseAsync(scope.DbContext, 1);
            var area = new WarehouseAreaEntity { warehouse_id = warehouse.id, area_name = "A1", tenant_id = 1 };
            scope.DbContext.GetDbSet<WarehouseAreaEntity>().Add(area);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetAsync(area.id, new CurrentUser { tenant_id = 2 });

            result.ShouldBeNull();
        }

        [Fact]
        public async Task UpdateAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var warehouse = await SeedWarehouseAsync(scope.DbContext, 1);
            var area = new WarehouseAreaEntity { warehouse_id = warehouse.id, area_name = "A1", tenant_id = 1 };
            scope.DbContext.GetDbSet<WarehouseAreaEntity>().Add(area);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(new WarehouseAreaViewModel { id = area.id, warehouse_id = warehouse.id, area_name = "Hijacked" }, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_BelongsToDifferentTenant_DoesNotDelete()
        {
            using var scope = new SqliteTestDbContextScope();
            var warehouse = await SeedWarehouseAsync(scope.DbContext, 1);
            var area = new WarehouseAreaEntity { warehouse_id = warehouse.id, area_name = "A1", tenant_id = 1 };
            scope.DbContext.GetDbSet<WarehouseAreaEntity>().Add(area);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(area.id, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<WarehouseAreaEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == area.id)).ShouldNotBeNull();
        }
    }
}
