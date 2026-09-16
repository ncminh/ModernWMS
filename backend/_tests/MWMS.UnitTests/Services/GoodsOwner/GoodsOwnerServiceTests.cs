using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.GoodsOwner
{
    public class GoodsOwnerServiceTests
    {
        private static GoodsOwnerService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
            new(dbContext, new FakeStringLocalizer<MultiLanguage>());

        [Fact]
        public async Task PageAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<GoodsownerEntity>().AddRange(
                new GoodsownerEntity { goods_owner_name = "Tenant1-Owner", tenant_id = 1 },
                new GoodsownerEntity { goods_owner_name = "Tenant2-Owner", tenant_id = 2 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.PageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
            data.ShouldHaveSingleItem().goods_owner_name.ShouldBe("Tenant1-Owner");
        }

        [Fact]
        public async Task AddAsync_NewName_StampsAuditColumns()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);
            var currentUser = new CurrentUser { tenant_id = 1, user_name = "alice" };

            var (id, _) = await service.AddAsync(new GoodsownerViewModel { goods_owner_name = "Owner1" }, currentUser);

            id.ShouldBeGreaterThan(0);
            var saved = await scope.DbContext.GetDbSet<GoodsownerEntity>().FindAsync(id);
            saved!.creator.ShouldBe("alice");
            saved.tenant_id.ShouldBe(1);
        }

        [Fact]
        public async Task AddAsync_DuplicateNameInSameTenant_IsRejected()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<GoodsownerEntity>().Add(new GoodsownerEntity { goods_owner_name = "Owner1", tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (id, _) = await service.AddAsync(new GoodsownerViewModel { goods_owner_name = "Owner1" }, new CurrentUser { tenant_id = 1 });

            id.ShouldBe(0);
        }

        [Fact]
        public async Task UpdateAsync_UnknownId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.UpdateAsync(new GoodsownerViewModel { id = 999 }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateAsync_DuplicateNameWithinSameTenant_IsRejected()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<GoodsownerEntity>().Add(new GoodsownerEntity { goods_owner_name = "Owner1", tenant_id = 1 });
            var target = new GoodsownerEntity { goods_owner_name = "Owner2", tenant_id = 1 };
            scope.DbContext.GetDbSet<GoodsownerEntity>().Add(target);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(new GoodsownerViewModel { id = target.id, goods_owner_name = "Owner1" }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_ExistingRecord_Succeeds()
        {
            using var scope = new SqliteTestDbContextScope();
            var owner = new GoodsownerEntity { goods_owner_name = "Owner1", tenant_id = 1 };
            scope.DbContext.GetDbSet<GoodsownerEntity>().Add(owner);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(owner.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<GoodsownerEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == owner.id)).ShouldBeNull();
        }

        [Fact]
        public async Task ExcelAsync_DuplicateAgainstExistingRow_ReportsErrorWithoutInsertingAnyRow()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<GoodsownerEntity>().Add(new GoodsownerEntity { goods_owner_name = "Owner1", tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var input = new List<GoodsownerImportViewModel>
            {
                new() { goods_owner_name = "Owner1" },
                new() { goods_owner_name = "Owner2" },
            };

            var (flag, errorData) = await service.ExcelAsync(input, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            errorData.ShouldHaveSingleItem().goods_owner_name.ShouldBe("Owner1");
            (await scope.DbContext.GetDbSet<GoodsownerEntity>().CountAsync()).ShouldBe(1);
        }

        [Fact]
        public async Task ExcelAsync_AllNew_InsertsAllRows()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);
            var input = new List<GoodsownerImportViewModel>
            {
                new() { goods_owner_name = "Owner1" },
                new() { goods_owner_name = "Owner2" },
            };

            var (flag, errorData) = await service.ExcelAsync(input, new CurrentUser { tenant_id = 1, user_name = "alice" });

            flag.ShouldBeTrue();
            errorData.ShouldBeEmpty();
            (await scope.DbContext.GetDbSet<GoodsownerEntity>().CountAsync()).ShouldBe(2);
        }

        [Fact]
        public async Task GetAsync_BelongsToDifferentTenant_ReturnsEmptyViewModel()
        {
            using var scope = new SqliteTestDbContextScope();
            var owner = new GoodsownerEntity { goods_owner_name = "Owner1", tenant_id = 1 };
            scope.DbContext.GetDbSet<GoodsownerEntity>().Add(owner);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetAsync(owner.id, new CurrentUser { tenant_id = 2 });

            result.goods_owner_name.ShouldBe(string.Empty);
        }

        [Fact]
        public async Task UpdateAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var owner = new GoodsownerEntity { goods_owner_name = "Owner1", tenant_id = 1 };
            scope.DbContext.GetDbSet<GoodsownerEntity>().Add(owner);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(new GoodsownerViewModel { id = owner.id, goods_owner_name = "Hijacked" }, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_BelongsToDifferentTenant_DoesNotDelete()
        {
            using var scope = new SqliteTestDbContextScope();
            var owner = new GoodsownerEntity { goods_owner_name = "Owner1", tenant_id = 1 };
            scope.DbContext.GetDbSet<GoodsownerEntity>().Add(owner);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(owner.id, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<GoodsownerEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == owner.id)).ShouldNotBeNull();
        }
    }
}
