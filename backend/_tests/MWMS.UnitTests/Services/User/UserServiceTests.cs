using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.User
{
    public class UserServiceTests
    {
        private static UserService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
            new(dbContext, new FakeStringLocalizer<MultiLanguage>());

        [Fact]
        public async Task PageAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<UserEntity>().AddRange(
                new UserEntity { UserNum = "Tenant1-User", TenantId = 1 },
                new UserEntity { UserNum = "Tenant2-User", TenantId = 2 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.PageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
            data.ShouldHaveSingleItem().UserNum.ShouldBe("Tenant1-User");
        }

        [Fact]
        public async Task GetSelectItemsAsnyc_OnlyReturnsValidRolesForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<UserroleEntity>().AddRange(
                new UserroleEntity { role_name = "Tenant1-Valid", tenant_id = 1, is_valid = true },
                new UserroleEntity { role_name = "Tenant1-Invalid", tenant_id = 1, is_valid = false },
                new UserroleEntity { role_name = "Tenant2-Valid", tenant_id = 2, is_valid = true });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetSelectItemsAsnyc(new CurrentUser { tenant_id = 1 });

            result.ShouldHaveSingleItem().name.ShouldBe("Tenant1-Valid");
        }

        [Fact]
        public async Task AddAsync_NewUserNum_StampsAuditColumnsAndHashesPassword()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (id, generatedPassword) = await service.AddAsync(new UserViewModel { UserNum = "U1" }, new CurrentUser { tenant_id = 1 });

            id.ShouldBeGreaterThan(0);
            generatedPassword.Length.ShouldBe(6);
            var saved = await scope.DbContext.GetDbSet<UserEntity>().FindAsync(id);
            saved!.TenantId.ShouldBe(1);
            saved.AuthString.ShouldNotBe(generatedPassword);
            saved.AuthString.ShouldNotBeEmpty();
        }

        [Fact]
        public async Task AddAsync_DuplicateUserNumInSameTenant_IsRejected()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<UserEntity>().Add(new UserEntity { UserNum = "U1", TenantId = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (id, _) = await service.AddAsync(new UserViewModel { UserNum = "U1" }, new CurrentUser { tenant_id = 1 });

            id.ShouldBe(0);
        }

        [Fact]
        public async Task UpdateAsync_UnknownId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.UpdateAsync(new UserViewModel { id = 999 }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateAsync_ExistingUser_UpdatesFields()
        {
            using var scope = new SqliteTestDbContextScope();
            var user = new UserEntity { UserNum = "U1", TenantId = 1, UserName = "Old" };
            scope.DbContext.GetDbSet<UserEntity>().Add(user);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(new UserViewModel { id = user.id, UserNum = "U1", UserName = "New" }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<UserEntity>().FindAsync(user.id))!.UserName.ShouldBe("New");
        }

        [Fact]
        public async Task DeleteAsync_ExistingUser_Succeeds()
        {
            using var scope = new SqliteTestDbContextScope();
            var user = new UserEntity { UserNum = "U1", TenantId = 1 };
            scope.DbContext.GetDbSet<UserEntity>().Add(user);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(user.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<UserEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == user.id)).ShouldBeNull();
        }

        [Fact]
        public async Task ExcelAsync_DuplicateWithinExcelBatch_IsRejected()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);
            var datas = new List<UserExcelImportViewModel>
            {
                new() { user_num = "U1" },
                new() { user_num = "U1" },
            };

            var (flag, _) = await service.ExcelAsync(datas, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<UserEntity>().CountAsync()).ShouldBe(0);
        }

        [Fact]
        public async Task ExcelAsync_DuplicateAgainstExistingRow_IsRejected()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<UserEntity>().Add(new UserEntity { UserNum = "U1", TenantId = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var datas = new List<UserExcelImportViewModel> { new() { user_num = "U1" } };

            var (flag, _) = await service.ExcelAsync(datas, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<UserEntity>().CountAsync()).ShouldBe(1);
        }

        [Fact]
        public async Task ExcelAsync_AllNew_InsertsAllRowsWithDefaultPassword()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);
            var datas = new List<UserExcelImportViewModel>
            {
                new() { user_num = "U1" },
                new() { user_num = "U2" },
            };

            var (flag, _) = await service.ExcelAsync(datas, new CurrentUser { tenant_id = 1, user_name = "alice" });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<UserEntity>().CountAsync()).ShouldBe(2);
        }

        [Fact]
        public async Task ResetPwd_ForUsersInCurrentTenant_ChangesTheirPasswords()
        {
            using var scope = new SqliteTestDbContextScope();
            var user = new UserEntity { UserNum = "U1", TenantId = 1, AuthString = "old-hash" };
            scope.DbContext.GetDbSet<UserEntity>().Add(user);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, newPassword) = await service.ResetPwd(new BatchOperationViewModel { id_list = new List<int> { user.id } }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            newPassword.Length.ShouldBe(6);
            (await scope.DbContext.GetDbSet<UserEntity>().FindAsync(user.id))!.AuthString.ShouldNotBe("old-hash");
        }

        [Fact]
        public async Task ResetPwd_UserBelongsToDifferentTenant_LeavesPasswordUnchanged()
        {
            using var scope = new SqliteTestDbContextScope();
            var user = new UserEntity { UserNum = "U1", TenantId = 1, AuthString = "old-hash" };
            scope.DbContext.GetDbSet<UserEntity>().Add(user);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ResetPwd(new BatchOperationViewModel { id_list = new List<int> { user.id } }, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<UserEntity>().FindAsync(user.id))!.AuthString.ShouldBe("old-hash");
        }

        [Fact]
        public async Task ChangePwd_UnknownId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.ChangePwd(new UserChangePwdViewModel { id = 999 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task ChangePwd_WrongOldPassword_IsRejected()
        {
            using var scope = new SqliteTestDbContextScope();
            var user = new UserEntity { UserNum = "U1", TenantId = 1, AuthString = "correct-hash" };
            scope.DbContext.GetDbSet<UserEntity>().Add(user);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ChangePwd(new UserChangePwdViewModel { id = user.id, old_password = "wrong-hash", new_password = "new-hash" });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<UserEntity>().FindAsync(user.id))!.AuthString.ShouldBe("correct-hash");
        }

        [Fact]
        public async Task ChangePwd_CorrectOldPassword_UpdatesAuthString()
        {
            using var scope = new SqliteTestDbContextScope();
            var user = new UserEntity { UserNum = "U1", TenantId = 1, AuthString = "correct-hash" };
            scope.DbContext.GetDbSet<UserEntity>().Add(user);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.ChangePwd(new UserChangePwdViewModel { id = user.id, old_password = "correct-hash", new_password = "new-hash" });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<UserEntity>().FindAsync(user.id))!.AuthString.ShouldBe("new-hash");
        }

        [Fact]
        public async Task Register_NewUserName_CreatesTenantWithAdminRoleMenusAndRoleMenuMappings()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.Register(new RegisterViewModel { user_name = "NewCo", auth_string = "hash", email = "a@b.com" });

            flag.ShouldBeTrue();
            var user = (await scope.DbContext.GetDbSet<UserEntity>().AsNoTracking().ToListAsync()).ShouldHaveSingleItem();
            user.UserRole.ShouldBe("admin");
            var tenantId = user.TenantId;
            var adminRole = (await scope.DbContext.GetDbSet<UserroleEntity>().AsNoTracking().Where(t => t.tenant_id == tenantId).ToListAsync()).ShouldHaveSingleItem();
            adminRole.role_name.ShouldBe("admin");
            var menus = await scope.DbContext.GetDbSet<MenuEntity>().AsNoTracking().Where(t => t.tenant_id == tenantId).ToListAsync();
            menus.ShouldNotBeEmpty();
            var roleMenus = await scope.DbContext.GetDbSet<RoleMenuEntity>().AsNoTracking().Where(t => t.tenant_id == tenantId).ToListAsync();
            roleMenus.Count.ShouldBe(menus.Count);
            roleMenus.ShouldAllBe(t => t.userrole_id == adminRole.id);
        }

        [Fact]
        public async Task Register_DuplicateUserName_IsRejected()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<UserEntity>().Add(new UserEntity { UserNum = "NewCo" });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.Register(new RegisterViewModel { user_name = "NewCo" });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task GetAsync_BelongsToDifferentTenant_ReturnsNull()
        {
            using var scope = new SqliteTestDbContextScope();
            var user = new UserEntity { UserNum = "U1", TenantId = 1 };
            scope.DbContext.GetDbSet<UserEntity>().Add(user);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetAsync(user.id, new CurrentUser { tenant_id = 2 });

            result.ShouldBeNull();
        }

        [Fact]
        public async Task UpdateAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var user = new UserEntity { UserNum = "U1", TenantId = 1 };
            scope.DbContext.GetDbSet<UserEntity>().Add(user);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(new UserViewModel { id = user.id, UserNum = "Hijacked" }, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_BelongsToDifferentTenant_DoesNotDelete()
        {
            using var scope = new SqliteTestDbContextScope();
            var user = new UserEntity { UserNum = "U1", TenantId = 1 };
            scope.DbContext.GetDbSet<UserEntity>().Add(user);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(user.id, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<UserEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == user.id)).ShouldNotBeNull();
        }
    }
}
