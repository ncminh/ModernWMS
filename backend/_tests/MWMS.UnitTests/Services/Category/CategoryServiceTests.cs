using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.Category
{
    public class CategoryServiceTests
    {
        private static CategoryService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
            new(dbContext, new FakeStringLocalizer<MultiLanguage>());

        [Fact]
        public async Task GetAllAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<CategoryEntity>().AddRange(
                new CategoryEntity { category_name = "Tenant1-Cat", tenant_id = 1 },
                new CategoryEntity { category_name = "Tenant2-Cat", tenant_id = 2 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var data = await service.GetAllAsync(new CurrentUser { tenant_id = 1 });

            data.ShouldHaveSingleItem().category_name.ShouldBe("Tenant1-Cat");
        }

        [Fact]
        public async Task AddAsync_NewName_StampsAuditColumns()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);
            var currentUser = new CurrentUser { tenant_id = 1, user_name = "alice" };

            var (id, _) = await service.AddAsync(new CategoryViewModel { category_name = "Cat1" }, currentUser);

            id.ShouldBeGreaterThan(0);
            var saved = await scope.DbContext.GetDbSet<CategoryEntity>().FindAsync(id);
            saved!.creator.ShouldBe("alice");
            saved.tenant_id.ShouldBe(1);
        }

        [Fact]
        public async Task AddAsync_DuplicateNameInSameTenant_IsRejected()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<CategoryEntity>().Add(new CategoryEntity { category_name = "Cat1", tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (id, _) = await service.AddAsync(new CategoryViewModel { category_name = "Cat1" }, new CurrentUser { tenant_id = 1 });

            id.ShouldBe(0);
        }

        [Fact]
        public async Task UpdateAsync_UnknownId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.UpdateAsync(new CategoryViewModel { id = 999 }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateAsync_TogglingInvalid_CascadesToDescendants()
        {
            // Regression test for a fixed bug: see the comment on
            // DeleteAsync_WithMultiLevelDescendants_DeletesWholeSubtree below. A 3-level
            // hierarchy here used to cause infinite recursion (StackOverflowException) in
            // CategoryService.GetChildren before the fix.
            using var scope = new SqliteTestDbContextScope();
            var root = new CategoryEntity { category_name = "Root", tenant_id = 1, is_valid = true };
            scope.DbContext.GetDbSet<CategoryEntity>().Add(root);
            await scope.DbContext.SaveChangesAsync();
            var child = new CategoryEntity { category_name = "Child", tenant_id = 1, is_valid = true, parent_id = root.id };
            scope.DbContext.GetDbSet<CategoryEntity>().Add(child);
            await scope.DbContext.SaveChangesAsync();
            var grandchild = new CategoryEntity { category_name = "Grandchild", tenant_id = 1, is_valid = true, parent_id = child.id };
            scope.DbContext.GetDbSet<CategoryEntity>().Add(grandchild);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(new CategoryViewModel { id = root.id, category_name = "Root", is_valid = false }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<CategoryEntity>().FindAsync(child.id))!.is_valid.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<CategoryEntity>().FindAsync(grandchild.id))!.is_valid.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_ReferencedBySpu_IsBlocked()
        {
            using var scope = new SqliteTestDbContextScope();
            var category = new CategoryEntity { category_name = "Cat1", tenant_id = 1 };
            scope.DbContext.GetDbSet<CategoryEntity>().Add(category);
            await scope.DbContext.SaveChangesAsync();
            scope.DbContext.GetDbSet<SpuEntity>().Add(new SpuEntity { spu_code = "SPU1", category_id = category.id, tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(category.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_WithMultiLevelDescendants_DeletesWholeSubtree()
        {
            // Regression test for a fixed bug: CategoryService.GetChildren's recursive call
            // used to pass item.parent_id instead of item.id, so a 3-level-deep hierarchy
            // either recursed infinitely (UpdateAsync's cascade, which loads the whole
            // non-root table) or silently missed everything past direct children
            // (DeleteAsync, which only loaded direct children to begin with). This seeds a
            // 3-level tree (root -> child -> grandchild) to prove both are fixed.
            using var scope = new SqliteTestDbContextScope();
            var root = new CategoryEntity { category_name = "Root", tenant_id = 1 };
            scope.DbContext.GetDbSet<CategoryEntity>().Add(root);
            await scope.DbContext.SaveChangesAsync();
            var child = new CategoryEntity { category_name = "Child", tenant_id = 1, parent_id = root.id };
            scope.DbContext.GetDbSet<CategoryEntity>().Add(child);
            await scope.DbContext.SaveChangesAsync();
            var grandchild = new CategoryEntity { category_name = "Grandchild", tenant_id = 1, parent_id = child.id };
            scope.DbContext.GetDbSet<CategoryEntity>().Add(grandchild);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(root.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<CategoryEntity>().AsNoTracking().CountAsync()).ShouldBe(0);
        }

        [Fact]
        public async Task GetAsync_BelongsToDifferentTenant_ReturnsEmptyViewModel()
        {
            using var scope = new SqliteTestDbContextScope();
            var category = new CategoryEntity { category_name = "Cat1", tenant_id = 1 };
            scope.DbContext.GetDbSet<CategoryEntity>().Add(category);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetAsync(category.id, new CurrentUser { tenant_id = 2 });

            result.category_name.ShouldBe(string.Empty);
        }

        [Fact]
        public async Task UpdateAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var category = new CategoryEntity { category_name = "Cat1", tenant_id = 1 };
            scope.DbContext.GetDbSet<CategoryEntity>().Add(category);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(new CategoryViewModel { id = category.id, category_name = "Hijacked" }, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_BelongsToDifferentTenant_DoesNotDelete()
        {
            using var scope = new SqliteTestDbContextScope();
            var category = new CategoryEntity { category_name = "Cat1", tenant_id = 1 };
            scope.DbContext.GetDbSet<CategoryEntity>().Add(category);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(category.id, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<CategoryEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == category.id)).ShouldNotBeNull();
        }
    }
}
