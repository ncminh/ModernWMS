using Microsoft.EntityFrameworkCore;
using ModernWMS.Core;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.UnitTests.TestSupport;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.Services;

namespace ModernWMS.UnitTests.Services.Sku
{
    public class SpuServiceTests
    {
        private static SpuService CreateService(ModernWMS.Core.DBContext.SqlDBContext dbContext) =>
            new(dbContext, new FakeStringLocalizer<MultiLanguage>());

        private static async Task<CategoryEntity> SeedCategoryAsync(ModernWMS.Core.DBContext.SqlDBContext dbContext, long tenantId)
        {
            var category = new CategoryEntity { category_name = "Cat", tenant_id = tenantId };
            dbContext.GetDbSet<CategoryEntity>().Add(category);
            await dbContext.SaveChangesAsync();
            return category;
        }

        [Fact]
        public async Task PageAsync_EmptyDatabase_ReturnsEmptyResult()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (data, totals) = await service.PageAsync(new PageSearch(), new CurrentUser());

            data.ShouldBeEmpty();
            totals.ShouldBe(0);
        }

        [Fact]
        public async Task PageAsync_OnlyReturnsRowsForCurrentTenant()
        {
            using var scope = new SqliteTestDbContextScope();
            var cat1 = await SeedCategoryAsync(scope.DbContext, 1);
            var cat2 = await SeedCategoryAsync(scope.DbContext, 2);
            scope.DbContext.GetDbSet<SpuEntity>().AddRange(
                new SpuEntity { spu_code = "Tenant1-Spu", category_id = cat1.id, tenant_id = 1 },
                new SpuEntity { spu_code = "Tenant2-Spu", category_id = cat2.id, tenant_id = 2 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (data, totals) = await service.PageAsync(new PageSearch(), new CurrentUser { tenant_id = 1 });

            totals.ShouldBe(1);
            data.ShouldHaveSingleItem().spu_code.ShouldBe("Tenant1-Spu");
        }

        [Fact]
        public async Task AddAsync_NewSpuCode_StampsAuditColumns()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);
            var currentUser = new CurrentUser { tenant_id = 1, user_name = "alice" };

            var (id, _) = await service.AddAsync(new SpuBothViewModel { spu_code = "SPU1" }, currentUser);

            id.ShouldBeGreaterThan(0);
            var saved = await scope.DbContext.GetDbSet<SpuEntity>().FindAsync(id);
            saved!.creator.ShouldBe("alice");
            saved.tenant_id.ShouldBe(1);
        }

        [Fact]
        public async Task AddAsync_DuplicateSpuCodeInSameTenant_IsRejected()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<SpuEntity>().Add(new SpuEntity { spu_code = "SPU1", tenant_id = 1 });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (id, _) = await service.AddAsync(new SpuBothViewModel { spu_code = "SPU1" }, new CurrentUser { tenant_id = 1 });

            id.ShouldBe(0);
        }

        [Fact]
        public async Task UpdateAsync_UnknownId_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.UpdateAsync(new SpuBothViewModel { id = 999 }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateAsync_DuplicateSpuCodeInSameTenant_IsRejected()
        {
            using var scope = new SqliteTestDbContextScope();
            scope.DbContext.GetDbSet<SpuEntity>().Add(new SpuEntity { spu_code = "SPU1", tenant_id = 1 });
            var target = new SpuEntity { spu_code = "SPU2", tenant_id = 1 };
            scope.DbContext.GetDbSet<SpuEntity>().Add(target);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(new SpuBothViewModel { id = target.id, spu_code = "SPU1" }, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateAsync_NewDetailRow_IsAdded()
        {
            using var scope = new SqliteTestDbContextScope();
            var spu = new SpuEntity { spu_code = "SPU1", tenant_id = 1 };
            scope.DbContext.GetDbSet<SpuEntity>().Add(spu);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var viewModel = new SpuBothViewModel { id = spu.id, spu_code = "SPU1" };
            viewModel.detailList.Add(new SkuViewModel { id = 0, sku_code = "SKU1" });
            var (flag, _) = await service.UpdateAsync(viewModel, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<SkuEntity>().AsNoTracking().CountAsync(t => t.spu_id == spu.id)).ShouldBe(1);
        }

        [Fact]
        public async Task UpdateAsync_ExistingDetailRow_IsUpdated()
        {
            using var scope = new SqliteTestDbContextScope();
            var spu = new SpuEntity { spu_code = "SPU1", tenant_id = 1 };
            scope.DbContext.GetDbSet<SpuEntity>().Add(spu);
            await scope.DbContext.SaveChangesAsync();
            var sku = new SkuEntity { spu_id = spu.id, sku_code = "SKU1" };
            scope.DbContext.GetDbSet<SkuEntity>().Add(sku);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var viewModel = new SpuBothViewModel { id = spu.id, spu_code = "SPU1" };
            viewModel.detailList.Add(new SkuViewModel { id = sku.id, sku_code = "SKU1-renamed" });
            var (flag, _) = await service.UpdateAsync(viewModel, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<SkuEntity>().FindAsync(sku.id))!.sku_code.ShouldBe("SKU1-renamed");
        }

        [Fact]
        public async Task UpdateAsync_NegativeDetailRowId_RemovesTheRow()
        {
            using var scope = new SqliteTestDbContextScope();
            var spu = new SpuEntity { spu_code = "SPU1", tenant_id = 1 };
            scope.DbContext.GetDbSet<SpuEntity>().Add(spu);
            await scope.DbContext.SaveChangesAsync();
            var sku = new SkuEntity { spu_id = spu.id, sku_code = "SKU1" };
            scope.DbContext.GetDbSet<SkuEntity>().Add(sku);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var viewModel = new SpuBothViewModel { id = spu.id, spu_code = "SPU1" };
            viewModel.detailList.Add(new SkuViewModel { id = -sku.id });
            var (flag, _) = await service.UpdateAsync(viewModel, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<SkuEntity>().AsNoTracking().CountAsync(t => t.spu_id == spu.id)).ShouldBe(0);
        }

        [Fact]
        public async Task DeleteAsync_ReferencedByAsn_IsBlocked()
        {
            using var scope = new SqliteTestDbContextScope();
            var spu = new SpuEntity { spu_code = "SPU1", tenant_id = 1 };
            scope.DbContext.GetDbSet<SpuEntity>().Add(spu);
            await scope.DbContext.SaveChangesAsync();
            var asnmaster = new AsnmasterEntity();
            scope.DbContext.GetDbSet<AsnmasterEntity>().Add(asnmaster);
            await scope.DbContext.SaveChangesAsync();
            scope.DbContext.GetDbSet<AsnEntity>().Add(new AsnEntity { asnmaster_id = asnmaster.id, spu_id = spu.id });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(spu.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_NotReferenced_DeletesSpuAndItsSkus()
        {
            using var scope = new SqliteTestDbContextScope();
            var spu = new SpuEntity { spu_code = "SPU1", tenant_id = 1 };
            scope.DbContext.GetDbSet<SpuEntity>().Add(spu);
            await scope.DbContext.SaveChangesAsync();
            scope.DbContext.GetDbSet<SkuEntity>().Add(new SkuEntity { spu_id = spu.id, sku_code = "SKU1" });
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(spu.id, new CurrentUser { tenant_id = 1 });

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<SpuEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == spu.id)).ShouldBeNull();
            (await scope.DbContext.GetDbSet<SkuEntity>().AsNoTracking().CountAsync(t => t.spu_id == spu.id)).ShouldBe(0);
        }

        [Fact]
        public async Task GetSkuAsync_ReturnsMatchingDetail()
        {
            using var scope = new SqliteTestDbContextScope();
            var category = await SeedCategoryAsync(scope.DbContext, 1);
            var spu = new SpuEntity { spu_code = "SPU1", category_id = category.id, tenant_id = 1 };
            scope.DbContext.GetDbSet<SpuEntity>().Add(spu);
            await scope.DbContext.SaveChangesAsync();
            var sku = new SkuEntity { spu_id = spu.id, sku_code = "SKU1" };
            scope.DbContext.GetDbSet<SkuEntity>().Add(sku);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetSkuAsync(sku.id);

            result.sku_code.ShouldBe("SKU1");
        }

        [Fact]
        public async Task GetSkuByBarCodeAsync_ReturnsMatchingDetail()
        {
            using var scope = new SqliteTestDbContextScope();
            var category = await SeedCategoryAsync(scope.DbContext, 1);
            var spu = new SpuEntity { spu_code = "SPU1", category_id = category.id, tenant_id = 1 };
            scope.DbContext.GetDbSet<SpuEntity>().Add(spu);
            await scope.DbContext.SaveChangesAsync();
            var sku = new SkuEntity { spu_id = spu.id, sku_code = "SKU1", bar_code = "BC1" };
            scope.DbContext.GetDbSet<SkuEntity>().Add(sku);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetSkuByBarCodeAsync("BC1");

            result.sku_code.ShouldBe("SKU1");
        }

        [Fact]
        public async Task InsertOrUpdateSkuSafetyStockAsync_EmptyDetailList_ReturnsFailure()
        {
            using var scope = new SqliteTestDbContextScope();
            var service = CreateService(scope.DbContext);

            var (flag, _) = await service.InsertOrUpdateSkuSafetyStockAsync(new SkuSafetyStockPutViewModel { sku_id = 1 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task InsertOrUpdateSkuSafetyStockAsync_NewRow_IsAdded()
        {
            using var scope = new SqliteTestDbContextScope();
            var spu = new SpuEntity { spu_code = "SPU1", tenant_id = 1 };
            scope.DbContext.GetDbSet<SpuEntity>().Add(spu);
            await scope.DbContext.SaveChangesAsync();
            var sku = new SkuEntity { spu_id = spu.id, sku_code = "SKU1" };
            scope.DbContext.GetDbSet<SkuEntity>().Add(sku);
            await scope.DbContext.SaveChangesAsync();
            var warehouse = new WarehouseEntity { warehouse_name = "WH1", tenant_id = 1 };
            scope.DbContext.GetDbSet<WarehouseEntity>().Add(warehouse);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var viewModel = new SkuSafetyStockPutViewModel { sku_id = sku.id };
            viewModel.detailList.Add(new SkuSafetyStockViewModel { id = 0, warehouse_id = warehouse.id, safety_stock_qty = 10 });
            var (flag, _) = await service.InsertOrUpdateSkuSafetyStockAsync(viewModel);

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<SkuSafetyStockEntity>().AsNoTracking().CountAsync(t => t.sku_id == sku.id)).ShouldBe(1);
        }

        [Fact]
        public async Task InsertOrUpdateSkuSafetyStockAsync_NegativeId_RemovesRow()
        {
            using var scope = new SqliteTestDbContextScope();
            var spu = new SpuEntity { spu_code = "SPU1", tenant_id = 1 };
            scope.DbContext.GetDbSet<SpuEntity>().Add(spu);
            await scope.DbContext.SaveChangesAsync();
            var sku = new SkuEntity { spu_id = spu.id, sku_code = "SKU1" };
            scope.DbContext.GetDbSet<SkuEntity>().Add(sku);
            await scope.DbContext.SaveChangesAsync();
            var safetyStock = new SkuSafetyStockEntity { sku_id = sku.id, safety_stock_qty = 5 };
            scope.DbContext.GetDbSet<SkuSafetyStockEntity>().Add(safetyStock);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var viewModel = new SkuSafetyStockPutViewModel { sku_id = sku.id };
            viewModel.detailList.Add(new SkuSafetyStockViewModel { id = -safetyStock.id });
            var (flag, _) = await service.InsertOrUpdateSkuSafetyStockAsync(viewModel);

            flag.ShouldBeTrue();
            (await scope.DbContext.GetDbSet<SkuSafetyStockEntity>().AsNoTracking().CountAsync(t => t.sku_id == sku.id)).ShouldBe(0);
        }

        [Fact]
        public async Task GetAsync_BelongsToDifferentTenant_ReturnsEmptyViewModel()
        {
            using var scope = new SqliteTestDbContextScope();
            var category = new CategoryEntity { category_name = "Cat1", tenant_id = 1 };
            scope.DbContext.GetDbSet<CategoryEntity>().Add(category);
            await scope.DbContext.SaveChangesAsync();
            var spu = new SpuEntity { spu_code = "SPU1", category_id = category.id, tenant_id = 1 };
            scope.DbContext.GetDbSet<SpuEntity>().Add(spu);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var result = await service.GetAsync(spu.id, new CurrentUser { tenant_id = 2 });

            result.spu_code.ShouldBe(string.Empty);
        }

        [Fact]
        public async Task UpdateAsync_BelongsToDifferentTenant_ReturnsNotExists()
        {
            using var scope = new SqliteTestDbContextScope();
            var spu = new SpuEntity { spu_code = "SPU1", tenant_id = 1 };
            scope.DbContext.GetDbSet<SpuEntity>().Add(spu);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.UpdateAsync(new SpuBothViewModel { id = spu.id, spu_code = "Hijacked" }, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_BelongsToDifferentTenant_DoesNotDelete()
        {
            using var scope = new SqliteTestDbContextScope();
            var spu = new SpuEntity { spu_code = "SPU1", tenant_id = 1 };
            scope.DbContext.GetDbSet<SpuEntity>().Add(spu);
            await scope.DbContext.SaveChangesAsync();

            var service = CreateService(scope.DbContext);
            var (flag, _) = await service.DeleteAsync(spu.id, new CurrentUser { tenant_id = 2 });

            flag.ShouldBeFalse();
            (await scope.DbContext.GetDbSet<SpuEntity>().AsNoTracking().FirstOrDefaultAsync(t => t.id == spu.id)).ShouldNotBeNull();
        }
    }
}
