/*
 * date：2022-12-20
 * developer：NoNo
 */

using Mapster;
using Microsoft.EntityFrameworkCore;
using ModernWMS.Core.DBContext;
using ModernWMS.Core.Services;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.IServices;
using ModernWMS.Core.Models;
using Microsoft.Extensions.Localization;
using ModernWMS.Core.Utility;
using System.Text;
using ModernWMS.Core.JWT;
using ModernWMS.Core.DynamicSearch;
using Microsoft.AspNetCore.Components.Forms;
using System.ComponentModel.Design;

namespace ModernWMS.WMS.Services
{
    /// <summary>
    ///  User Service
    /// </summary>
    public class UserService : BaseService<UserEntity>, IUserService
    {
        #region Args

        /// <summary>
        /// The DBContext
        /// </summary>
        private readonly SqlDBContext _dBContext;

        /// <summary>
        /// Localizer Service
        /// </summary>
        private readonly IStringLocalizer<ModernWMS.Core.MultiLanguage> _stringLocalizer;

        #endregion Args

        #region constructor

        /// <summary>
        ///User  constructor
        /// </summary>
        /// <param name="dBContext">The DBContext</param>
        /// <param name="stringLocalizer">Localizer</param>
        public UserService(
            SqlDBContext dBContext
          , IStringLocalizer<ModernWMS.Core.MultiLanguage> stringLocalizer
            )
        {
            this._dBContext = dBContext;
            this._stringLocalizer = stringLocalizer;
        }

        #endregion constructor

        #region Api

        /// <summary>
        /// get select items
        /// </summary>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        public async Task<List<FormSelectItem>> GetSelectItemsAsnyc(CurrentUser currentUser)
        {
            var res = new List<FormSelectItem>();
            var userrole_DBSet = _dBContext.GetDbSet<UserroleEntity>();
            res.AddRange(await (from ur in userrole_DBSet.AsNoTracking()
                                where ur.is_valid == true && ur.tenant_id == currentUser.tenant_id
                                select new FormSelectItem
                                {
                                    code = "user_role",
                                    name = ur.role_name,
                                    value = ur.id.ToString(),
                                    comments = "user's role",
                                }).ToListAsync());
            return res;
        }

        /// <summary>
        /// page search
        /// </summary>
        /// <param name="pageSearch">args</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        public async Task<(List<UserViewModel> data, int totals)> PageAsync(PageSearch pageSearch, CurrentUser currentUser)
        {
            QueryCollection queries = new QueryCollection();
            if (pageSearch.searchObjects.Any())
            {
                pageSearch.searchObjects.ForEach(s =>
                {
                    queries.Add(s);
                });
            }
            var DbSet = _dBContext.GetDbSet<UserEntity>();
            var query = DbSet.AsNoTracking()
                .Where(t => t.TenantId.Equals(currentUser.tenant_id))
                .Where(queries.AsExpression<UserEntity>());
            if (pageSearch.sqlTitle == "select")
            {
                query = query.Where(t => t.IsValid == true);
            }
            int totals = await query.CountAsync();
            var list = await query.OrderByDescending(t => t.CreateTime)
                       .Skip((pageSearch.pageIndex - 1) * pageSearch.pageSize)
                       .Take(pageSearch.pageSize)
                       .ToListAsync();
            return (list.Adapt<List<UserViewModel>>(), totals);
        }

        /// <summary>
        /// Get all records
        /// </summary>
        /// <returns></returns>
        public async Task<List<UserViewModel>> GetAllAsync(CurrentUser currentUser)
        {
            var DbSet = _dBContext.GetDbSet<UserEntity>();
            var data = await DbSet.AsNoTracking().Where(t => t.TenantId == currentUser.tenant_id).ToListAsync();
            return data.Adapt<List<UserViewModel>>();
        }

        /// <summary>
        /// Get a record by id
        /// </summary>
        /// <returns></returns>
        public async Task<UserViewModel> GetAsync(int id)
        {
            var DbSet = _dBContext.GetDbSet<UserEntity>();
            var entity = await DbSet.AsNoTracking().FirstOrDefaultAsync(t => t.id.Equals(id));
            if (entity == null)
            {
                return null;
            }
            return entity.Adapt<UserViewModel>();
        }

        /// <summary>
        /// add a new record
        /// </summary>
        /// <param name="viewModel">viewmodel</param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        public async Task<(int id, string msg)> AddAsync(UserViewModel viewModel, CurrentUser currentUser)
        {
            var DbSet = _dBContext.GetDbSet<UserEntity>();
            if (await DbSet.AnyAsync(t => t.UserNum == viewModel.UserNum && t.TenantId == currentUser.tenant_id))
            {
                return (0, string.Format(_stringLocalizer["exists_entity"], _stringLocalizer["user_num"], viewModel.UserNum));
            }
            var entity = viewModel.Adapt<UserEntity>();
            entity.id = 0;
            var new_auth = GetRandomPassword();
            entity.AuthString = Md5Helper.Md5Encrypt32(new_auth);
            entity.CreateTime = DateTime.Now;
            entity.LastUpdateTime = DateTime.Now;
            entity.TenantId = currentUser.tenant_id;
            await DbSet.AddAsync(entity);
            await _dBContext.SaveChangesAsync();
            if (entity.id > 0)
            {
                return (entity.id, new_auth);
            }
            else
            {
                return (0, _stringLocalizer["save_failed"]);
            }
        }

        /// <summary>
        /// update a record
        /// </summary>
        /// <param name="viewModel">args</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> UpdateAsync(UserViewModel viewModel, CurrentUser currentUser)
        {
            var DbSet = _dBContext.GetDbSet<UserEntity>();
            if (await DbSet.AnyAsync(t => t.id != viewModel.id && t.UserNum == viewModel.UserNum && t.TenantId == currentUser.tenant_id))
            {
                return (false, string.Format(_stringLocalizer["exists_entity"], _stringLocalizer["user_num"], viewModel.UserNum));
            }
            var entity = await DbSet.FirstOrDefaultAsync(t => t.id.Equals(viewModel.id));
            if (entity == null)
            {
                return (false, _stringLocalizer["not_exists_entity"]);
            }
            entity.id = viewModel.id;
            entity.UserNum = viewModel.UserNum;
            entity.UserName = viewModel.UserName;
            entity.ContactTel = viewModel.ContactTel;
            entity.UserRole = viewModel.UserRole;
            entity.Sex = viewModel.Sex;
            entity.IsValid = viewModel.IsValid;
            entity.LastUpdateTime = DateTime.Now;
            var qty = await _dBContext.SaveChangesAsync();
            if (qty > 0)
            {
                return (true, _stringLocalizer["save_success"]);
            }
            else
            {
                return (false, _stringLocalizer["save_failed"]);
            }
        }

        /// <summary>
        /// delete a record
        /// </summary>
        /// <param name="id">id</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> DeleteAsync(int id)
        {
            var qty = await _dBContext.GetDbSet<UserEntity>().Where(t => t.id.Equals(id)).ExecuteDeleteAsync();
            if (qty > 0)
            {
                return (true, _stringLocalizer["delete_success"]);
            }
            else
            {
                return (false, _stringLocalizer["delete_failed"]);
            }
        }

        /// <summary>
        /// import users by excel
        /// </summary>
        /// <param name="datas">excel datas</param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> ExcelAsync(List<UserExcelImportViewModel> datas, CurrentUser currentUser)
        {
            StringBuilder sb = new StringBuilder();
            var DbSet = _dBContext.GetDbSet<UserEntity>();
            var user_num_repeat_excel = datas.GroupBy(t => t.user_num).Select(t => new { user_num = t.Key, cnt = t.Count() }).Where(t => t.cnt > 1).ToList();
            foreach (var repeat in user_num_repeat_excel)
            {
                sb.AppendLine(string.Format(_stringLocalizer["exists_entity"], _stringLocalizer["user_num"], repeat.user_num));
            }
            if (user_num_repeat_excel.Count > 0)
            {
                return (false, sb.ToString());
            }

            var user_num_repeat_exists = await DbSet.Where(t => t.TenantId == currentUser.tenant_id).Where(t => datas.Select(t => t.user_num).ToList().Contains(t.UserNum)).Select(t => t.UserNum).ToListAsync();
            foreach (var repeat in user_num_repeat_exists)
            {
                sb.AppendLine(string.Format(_stringLocalizer["exists_entity"], _stringLocalizer["user_num"], repeat));
            }
            if (user_num_repeat_exists.Count > 0)
            {
                return (false, sb.ToString());
            }

            var entities = datas.Adapt<List<UserEntity>>();
            entities.ForEach(t =>
            {
                t.Creator = currentUser.user_name;
                t.TenantId = currentUser.tenant_id;
                t.AuthString = Md5Helper.Md5Encrypt32("pwd123456");
                t.CreateTime = DateTime.Now;
                t.LastUpdateTime = DateTime.Now;
                t.IsValid = true;
            });
            await DbSet.AddRangeAsync(entities);
            var res = await _dBContext.SaveChangesAsync();
            if (res > 0)
            {
                return (true, _stringLocalizer["save_success"]);
            }
            return (false, _stringLocalizer["save_failed"]);
        }

        /// <summary>
        /// reset password
        /// </summary>
        /// <param name="viewModel">viewmodel</param>
        /// <returns></returns>
        public async Task<(bool, string)> ResetPwd(BatchOperationViewModel viewModel)
        {
            var DBSet = _dBContext.GetDbSet<UserEntity>();
            var entities = await DBSet.Where(t => viewModel.id_list.Contains(t.id)).ToListAsync();
            var newpassword = GetRandomPassword();
            entities.ForEach(t => { t.AuthString = Md5Helper.Md5Encrypt32(newpassword); t.LastUpdateTime = DateTime.Now; });
            var res = await _dBContext.SaveChangesAsync();
            if (res > 0)
            {
                return (true, newpassword);
            }
            return (false, _stringLocalizer["operation_failed"]);
        }

        /// <summary>
        /// change password
        /// </summary>
        /// <param name="viewModel">viewmodel</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> ChangePwd(UserChangePwdViewModel viewModel)
        {
            var DBSet = _dBContext.GetDbSet<UserEntity>();
            var entity = await DBSet.FirstOrDefaultAsync(t => t.id.Equals(viewModel.id));
            if (entity == null)
            {
                return (false, _stringLocalizer["not_exists_entity"]);
            }
            if (!entity.AuthString.Equals(viewModel.old_password))
            {
                return (false, _stringLocalizer["old_password"] + _stringLocalizer["is_incorrect"]);
            }
            entity.AuthString = viewModel.new_password;
            await _dBContext.SaveChangesAsync();
            return (true, _stringLocalizer["save_success"]);
        }

        /// <summary>
        /// register a new tenant
        /// </summary>
        /// <param name="viewModel">viewModel</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> Register(RegisterViewModel viewModel)
        {
            var DbSet = _dBContext.GetDbSet<UserEntity>();
            var num_exist = await DbSet.AnyAsync(t => t.UserNum == viewModel.user_name);
            if (num_exist)
            {
                return (false, _stringLocalizer["username_existed"]);
            }
            var entity = viewModel.Adapt<UserEntity>();
            var time = DateTime.Now;
            entity.UserNum = entity.UserName;
            entity.id = 0;
            entity.AuthString = viewModel.auth_string;
            entity.CreateTime = time;
            entity.LastUpdateTime = time;
            entity.Email = viewModel.email;
            entity.Sex = viewModel.sex;
            entity.IsValid = true;
            await DbSet.AddAsync(entity);
            await _dBContext.SaveChangesAsync();
            if (entity.id > 0)
            {
                var tenant_id = entity.id;

                #region menus

                var menus = new List<MenuEntity>
                {
                    new MenuEntity
                    {
                        menu_name = "companySetting",
                        module = "baseModule",
                        vue_path = "companySetting",
                        vue_path_detail = "",
                        vue_directory = "base/companySetting",
                        sort = 1,
                        tenant_id = tenant_id
                    },
                    new MenuEntity
                    {
                        menu_name = "userRoleSetting",
                        module = "baseModule",
                        vue_path = "userRoleSetting",
                        vue_path_detail = "",
                        vue_directory = "base/userRoleSetting",
                        sort = 2,
                        tenant_id = tenant_id
                    },
                    new MenuEntity
                    {
                        menu_name = "roleMenu",
                        module = "baseModule",
                        vue_path = "roleMenu",
                        vue_path_detail = "",
                        vue_directory = "base/roleMenu",
                        sort = 3,
                        tenant_id = tenant_id
                    },
                    new MenuEntity
                    {
                        menu_name = "userManagement",
                        module = "baseModule",
                        vue_path = "userManagement",
                        vue_path_detail = "",
                        vue_directory = "base/userManagement",
                        sort = 4,
                        tenant_id = tenant_id
                    },
                    new MenuEntity
                    {
                        menu_name = "commodityCategorySetting",
                        module = "baseModule",
                        vue_path = "commodityCategorySetting",
                        vue_path_detail = "",
                        vue_directory = "base/commodityCategorySetting",
                        sort = 5,
                        tenant_id = tenant_id
                    },
                    new MenuEntity
                    {
                        menu_name = "commodityManagement",
                        module = "baseModule",
                        vue_path = "commodityManagement",
                        vue_path_detail = "",
                        vue_directory = "base/commodityManagement",
                        sort = 6,
                        tenant_id = tenant_id
                    },
                    new MenuEntity
                    {
                        menu_name = "supplier",
                        module = "baseModule",
                        vue_path = "supplier",
                        vue_path_detail = "",
                        vue_directory = "base/supplier",
                        sort = 7,
                        tenant_id = tenant_id
                    },
                    new MenuEntity
                    {
                        menu_name = "warehouseSetting",
                        module = "baseModule",
                        vue_path = "warehouseSetting",
                        vue_path_detail = "",
                        vue_directory = "base/warehouseSetting",
                        sort = 8,
                        tenant_id = tenant_id
                    },new MenuEntity
                    {
                        menu_name = "ownerOfCargo",
                        module = "baseModule",
                        vue_path = "ownerOfCargo",
                        vue_path_detail = "",
                        vue_directory = "base/ownerOfCargo",
                        sort = 9,
                        tenant_id = tenant_id
                    },new MenuEntity
                    {
                        menu_name = "freightSetting",
                        module = "baseModule",
                        vue_path = "freightSetting",
                        vue_path_detail = "",
                        vue_directory = "base/freightSetting",
                        sort = 10,
                        tenant_id = tenant_id
                    },new MenuEntity
                    {
                        menu_name = "customer",
                        module = "baseModule",
                        vue_path = "customer",
                        vue_path_detail = "",
                        vue_directory = "base/customer",
                        sort = 11,
                        tenant_id = tenant_id
                    },new MenuEntity
                    {
                        menu_name = "print",
                        module = "baseModule",
                        vue_path = "print",
                        vue_path_detail = "",
                        vue_directory = "base/print",
                        sort = 12,
                        tenant_id = tenant_id
                    },new MenuEntity
                    {
                        menu_name = "stockManagement",
                        module = "statisticAnalysis ",
                        vue_path = "stockManagement",
                        vue_path_detail = "",
                        vue_directory = "wms/stockManagement",
                        sort = 3,
                        tenant_id = tenant_id
                    },new MenuEntity
                    {
                        menu_name = "saftyStock",
                        module = "statisticAnalysis ",
                        vue_path = "saftyStock",
                        vue_path_detail = "",
                        vue_directory = "statisticAnalysis/saftyStock",
                        sort = 4,
                        tenant_id = tenant_id
                    },new MenuEntity
                    {
                        menu_name = "asnStatistic",
                        module = "statisticAnalysis ",
                        vue_path = "asnStatistic",
                        vue_path_detail = "",
                        vue_directory = "statisticAnalysis/asnStatistic",
                        sort = 5,
                        tenant_id = tenant_id
                    },new MenuEntity
                    {
                        menu_name = "deliveryStatistic",
                        module = "statisticAnalysis ",
                        vue_path = "deliveryStatistic",
                        vue_path_detail = "",
                        vue_directory = "statisticAnalysis/deliveryStatistic",
                        sort = 6,
                        tenant_id = tenant_id
                    },new MenuEntity
                    {
                        menu_name = "stockageStatistic",
                        module = "statisticAnalysis ",
                        vue_path = "stockageStatistic",
                        vue_path_detail = "",
                        vue_directory = "statisticAnalysis/stockageStatistic",
                        sort = 7,
                        tenant_id = tenant_id
                    },new MenuEntity
                    {
                        menu_name = "warehouseProcessing",
                        module = "warehouseWorkingModule",
                        vue_path = "warehouseProcessing",
                        vue_path_detail = "",
                        vue_directory = "warehouseWorking/warehouseProcessing",
                        sort = 4,
                        tenant_id = tenant_id
                    },new MenuEntity
                    {
                        menu_name = "warehouseMove",
                        module = "warehouseWorkingModule",
                        vue_path = "warehouseMove",
                        vue_path_detail = "",
                        vue_directory = "warehouseWorking/warehouseMove",
                        sort = 5,
                        tenant_id = tenant_id
                    },new MenuEntity
                    {
                        menu_name = "warehouseFreeze",
                        module = "warehouseWorkingModule",
                        vue_path = "warehouseFreeze",
                        vue_path_detail = "",
                        vue_directory = "warehouseWorking/warehouseFreeze",
                        sort = 6,
                        tenant_id = tenant_id
                    },new MenuEntity
                    {
                        menu_name = "warehouseAdjust",
                        module = "warehouseWorkingModule",
                        vue_path = "warehouseAdjust",
                        vue_path_detail = "",
                        vue_directory = "warehouseWorking/warehouseAdjust",
                        sort = 7,
                        tenant_id = tenant_id
                    },new MenuEntity
                    {
                        menu_name = "warehouseTaking",
                        module = "warehouseWorkingModule",
                        vue_path = "warehouseTaking",
                        vue_path_detail = "",
                        vue_directory = "warehouseWorking/warehouseTaking",
                        sort = 8,
                        tenant_id = tenant_id
                    },new MenuEntity
                    {
                        menu_name = "stockAsn",
                        module = "",
                        vue_path = "stockAsn",
                        vue_path_detail = "",
                        vue_directory = "wms/stockAsn",
                        sort = 2,
                        tenant_id = tenant_id
                    },new MenuEntity
                    {
                        menu_name = "deliveryManagement",
                        module = "",
                        vue_path = "deliveryManagement",
                        vue_path_detail = "",
                        vue_directory = "deliveryManagement/deliveryManagement",
                        sort = 5,
                        tenant_id = tenant_id
                    }
                    ,new MenuEntity
                    {
                        menu_name = "largeScreen",
                        module = "",
                        vue_path = "largeScreen",
                        vue_path_detail = "",
                        vue_directory = "largeScreen/largeScreen",
                        sort = 6,
                        tenant_id = tenant_id
                    }
                };

                #endregion menus

                entity.TenantId = tenant_id;
                entity.Creator = entity.UserName;
                entity.UserRole = "admin";
                var adminrole = new UserroleEntity { is_valid = true, last_update_time = time, create_time = time, role_name = "admin", tenant_id = tenant_id };
                await _dBContext.GetDbSet<UserroleEntity>().AddAsync(adminrole);
                await _dBContext.GetDbSet<MenuEntity>().AddRangeAsync(menus);
                await _dBContext.SaveChangesAsync();
                foreach (var menu in menus)
                {
                    await _dBContext.GetDbSet<RolemenuEntity>().AddAsync(new RolemenuEntity
                    {
                        userrole_id = adminrole.id,
                        authority = 1,
                        menu_id = menu.id,
                        tenant_id = tenant_id,
                        last_update_time = time,
                        create_time = time,
                    });
                }
                await _dBContext.SaveChangesAsync();
                return (true, _stringLocalizer["operation_success"]);
            }
            else
            {
                return (false, _stringLocalizer["operation_failed"]);
            }
        }

        /// <summary>
        /// get a random password
        /// </summary>
        /// <returns></returns>
        public string GetRandomPassword()
        {
            string randomChars = "ABCDEFGHIJKLMNOPQRSTVWXYZ123456789";
            string password = string.Empty;
            int randomNum;
            Random random = new Random();
            for (int i = 0; i < 6; i++)
            {
                randomNum = random.Next(randomChars.Length);
                password += randomChars[randomNum];
            }
            return password;
        }

        #endregion Api
    }
}