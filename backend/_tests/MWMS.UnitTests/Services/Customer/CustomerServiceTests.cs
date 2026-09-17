using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.Customer
{
    public class CustomerServiceTests
    {
        private static CustomerService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
            new(dbContext, new FakeStringLocalizer<MultiLanguage>());

        [Fact]
        public async Task PageAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<CustomerEntity>().AddRange(
                new CustomerEntity { customer_name = "Tenant1-Customer", tenant_id = 1 },
                new CustomerEntity { customer_name = "Tenant2-Customer", tenant_id = 2 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.PageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
            data.ShouldHaveSingleItem().customer_name.ShouldBe("Tenant1-Customer");
        }

        [Fact]
        public async Task AddAsync_NewName_StampsAuditColumns()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);
            var currentUser = new CurrentUser { tenant_id = 1, user_name = "alice" };

            var (id, _) = await service.AddAsync(new CustomerViewModel { customer_name = "Customer1" }, currentUser);

            id.ShouldBeGreaterThan(0);
            var saved = await scope.DbContext.GetDbSet<CustomerEntity>().FindAsync(id);
            saved!.creator.ShouldBe("alice");
            saved.tenant_id.ShouldBe(1);
        }

        [Fact]
        public async Task AddAsync_DuplicateNameInSameTenant_IsRejected()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<CustomerEntity>().Add(new CustomerEntity { customer_name = "Customer1", tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (id, _) = await service.AddAsync(new CustomerViewModel { customer_name = "Customer1" }, new CurrentUser { tenant_id = 1 });

            id.ShouldBe(0);
        }

        [Fact]
        public async Task UpdateAsync_UnknownId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.UpdateAsync(new CustomerViewModel { id = 999 }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_CustomerReferencedByDispatchlist_IsBlocked()
        {
            using var scope = new SqliteTestDbContextScope();
            var customer = new CustomerEntity { customer_name = "Customer1", tenant_id = 1 };
            scope.DbContext.GetDbSet<CustomerEntity>().Add(customer);
            await scope.DbContext.SaveChangesAsync();
            scope.DbContext.GetDbSet<DispatchListEntity>().Add(new DispatchListEntity { customer_id = customer.id });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(customer.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_CustomerWithoutDispatchlists_Succeeds()
        {
            using var scope = new SqliteTestDbContextScope();
            var customer = new CustomerEntity { customer_name = "Customer1", tenant_id = 1 };
            scope.DbContext.GetDbSet<CustomerEntity>().Add(customer);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(customer.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<CustomerEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == customer.id)).ShouldBeNull();
        }

        [Fact]
        public async Task ExcelAsync_DuplicateAgainstExistingRow_ReportsErrorWithoutInsertingAnyRow()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<CustomerEntity>().Add(new CustomerEntity { customer_name = "Customer1", tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var input = new List<CustomerImportViewModel>
            {
                new() { customer_name = "Customer1" },
                new() { customer_name = "Customer2" },
            };

            var (flag, errorData) = await service.ExcelAsync(input, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            errorData.ShouldHaveSingleItem().customer_name.ShouldBe("Customer1");
            (await scope.DbContext.GetDbSet<CustomerEntity>().CountAsync()).ShouldBe(1);
        }

        [Fact]
        public async Task GetAsync_BelongsToDifferentTenant_ReturnsEmptyViewModel()
        {
            using var scope = new SqliteTestDbContextScope();
            var customer = new CustomerEntity { customer_name = "Customer1", tenant_id = 1 };
            scope.DbContext.GetDbSet<CustomerEntity>().Add(customer);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetAsync(customer.id, new CurrentUser { tenant_id = 2 });

            result.customer_name.ShouldBe(string.Empty);
        }

        [Fact]
        public async Task UpdateAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var customer = new CustomerEntity { customer_name = "Customer1", tenant_id = 1 };
            scope.DbContext.GetDbSet<CustomerEntity>().Add(customer);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(new CustomerViewModel { id = customer.id, customer_name = "Hijacked" }, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_BelongsToDifferentTenant_DoesNotDelete()
        {
            using var scope = new SqliteTestDbContextScope();
            var customer = new CustomerEntity { customer_name = "Customer1", tenant_id = 1 };
            scope.DbContext.GetDbSet<CustomerEntity>().Add(customer);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(customer.id, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<CustomerEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == customer.id)).ShouldNotBeNull();
        }
    }
}
