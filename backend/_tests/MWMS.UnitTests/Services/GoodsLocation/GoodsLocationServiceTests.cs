using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.GoodsLocation
{
    public class GoodsLocationServiceTests
    {
        private static GoodsLocationService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
            new(dbContext, new FakeStringLocalizer<MultiLanguage>());

        [Fact]
        public async Task PageAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<GoodslocationEntity>().AddRange(
                new GoodslocationEntity { location_name = "Tenant1-Loc", tenant_id = 1 },
                new GoodslocationEntity { location_name = "Tenant2-Loc", tenant_id = 2 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.PageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
            data.ShouldHaveSingleItem().location_name.ShouldBe("Tenant1-Loc");
        }

        [Fact]
        public async Task AddAsync_NewLocation_Persists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (id, _) = await service.AddAsync(new GoodslocationViewModel { location_name = "L1" }, new CurrentUser { tenant_id = 1 });

            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task AddAsync_DuplicateLocationNameInSameTenant_IsRejected()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<GoodslocationEntity>().Add(new GoodslocationEntity { location_name = "L1", tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (id, _) = await service.AddAsync(new GoodslocationViewModel { location_name = "L1" }, new CurrentUser { tenant_id = 1 });

            id.ShouldBe(0);
        }

        [Fact]
        public async Task UpdateAsync_UnknownId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.UpdateAsync(new GoodslocationViewModel { id = 999 }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateAsync_ExistingLocation_UpdatesFields()
        {
            using var scope = new SqliteTestDbContextScope();
            var location = new GoodslocationEntity { location_name = "L1", tenant_id = 1, location_length = 1 };
            scope.DbContext.GetDbSet<GoodslocationEntity>().Add(location);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var viewModel = new GoodslocationViewModel { id = location.id, location_name = "L1-renamed", location_length = 5 };
            var (flag, _) = await service.UpdateAsync(viewModel, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var updated = await scope.DbContext.GetDbSet<GoodslocationEntity>().FindAsync(location.id);
            updated!.location_name.ShouldBe("L1-renamed");
            updated.location_length.ShouldBe(5);
        }

        [Fact]
        public async Task DeleteAsync_LocationWithPositiveStock_IsBlocked()
        {
            using var scope = new SqliteTestDbContextScope();
            var location = new GoodslocationEntity { location_name = "L1", tenant_id = 1 };
            scope.DbContext.GetDbSet<GoodslocationEntity>().Add(location);
            await scope.DbContext.SaveChangesAsync();
            scope.DbContext.GetDbSet<StockEntity>().Add(new StockEntity { goods_location_id = location.id, qty = 10, tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(location.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_LocationWithZeroStock_Succeeds()
        {
            using var scope = new SqliteTestDbContextScope();
            var location = new GoodslocationEntity { location_name = "L1", tenant_id = 1 };
            scope.DbContext.GetDbSet<GoodslocationEntity>().Add(location);
            await scope.DbContext.SaveChangesAsync();
            scope.DbContext.GetDbSet<StockEntity>().Add(new StockEntity { goods_location_id = location.id, qty = 0, tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(location.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<GoodslocationEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == location.id)).ShouldBeNull();
        }

        [Fact]
        public async Task GetAsync_BelongsToDifferentTenant_ReturnsNull()
        {
            using var scope = new SqliteTestDbContextScope();
            var location = new GoodslocationEntity { location_name = "L1", tenant_id = 1 };
            scope.DbContext.GetDbSet<GoodslocationEntity>().Add(location);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetAsync(location.id, new CurrentUser { tenant_id = 2 });

            result.ShouldBeNull();
        }

        [Fact]
        public async Task UpdateAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var location = new GoodslocationEntity { location_name = "L1", tenant_id = 1 };
            scope.DbContext.GetDbSet<GoodslocationEntity>().Add(location);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(new GoodslocationViewModel { id = location.id, location_name = "Hijacked" }, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_BelongsToDifferentTenant_DoesNotDelete()
        {
            using var scope = new SqliteTestDbContextScope();
            var location = new GoodslocationEntity { location_name = "L1", tenant_id = 1 };
            scope.DbContext.GetDbSet<GoodslocationEntity>().Add(location);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(location.id, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<GoodslocationEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == location.id)).ShouldNotBeNull();
        }
    }
}
