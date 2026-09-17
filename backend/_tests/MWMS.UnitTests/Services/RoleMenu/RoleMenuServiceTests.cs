using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.RoleMenu
{
    public class RoleMenuServiceTests
    {
        private static RoleMenuService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
            new(dbContext, new FakeStringLocalizer<MultiLanguage>());

        private static async Task<UserroleEntity> SeedRoleAsync(ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId, string roleName = "Role")
        {
            var role = new UserroleEntity { role_name = roleName, tenant_id = tenantId };
            dbContext.GetDbSet<UserroleEntity>().Add(role);
            await dbContext.SaveChangesAsync();
            return role;
        }

        [Fact]
        public async Task GetAllAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            var role1 = await SeedRoleAsync(scope.DbContext, 1, "Tenant1-Role");
            var role2 = await SeedRoleAsync(scope.DbContext, 2, "Tenant2-Role");
            scope.DbContext.GetDbSet<RoleMenuEntity>().AddRange(
                new RoleMenuEntity { userrole_id = role1.id, menu_id = 1, tenant_id = 1 },
                new RoleMenuEntity { userrole_id = role2.id, menu_id = 1, tenant_id = 2 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var data = await service.GetAllAsync(new CurrentUser { tenant_id = 1 });

            data.ShouldHaveSingleItem().role_name.ShouldBe("Tenant1-Role");
        }

        [Fact]
        public async Task GetAllMenusAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<MenuEntity>().AddRange(
                new MenuEntity { menu_name = "Tenant1-Menu", tenant_id = 1 },
                new MenuEntity { menu_name = "Tenant2-Menu", tenant_id = 2 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var data = await service.GetAllMenusAsync(new CurrentUser { tenant_id = 1 });

            data.ShouldHaveSingleItem().menu_name.ShouldBe("Tenant1-Menu");
        }

        [Fact]
        public async Task GetMenusByRoleId_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            var role1 = await SeedRoleAsync(scope.DbContext, 1);
            var menu = new MenuEntity { menu_name = "Menu1", tenant_id = 1 };
            scope.DbContext.GetDbSet<MenuEntity>().Add(menu);
            await scope.DbContext.SaveChangesAsync();
            scope.DbContext.GetDbSet<RoleMenuEntity>().Add(new RoleMenuEntity { userrole_id = role1.id, menu_id = menu.id, tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var resultSameTenant = await service.GetMenusByRoleId(role1.id, new CurrentUser { tenant_id = 1 });
            var resultOtherTenant = await service.GetMenusByRoleId(role1.id, new CurrentUser { tenant_id = 2 });

            resultSameTenant.ShouldHaveSingleItem().menu_name.ShouldBe("Menu1");
            resultOtherTenant.ShouldBeEmpty();
        }

        [Fact]
        public async Task GetAsync_UnknownUserroleId_ReturnsEmptyViewModel()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var result = await service.GetAsync(999, new CurrentUser { tenant_id = 1 });

            result.userrole_id.ShouldBe(0);
        }

        [Fact]
        public async Task GetAsync_ReturnsDeserializedMenuActionsAuthority()
        {
            using var scope = new SqliteTestDbContextScope();
            var role = await SeedRoleAsync(scope.DbContext, 1);
            var menu = new MenuEntity { menu_name = "Menu1", tenant_id = 1 };
            scope.DbContext.GetDbSet<MenuEntity>().Add(menu);
            await scope.DbContext.SaveChangesAsync();
            scope.DbContext.GetDbSet<RoleMenuEntity>().Add(new RoleMenuEntity
            {
                userrole_id = role.id,
                menu_id = menu.id,
                tenant_id = 1,
                authority = 1,
                menu_actions_authority = "[\"view\",\"edit\"]",
            });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetAsync(role.id, new CurrentUser { tenant_id = 1 });

            result.userrole_id.ShouldBe(role.id);
            var detail = result.detailList.ShouldHaveSingleItem();
            detail.menu_actions_authority.ShouldBe(new List<string> { "view", "edit" });
        }

        [Fact]
        public async Task AddAsync_NewMapping_Persists()
        {
            using var scope = new SqliteTestDbContextScope();
            var role = await SeedRoleAsync(scope.DbContext, 1);
            var service = CreateService(scope.DbContext);
            var viewModel = new RoleMenuBothViewModel { userrole_id = role.id };
            viewModel.detailList.Add(new RoleMenuViewModel { menu_id = 1, authority = 1 });

            var (id, _) = await service.AddAsync(viewModel, new CurrentUser { tenant_id = 1 });

            id.ShouldBe(role.id);
            (await scope.DbContext.GetDbSet<RoleMenuEntity>().CountAsync(t => t.userrole_id == role.id)).ShouldBe(1);
        }

        [Fact]
        public async Task AddAsync_MappingAlreadyExistsForRole_IsRejected()
        {
            using var scope = new SqliteTestDbContextScope();
            var role = await SeedRoleAsync(scope.DbContext, 1);
            scope.DbContext.GetDbSet<RoleMenuEntity>().Add(new RoleMenuEntity { userrole_id = role.id, menu_id = 1, tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var viewModel = new RoleMenuBothViewModel { userrole_id = role.id };
            viewModel.detailList.Add(new RoleMenuViewModel { menu_id = 2, authority = 1 });

            var (id, _) = await service.AddAsync(viewModel, new CurrentUser { tenant_id = 1 });

            id.ShouldBe(0);
        }

        [Fact]
        public async Task UpdateAsync_UnknownUserroleId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.UpdateAsync(new RoleMenuBothViewModel { userrole_id = 999 }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateAsync_AddsUpdatesAndRemovesMappingsInOneCall()
        {
            using var scope = new SqliteTestDbContextScope();
            var role = await SeedRoleAsync(scope.DbContext, 1);
            var toUpdate = new RoleMenuEntity { userrole_id = role.id, menu_id = 1, authority = 1, tenant_id = 1 };
            var toDelete = new RoleMenuEntity { userrole_id = role.id, menu_id = 2, authority = 1, tenant_id = 1 };
            scope.DbContext.GetDbSet<RoleMenuEntity>().AddRange(toUpdate, toDelete);
            await scope.DbContext.SaveChangesAsync();
            // UpdateAsync builds fresh RoleMenuEntity instances with the same ids and attaches
            // them; a real request gets a fresh SqlDBContext, so simulate that by clearing the
            // tracker instead of leaving these seed instances tracked from the Add above.
            scope.DbContext.ChangeTracker.Clear();

            var service = CreateService(scope.DbContext);
            var viewModel = new RoleMenuBothViewModel { userrole_id = role.id };
            viewModel.detailList.Add(new RoleMenuViewModel { id = toUpdate.id, menu_id = toUpdate.menu_id, authority = 2 });
            viewModel.detailList.Add(new RoleMenuViewModel { id = -toDelete.id, menu_id = toDelete.menu_id });
            viewModel.detailList.Add(new RoleMenuViewModel { id = 0, menu_id = 3, authority = 1 });

            var (flag, _) = await service.UpdateAsync(viewModel, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            var remaining = await scope.DbContext.GetDbSet<RoleMenuEntity>().AsNoTracking().Where(t => t.userrole_id == role.id).ToListAsync();
            remaining.Count.ShouldBe(2);
            remaining.ShouldContain(t => t.menu_id == 1 && t.authority == 2);
            remaining.ShouldContain(t => t.menu_id == 3);
            remaining.ShouldNotContain(t => t.menu_id == 2);
        }

        [Fact]
        public async Task DeleteAsync_ExistingMapping_Succeeds()
        {
            using var scope = new SqliteTestDbContextScope();
            var role = await SeedRoleAsync(scope.DbContext, 1);
            scope.DbContext.GetDbSet<RoleMenuEntity>().Add(new RoleMenuEntity { userrole_id = role.id, menu_id = 1, tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(role.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<RoleMenuEntity>().AsNoTracking().CountAsync(t => t.userrole_id == role.id)).ShouldBe(0);
        }

        [Fact]
        public async Task GetAsync_BelongsToDifferentTenant_ReturnsEmptyViewModel()
        {
            using var scope = new SqliteTestDbContextScope();
            var role = await SeedRoleAsync(scope.DbContext, 1);
            var menu = new MenuEntity { menu_name = "Menu1", tenant_id = 1 };
            scope.DbContext.GetDbSet<MenuEntity>().Add(menu);
            await scope.DbContext.SaveChangesAsync();
            scope.DbContext.GetDbSet<RoleMenuEntity>().Add(new RoleMenuEntity { userrole_id = role.id, menu_id = menu.id, tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetAsync(role.id, new CurrentUser { tenant_id = 2 });

            result.userrole_id.ShouldBe(0);
        }

        [Fact]
        public async Task DeleteAsync_BelongsToDifferentTenant_DoesNotDelete()
        {
            using var scope = new SqliteTestDbContextScope();
            var role = await SeedRoleAsync(scope.DbContext, 1);
            scope.DbContext.GetDbSet<RoleMenuEntity>().Add(new RoleMenuEntity { userrole_id = role.id, menu_id = 1, tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(role.id, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<RoleMenuEntity>().AsNoTracking().CountAsync(t => t.userrole_id == role.id)).ShouldBe(1);
        }
    }
}
