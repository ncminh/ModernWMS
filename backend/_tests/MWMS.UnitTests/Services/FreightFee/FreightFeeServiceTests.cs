using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.FreightFee
{
    public class FreightFeeServiceTests
    {
        private static FreightFeeService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
            new(dbContext, new FakeStringLocalizer<MultiLanguage>());

        [Fact]
        public async Task PageAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<FreightFeeEntity>().AddRange(
                new FreightFeeEntity { carrier = "Tenant1-Carrier", tenant_id = 1 },
                new FreightFeeEntity { carrier = "Tenant2-Carrier", tenant_id = 2 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.PageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
            data.ShouldHaveSingleItem().carrier.ShouldBe("Tenant1-Carrier");
        }

        [Fact]
        public async Task AddAsync_NewRecord_StampsAuditColumns()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);
            var currentUser = new CurrentUser { tenant_id = 1, user_name = "alice" };

            var (id, _) = await service.AddAsync(new FreightFeeViewModel { carrier = "Carrier1" }, currentUser);

            id.ShouldBeGreaterThan(0);
            var saved = await scope.DbContext.GetDbSet<FreightFeeEntity>().FindAsync(id);
            saved!.creator.ShouldBe("alice");
            saved.tenant_id.ShouldBe(1);
        }

        [Fact]
        public async Task UpdateAsync_UnknownId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.UpdateAsync(new FreightFeeViewModel { id = 999 }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateAsync_ExistingRecord_UpdatesFields()
        {
            using var scope = new SqliteTestDbContextScope();
            var freightfee = new FreightFeeEntity { carrier = "Carrier1", tenant_id = 1, price_per_weight = 1 };
            scope.DbContext.GetDbSet<FreightFeeEntity>().Add(freightfee);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(new FreightFeeViewModel { id = freightfee.id, carrier = "Carrier1", price_per_weight = 5 }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<FreightFeeEntity>().FindAsync(freightfee.id))!.price_per_weight.ShouldBe(5);
        }

        [Fact]
        public async Task DeleteAsync_ExistingRecord_Succeeds()
        {
            using var scope = new SqliteTestDbContextScope();
            var freightfee = new FreightFeeEntity { carrier = "Carrier1", tenant_id = 1 };
            scope.DbContext.GetDbSet<FreightFeeEntity>().Add(freightfee);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(freightfee.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<FreightFeeEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == freightfee.id)).ShouldBeNull();
        }

        [Fact]
        public async Task ExcelAsync_InsertsAllRows()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);
            var datas = new List<FreightFeeExcelmportViewModel>
            {
                new() { carrier = "Carrier1" },
                new() { carrier = "Carrier2" },
            };

            var (flag, _) = await service.ExcelAsync(datas, new CurrentUser { tenant_id = 1, user_name = "alice" });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<FreightFeeEntity>().CountAsync()).ShouldBe(2);
        }

        [Fact]
        public async Task GetAsync_BelongsToDifferentTenant_ReturnsNull()
        {
            using var scope = new SqliteTestDbContextScope();
            var freightfee = new FreightFeeEntity { carrier = "Carrier1", tenant_id = 1 };
            scope.DbContext.GetDbSet<FreightFeeEntity>().Add(freightfee);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetAsync(freightfee.id, new CurrentUser { tenant_id = 2 });

            result.ShouldBeNull();
        }

        [Fact]
        public async Task UpdateAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var freightfee = new FreightFeeEntity { carrier = "Carrier1", tenant_id = 1 };
            scope.DbContext.GetDbSet<FreightFeeEntity>().Add(freightfee);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(new FreightFeeViewModel { id = freightfee.id, carrier = "Hijacked" }, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_BelongsToDifferentTenant_DoesNotDelete()
        {
            using var scope = new SqliteTestDbContextScope();
            var freightfee = new FreightFeeEntity { carrier = "Carrier1", tenant_id = 1 };
            scope.DbContext.GetDbSet<FreightFeeEntity>().Add(freightfee);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(freightfee.id, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<FreightFeeEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == freightfee.id)).ShouldNotBeNull();
        }
    }
}
