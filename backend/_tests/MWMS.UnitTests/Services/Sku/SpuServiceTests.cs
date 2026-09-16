using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.Sku
{
    /// <summary>
    /// Phase 0 harness smoke test: proves DB creation, service construction without the
    /// DI container, and assertions all work end-to-end before any real coverage is added.
    /// </summary>
    public class SpuServiceTests
    {
        private static SpuService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
            new(dbContext, new FakeStringLocalizer<MultiLanguage>());

        [Fact]
        public async Task PageAsync_EmptyDatabase_ReturnsEmptyResult()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (data, totals) = await service.PageAsync(new PageSearch(), new CurrentUser());

            data.ShouldBeEmpty();
            totals.ShouldBe(0);
        }

        [Fact]
        public async Task GetAsync_BelongsToDifferentTenant_ReturnsEmptyViewModel()
        {
            using var scope = new SqliteTestDbContextScope();
            var category = new CategoryEntity { category_name = "Cat1", tenant_id = 1 };
            scope.DbContext.GetDbSet<CategoryEntity>().Add(category);
            await scope.DbContext.SaveChangesAsync();
            var spu = new SpuEntity { spu_code = "SPU1", category_id = category.id, tenant_id = 1 };
            scope.DbContext.GetDbSet<SpuEntity>().Add(spu);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetAsync(spu.id, new CurrentUser { tenant_id = 2 });

            result.spu_code.ShouldBe(string.Empty);
        }

        [Fact]
        public async Task UpdateAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var spu = new SpuEntity { spu_code = "SPU1", tenant_id = 1 };
            scope.DbContext.GetDbSet<SpuEntity>().Add(spu);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(new SpuBothViewModel { id = spu.id, spu_code = "Hijacked" }, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_BelongsToDifferentTenant_DoesNotDelete()
        {
            using var scope = new SqliteTestDbContextScope();
            var spu = new SpuEntity { spu_code = "SPU1", tenant_id = 1 };
            scope.DbContext.GetDbSet<SpuEntity>().Add(spu);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(spu.id, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<SpuEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == spu.id)).ShouldNotBeNull();
        }
    }
}
