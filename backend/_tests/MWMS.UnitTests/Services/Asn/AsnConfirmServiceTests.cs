using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.Asn
{
    public class AsnConfirmServiceTests
    {
        private static AsnConfirmService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
            new(dbContext, new FakeStringLocalizer<MultiLanguage>());

        private static async Task<AsnmasterEntity> SeedAsnmasterAsync(ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId)
        {
            var asnmaster = new AsnmasterEntity { asn_no = "ASNM1", tenant_id = tenantId };
            dbContext.GetDbSet<AsnmasterEntity>().Add(asnmaster);
            await dbContext.SaveChangesAsync();
            return asnmaster;
        }

        private static async Task<AsnEntity> SeedAsnAsync(ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, byte asnStatus, int? asnmasterId = null)
        {
            var masterId = asnmasterId ?? (await SeedAsnmasterAsync(dbContext, tenantId)).id;
            var asn = new AsnEntity { asnmaster_id = masterId, asn_no = "ASN1", asn_status = asnStatus, tenant_id = tenantId };
            dbContext.GetDbSet<AsnEntity>().Add(asn);
            await dbContext.SaveChangesAsync();
            return asn;
        }

        [Fact]
        public async Task ConfirmAsync_NoMatchingIds_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.ConfirmAsync(new List<AsnConfirmInputViewModel> { new() { id = 999 } }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task ConfirmAsync_NotPreDeliveryStatus_IsBlocked()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 1);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ConfirmAsync(new List<AsnConfirmInputViewModel> { new() { id = asn.id } }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task ConfirmAsync_MultipleRows_EachGetsItsOwnArrivalTime()
        {
            // Regression test for a fixed bug: entities.ForEach(t => { var vm =
            // viewModels.FirstOrDefault(t => t.id == t.id); ... }) shadowed the outer 't',
            // so the comparison always compared a variable to itself and 'vm' always
            // resolved to viewModels[0] - every row in a multi-select confirm batch used to
            // silently get the FIRST row's arrival_time. Two distinct rows with distinct
            // arrival_time values is what actually exercises this.
            using var scope = new SqliteTestDbContextScope();
            var asnmaster = await SeedAsnmasterAsync(scope.DbContext, 1);
            var asn1 = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 0, asnmasterId: asnmaster.id);
            var asn2 = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 0, asnmasterId: asnmaster.id);
            var time1 = new DateTime(2024, 1, 1);
            var time2 = new DateTime(2024, 2, 2);

            var service = CreateService(scope.DbContext);
            var viewModels = new List<AsnConfirmInputViewModel>
            {
                new() { id = asn1.id, arrival_time = time1 },
                new() { id = asn2.id, arrival_time = time2 },
            };
            var (flag, _) = await service.ConfirmAsync(viewModels, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<AsnEntity>().FindAsync(asn1.id))!.arrival_time.ShouldBe(time1);
            (await scope.DbContext.GetDbSet<AsnEntity>().FindAsync(asn2.id))!.arrival_time.ShouldBe(time2);
        }

        [Fact]
        public async Task ConfirmAsync_BelongsToDifferentTenant_ReturnsNotExistsAndDoesNotChangeStatus()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 0);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ConfirmAsync(new List<AsnConfirmInputViewModel> { new() { id = asn.id } }, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<AsnEntity>().AsNoTracking().FirstAsync(t => t.id == asn.id)).asn_status.ShouldBe((byte)0);
        }

        [Fact]
        public async Task ConfirmCancelAsync_NotConfirmedStatus_IsBlocked()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 0);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ConfirmCancelAsync(new List<int> { asn.id }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task ConfirmCancelAsync_Success_ResetsStatusAndArrivalTime()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 1);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ConfirmCancelAsync(new List<int> { asn.id }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var updated = await scope.DbContext.GetDbSet<AsnEntity>().FindAsync(asn.id);
            updated!.asn_status.ShouldBe((byte)0);
            updated.arrival_time.ShouldBe(ModernWMS.Core.Utility.UtilConvert.MinDate);
        }

        [Fact]
        public async Task ConfirmCancelAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 1);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ConfirmCancelAsync(new List<int> { asn.id }, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<AsnEntity>().AsNoTracking().FirstAsync(t => t.id == asn.id)).asn_status.ShouldBe((byte)1);
        }

        [Fact]
        public async Task UnloadAsync_NotPreLoadStatus_IsBlocked()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 2);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UnloadAsync(new List<AsnUnloadInputViewModel> { new() { id = asn.id } }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task UnloadAsync_MultipleRows_EachGetsItsOwnUnloadData()
        {
            // Same shadowing bug as ConfirmAsync, fixed the same way - see the comment there.
            using var scope = new SqliteTestDbContextScope();
            var asnmaster = await SeedAsnmasterAsync(scope.DbContext, 1);
            var asn1 = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 1, asnmasterId: asnmaster.id);
            var asn2 = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 1, asnmasterId: asnmaster.id);

            var service = CreateService(scope.DbContext);
            var viewModels = new List<AsnUnloadInputViewModel>
            {
                new() { id = asn1.id, unload_person_id = 11, unload_person = "Alice" },
                new() { id = asn2.id, unload_person_id = 22, unload_person = "Bob" },
            };
            var (flag, _) = await service.UnloadAsync(viewModels, new CurrentUser { tenant_id = 1, user_id = 1, user_name = "system" });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<AsnEntity>().FindAsync(asn1.id))!.unload_person.ShouldBe("Alice");
            (await scope.DbContext.GetDbSet<AsnEntity>().FindAsync(asn2.id))!.unload_person.ShouldBe("Bob");
        }

        [Fact]
        public async Task UnloadAsync_PersonIdZero_DefaultsToCurrentUser()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 1);

            var service = CreateService(scope.DbContext);
            var currentUser = new CurrentUser { tenant_id = 1, user_id = 42, user_name = "system-user" };
            var (flag, _) = await service.UnloadAsync(new List<AsnUnloadInputViewModel> { new() { id = asn.id, unload_person_id = 0 } }, currentUser);

            flag.ShouldBeTrue();
            var updated = await scope.DbContext.GetDbSet<AsnEntity>().FindAsync(asn.id);
            updated!.unload_person_id.ShouldBe(42);
            updated.unload_person.ShouldBe("system-user");
        }

        [Fact]
        public async Task UnloadAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 1);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UnloadAsync(new List<AsnUnloadInputViewModel> { new() { id = asn.id } }, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<AsnEntity>().AsNoTracking().FirstAsync(t => t.id == asn.id)).asn_status.ShouldBe((byte)1);
        }

        [Fact]
        public async Task UnloadCancelAsync_NotUnloadedStatus_IsBlocked()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 1);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UnloadCancelAsync(new List<int> { asn.id }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task UnloadCancelAsync_Success_ResetsFields()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 2);
            asn.unload_person_id = 7;
            asn.unload_person = "Alice";
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UnloadCancelAsync(new List<int> { asn.id }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var updated = await scope.DbContext.GetDbSet<AsnEntity>().FindAsync(asn.id);
            updated!.asn_status.ShouldBe((byte)1);
            updated.unload_person_id.ShouldBe(0);
            updated.unload_person.ShouldBe(string.Empty);
        }

        [Fact]
        public async Task UnloadCancelAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var asn = await SeedAsnAsync(scope.DbContext, 1, asnStatus: 2);

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UnloadCancelAsync(new List<int> { asn.id }, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<AsnEntity>().AsNoTracking().FirstAsync(t => t.id == asn.id)).asn_status.ShouldBe((byte)2);
        }
    }
}
