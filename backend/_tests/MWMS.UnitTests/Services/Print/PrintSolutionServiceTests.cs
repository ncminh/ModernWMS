using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.Print
{
    public class PrintSolutionServiceTests
    {
        private static PrintSolutionService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
            new(dbContext, new FakeStringLocalizer<MultiLanguage>());

        [Fact]
        public async Task PageAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<PrintSolutionEntity>().AddRange(
                new PrintSolutionEntity { solution_name = "Tenant1-Sol", tenant_id = 1, config_json = "{}" },
                new PrintSolutionEntity { solution_name = "Tenant2-Sol", tenant_id = 2, config_json = "{}" });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.PageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
            data.ShouldHaveSingleItem().solution_name.ShouldBe("Tenant1-Sol");
        }

        [Fact]
        public async Task GetByPathAsync_OnlyReturnsRowsForCurrentTenantAndPath()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<PrintSolutionEntity>().AddRange(
                new PrintSolutionEntity { solution_name = "MatchingSol", tenant_id = 1, vue_path = "/asn", tab_page = "list", config_json = "{}" },
                new PrintSolutionEntity { solution_name = "OtherPath", tenant_id = 1, vue_path = "/dispatch", tab_page = "list", config_json = "{}" },
                new PrintSolutionEntity { solution_name = "OtherTenant", tenant_id = 2, vue_path = "/asn", tab_page = "list", config_json = "{}" });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetByPathAsync(new PrintSolutionGetByPathInputViewModel { vue_path = "/asn", tab_page = "list" }, new CurrentUser { tenant_id = 1 });

            result.ShouldHaveSingleItem().solution_name.ShouldBe("MatchingSol");
        }

        [Fact]
        public async Task AddAsync_NewRecord_Persists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (id, _) = await service.AddAsync(new PrintSolutionViewModel { solution_name = "Sol1", config_json = "{}" }, new CurrentUser { tenant_id = 1 });

            id.ShouldBeGreaterThan(0);
            (await scope.DbContext.GetDbSet<PrintSolutionEntity>().FindAsync(id))!.tenant_id.ShouldBe(1);
        }

        [Fact]
        public async Task UpdateAsync_UnknownId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.UpdateAsync(new PrintSolutionViewModel { id = 999, config_json = "{}" }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateAsync_ExistingRecord_UpdatesFields()
        {
            using var scope = new SqliteTestDbContextScope();
            var solution = new PrintSolutionEntity { solution_name = "Sol1", tenant_id = 1, config_json = "{}" };
            scope.DbContext.GetDbSet<PrintSolutionEntity>().Add(solution);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(new PrintSolutionViewModel { id = solution.id, solution_name = "Sol1-renamed", config_json = "{}" }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<PrintSolutionEntity>().FindAsync(solution.id))!.solution_name.ShouldBe("Sol1-renamed");
        }

        [Fact]
        public async Task DeleteAsync_ExistingRecord_Succeeds()
        {
            using var scope = new SqliteTestDbContextScope();
            var solution = new PrintSolutionEntity { solution_name = "Sol1", tenant_id = 1, config_json = "{}" };
            scope.DbContext.GetDbSet<PrintSolutionEntity>().Add(solution);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(solution.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<PrintSolutionEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == solution.id)).ShouldBeNull();
        }

        [Fact]
        public async Task GetAsync_BelongsToDifferentTenant_ReturnsNull()
        {
            using var scope = new SqliteTestDbContextScope();
            var solution = new PrintSolutionEntity { solution_name = "Sol1", tenant_id = 1, config_json = "{}" };
            scope.DbContext.GetDbSet<PrintSolutionEntity>().Add(solution);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetAsync(solution.id, new CurrentUser { tenant_id = 2 });

            result.ShouldBeNull();
        }

        [Fact]
        public async Task UpdateAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var solution = new PrintSolutionEntity { solution_name = "Sol1", tenant_id = 1, config_json = "{}" };
            scope.DbContext.GetDbSet<PrintSolutionEntity>().Add(solution);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(new PrintSolutionViewModel { id = solution.id, solution_name = "Hijacked", config_json = "{}" }, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_BelongsToDifferentTenant_DoesNotDelete()
        {
            using var scope = new SqliteTestDbContextScope();
            var solution = new PrintSolutionEntity { solution_name = "Sol1", tenant_id = 1, config_json = "{}" };
            scope.DbContext.GetDbSet<PrintSolutionEntity>().Add(solution);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(solution.id, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<PrintSolutionEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == solution.id)).ShouldNotBeNull();
        }
    }
}
