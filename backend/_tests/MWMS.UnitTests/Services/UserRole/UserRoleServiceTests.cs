using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.UserRole
{
    public class UserRoleServiceTests
    {
        private static UserRoleService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
            new(dbContext, new FakeStringLocalizer<MultiLanguage>());

        [Fact]
        public async Task GetAllAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<UserroleEntity>().AddRange(
                new UserroleEntity { role_name = "Tenant1-Role", tenant_id = 1 },
                new UserroleEntity { role_name = "Tenant2-Role", tenant_id = 2 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var data = await service.GetAllAsync(new CurrentUser { tenant_id = 1 });

            data.ShouldHaveSingleItem().role_name.ShouldBe("Tenant1-Role");
        }

        [Fact]
        public async Task AddAsync_NewRoleName_Persists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (id, _) = await service.AddAsync(new UserRoleViewModel { role_name = "Role1" }, new CurrentUser { tenant_id = 1 });

            id.ShouldBeGreaterThan(0);
            (await scope.DbContext.GetDbSet<UserroleEntity>().FindAsync(id))!.tenant_id.ShouldBe(1);
        }

        [Fact]
        public async Task AddAsync_DuplicateRoleNameInSameTenant_IsRejected()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<UserroleEntity>().Add(new UserroleEntity { role_name = "Role1", tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (id, _) = await service.AddAsync(new UserRoleViewModel { role_name = "Role1" }, new CurrentUser { tenant_id = 1 });

            id.ShouldBe(0);
        }

        [Fact]
        public async Task UpdateAsync_UnknownId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.UpdateAsync(new UserRoleViewModel { id = 999 }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateAsync_RenamingRole_PropagatesToUsersWithThatRole()
        {
            using var scope = new SqliteTestDbContextScope();
            var role = new ModernWMS.Core.Models.UserroleEntity { role_name = "OldName", tenant_id = 1 };
            scope.DbContext.GetDbSet<UserroleEntity>().Add(role);
            await scope.DbContext.SaveChangesAsync();
            var user = new ModernWMS.Core.Models.UserEntity { UserNum = "U1", TenantId = 1, UserRole = "OldName" };
            scope.DbContext.GetDbSet<ModernWMS.Core.Models.UserEntity>().Add(user);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(new UserRoleViewModel { id = role.id, role_name = "NewName" }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<ModernWMS.Core.Models.UserEntity>().FindAsync(user.id))!.UserRole.ShouldBe("NewName");
        }

        [Fact]
        public async Task DeleteAsync_ExistingRole_Succeeds()
        {
            using var scope = new SqliteTestDbContextScope();
            var role = new UserroleEntity { role_name = "Role1", tenant_id = 1 };
            scope.DbContext.GetDbSet<UserroleEntity>().Add(role);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(role.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<UserroleEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == role.id)).ShouldBeNull();
        }

        [Fact]
        public async Task BulkSaveAsync_DuplicateNameWithinPayload_IsRejected()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);
            var viewModels = new List<UserRoleViewModel>
            {
                new() { id = 0, role_name = "Role1" },
                new() { id = 0, role_name = "Role1" },
            };

            var (flag, _) = await service.BulkSaveAsync(viewModels, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<UserroleEntity>().CountAsync()).ShouldBe(0);
        }

        [Fact]
        public async Task BulkSaveAsync_AddsUpdatesAndDeletesInOneCall()
        {
            using var scope = new SqliteTestDbContextScope();
            var toUpdate = new UserroleEntity { role_name = "ToUpdate", tenant_id = 1 };
            var toDelete = new UserroleEntity { role_name = "ToDelete", tenant_id = 1 };
            scope.DbContext.GetDbSet<UserroleEntity>().AddRange(toUpdate, toDelete);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var viewModels = new List<UserRoleViewModel>
            {
                new() { id = 0, role_name = "NewRole" },
                new() { id = toUpdate.id, role_name = "Updated" },
                new() { id = -toDelete.id },
            };

            var (flag, _) = await service.BulkSaveAsync(viewModels, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var remaining = await scope.DbContext.GetDbSet<UserroleEntity>().AsNoTracking().ToListAsync();
            remaining.Count.ShouldBe(2);
            remaining.ShouldContain(t => t.role_name == "NewRole");
            remaining.ShouldContain(t => t.role_name == "Updated");
            remaining.ShouldNotContain(t => t.id == toDelete.id);
        }

        [Fact]
        public async Task GetAsync_BelongsToDifferentTenant_ReturnsNull()
        {
            using var scope = new SqliteTestDbContextScope();
            var role = new UserroleEntity { role_name = "Role1", tenant_id = 1 };
            scope.DbContext.GetDbSet<UserroleEntity>().Add(role);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetAsync(role.id, new CurrentUser { tenant_id = 2 });

            result.ShouldBeNull();
        }

        [Fact]
        public async Task UpdateAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var role = new UserroleEntity { role_name = "Role1", tenant_id = 1 };
            scope.DbContext.GetDbSet<UserroleEntity>().Add(role);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(new UserRoleViewModel { id = role.id, role_name = "Hijacked" }, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_BelongsToDifferentTenant_DoesNotDelete()
        {
            using var scope = new SqliteTestDbContextScope();
            var role = new UserroleEntity { role_name = "Role1", tenant_id = 1 };
            scope.DbContext.GetDbSet<UserroleEntity>().Add(role);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(role.id, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<UserroleEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == role.id)).ShouldNotBeNull();
        }
    }
}
