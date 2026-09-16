using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.Supplier
{
    public class SupplierServiceTests
    {
        private static SupplierService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
            new(dbContext, new FakeStringLocalizer<MultiLanguage>());

        [Fact]
        public async Task PageAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<SupplierEntity>().AddRange(
                new SupplierEntity { supplier_name = "Tenant1-Supplier", tenant_id = 1 },
                new SupplierEntity { supplier_name = "Tenant2-Supplier", tenant_id = 2 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.PageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
            data.ShouldHaveSingleItem().supplier_name.ShouldBe("Tenant1-Supplier");
        }

        [Fact]
        public async Task AddAsync_NewName_StampsAuditColumns()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);
            var currentUser = new CurrentUser { tenant_id = 1, user_name = "alice" };

            var (id, _) = await service.AddAsync(new SupplierViewModel { supplier_name = "Supplier1" }, currentUser);

            id.ShouldBeGreaterThan(0);
            var saved = await scope.DbContext.GetDbSet<SupplierEntity>().FindAsync(id);
            saved!.creator.ShouldBe("alice");
            saved.tenant_id.ShouldBe(1);
        }

        [Fact]
        public async Task AddAsync_DuplicateNameInSameTenant_IsRejectedWithoutPersisting()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<SupplierEntity>().Add(new SupplierEntity { supplier_name = "Supplier1", tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (id, _) = await service.AddAsync(new SupplierViewModel { supplier_name = "Supplier1" }, new CurrentUser { tenant_id = 1 });

            id.ShouldBe(0);
            // AddAsync's duplicate check runs after the new entity is already staged in the
            // change tracker (see SupplierService.AddAsync); confirm the rejected call still
            // doesn't leave anything actually committed.
            (await scope.DbContext.GetDbSet<SupplierEntity>().CountAsync()).ShouldBe(1);
        }

        [Fact]
        public async Task UpdateAsync_UnknownId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.UpdateAsync(new SupplierViewModel { id = 999 }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateAsync_DuplicateNameWithinSameTenant_IsRejected()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<SupplierEntity>().Add(new SupplierEntity { supplier_name = "Supplier1", tenant_id = 1 });
            var target = new SupplierEntity { supplier_name = "Supplier2", tenant_id = 1 };
            scope.DbContext.GetDbSet<SupplierEntity>().Add(target);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(new SupplierViewModel { id = target.id, supplier_name = "Supplier1" }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_ExistingRecord_Succeeds()
        {
            using var scope = new SqliteTestDbContextScope();
            var supplier = new SupplierEntity { supplier_name = "Supplier1", tenant_id = 1 };
            scope.DbContext.GetDbSet<SupplierEntity>().Add(supplier);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(supplier.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<SupplierEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == supplier.id)).ShouldBeNull();
        }

        [Fact]
        public async Task ExcelAsync_DuplicateWithinExcelBatch_IsRejected()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);
            var datas = new List<SupplierExcelImportViewModel>
            {
                new() { supplier_name = "Supplier1" },
                new() { supplier_name = "Supplier1" },
            };

            var (flag, _) = await service.ExcelAsync(datas, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<SupplierEntity>().CountAsync()).ShouldBe(0);
        }

        [Fact]
        public async Task ExcelAsync_AllNew_InsertsAllRows()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);
            var datas = new List<SupplierExcelImportViewModel>
            {
                new() { supplier_name = "Supplier1" },
                new() { supplier_name = "Supplier2" },
            };

            var (flag, _) = await service.ExcelAsync(datas, new CurrentUser { tenant_id = 1, user_name = "alice" });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<SupplierEntity>().CountAsync()).ShouldBe(2);
        }

        [Fact]
        public async Task GetAsync_BelongsToDifferentTenant_ReturnsNull()
        {
            using var scope = new SqliteTestDbContextScope();
            var supplier = new SupplierEntity { supplier_name = "Supplier1", tenant_id = 1 };
            scope.DbContext.GetDbSet<SupplierEntity>().Add(supplier);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetAsync(supplier.id, new CurrentUser { tenant_id = 2 });

            result.ShouldBeNull();
        }

        [Fact]
        public async Task UpdateAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var supplier = new SupplierEntity { supplier_name = "Supplier1", tenant_id = 1 };
            scope.DbContext.GetDbSet<SupplierEntity>().Add(supplier);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(new SupplierViewModel { id = supplier.id, supplier_name = "Hijacked" }, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_BelongsToDifferentTenant_DoesNotDelete()
        {
            using var scope = new SqliteTestDbContextScope();
            var supplier = new SupplierEntity { supplier_name = "Supplier1", tenant_id = 1 };
            scope.DbContext.GetDbSet<SupplierEntity>().Add(supplier);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(supplier.id, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<SupplierEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == supplier.id)).ShouldNotBeNull();
        }
    }
}
