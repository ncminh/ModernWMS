using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.Company
{
    public class CompanyServiceTests
    {
        private static CompanyService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
            new(dbContext, new FakeStringLocalizer<MultiLanguage>());

        [Fact]
        public async Task GetAllAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<CompanyEntity>().AddRange(
                new CompanyEntity { company_name = "Tenant1-Co", tenant_id = 1 },
                new CompanyEntity { company_name = "Tenant2-Co", tenant_id = 2 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var data = await service.GetAllAsync(new CurrentUser { tenant_id = 1 });

            data.ShouldHaveSingleItem().company_name.ShouldBe("Tenant1-Co");
        }

        [Fact]
        public async Task GetAsync_UnknownId_ReturnsEmptyViewModelNotNull()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var result = await service.GetAsync(999, new CurrentUser { tenant_id = 1 });

            result.ShouldNotBeNull();
            result.company_name.ShouldBe(string.Empty);
        }

        [Fact]
        public async Task AddAsync_NewName_Persists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (id, _) = await service.AddAsync(new CompanyViewModel { company_name = "Company1" }, new CurrentUser { tenant_id = 1 });

            id.ShouldBeGreaterThan(0);
            var saved = await scope.DbContext.GetDbSet<CompanyEntity>().FindAsync(id);
            saved!.tenant_id.ShouldBe(1);
        }

        [Fact]
        public async Task AddAsync_DuplicateNameInSameTenant_IsRejected()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<CompanyEntity>().Add(new CompanyEntity { company_name = "Company1", tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (id, _) = await service.AddAsync(new CompanyViewModel { company_name = "Company1" }, new CurrentUser { tenant_id = 1 });

            id.ShouldBe(0);
        }

        [Fact]
        public async Task UpdateAsync_UnknownId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.UpdateAsync(new CompanyViewModel { id = 999 }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateAsync_ExistingCompany_UpdatesFields()
        {
            using var scope = new SqliteTestDbContextScope();
            var company = new CompanyEntity { company_name = "Company1", tenant_id = 1, city = "OldCity" };
            scope.DbContext.GetDbSet<CompanyEntity>().Add(company);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(new CompanyViewModel { id = company.id, company_name = "Company1", city = "NewCity" }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var updated = await scope.DbContext.GetDbSet<CompanyEntity>().FindAsync(company.id);
            updated!.city.ShouldBe("NewCity");
        }

        [Fact]
        public async Task DeleteAsync_ExistingRecord_Succeeds()
        {
            using var scope = new SqliteTestDbContextScope();
            var company = new CompanyEntity { company_name = "Company1", tenant_id = 1 };
            scope.DbContext.GetDbSet<CompanyEntity>().Add(company);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(company.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<CompanyEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == company.id)).ShouldBeNull();
        }

        [Fact]
        public async Task DeleteAsync_UnknownId_ReturnsFailure()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.DeleteAsync(999, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task GetAsync_BelongsToDifferentTenant_ReturnsEmptyViewModel()
        {
            using var scope = new SqliteTestDbContextScope();
            var company = new CompanyEntity { company_name = "Company1", tenant_id = 1 };
            scope.DbContext.GetDbSet<CompanyEntity>().Add(company);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetAsync(company.id, new CurrentUser { tenant_id = 2 });

            result.company_name.ShouldBe(string.Empty);
        }

        [Fact]
        public async Task UpdateAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var company = new CompanyEntity { company_name = "Company1", tenant_id = 1 };
            scope.DbContext.GetDbSet<CompanyEntity>().Add(company);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(new CompanyViewModel { id = company.id, company_name = "Hijacked" }, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_BelongsToDifferentTenant_DoesNotDelete()
        {
            using var scope = new SqliteTestDbContextScope();
            var company = new CompanyEntity { company_name = "Company1", tenant_id = 1 };
            scope.DbContext.GetDbSet<CompanyEntity>().Add(company);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(company.id, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<CompanyEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == company.id)).ShouldNotBeNull();
        }
    }
}
