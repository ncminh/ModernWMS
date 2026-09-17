/*
 * date：2022-12-27
 * developer：NoNo
 */

using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ModernWMS.Core;
using ModernWMS.Core.DBContext;
using ModernWMS.Core.DynamicSearch;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.Core.Services;
using ModernWMS.Core.Utility;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.IServices;
using System.Collections.Generic;

namespace ModernWMS.WMS.Services
{
    /// <summary>
    ///  Dispatchlist Service.
    /// Basic CRUD/read for dispatchlist records; the confirm/pick sub-flow lives in
    /// DispatchConfirmService and the package/weight/delivery/sign sub-flow lives in
    /// DispatchDeliveryService.
    /// </summary>
    public class DispatchListService : BaseService<DispatchListEntity>, IDispatchListService
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

        /// <summary>
        /// functions
        /// </summary>
        private readonly FunctionHelper _functionHelper;

        #endregion Args

        #region constructor

        /// <summary>
        ///Dispatchlist  constructor
        /// </summary>
        /// <param name="dBContext">The DBContext</param>
        /// <param name="stringLocalizer">Localizer</param>
        public DispatchListService(
            SqlDBContext dBContext
          , IStringLocalizer<ModernWMS.Core.MultiLanguage> stringLocalizer
           , FunctionHelper functionHelper
            )
        {
            this._dBContext = dBContext;
            this._stringLocalizer = stringLocalizer;
            this._functionHelper = functionHelper;
        }

        #endregion constructor

        #region Api

        /// <summary>
        /// page search
        /// </summary>
        /// <param name="pageSearch">args</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        public async Task<(List<DispatchListViewModel> data, int totals)> PageAsync(PageSearch pageSearch, CurrentUser currentUser)
        {
            QueryCollection queries = new QueryCollection();
            if (pageSearch.searchObjects.Any())
            {
                pageSearch.searchObjects.ForEach(s =>
                {
                    queries.Add(s);
                });
            }
            var DbSet = _dBContext.GetDbSet<DispatchListEntity>().AsNoTracking();
            if (pageSearch.sqlTitle.Contains("dispatch_status"))
            {
                var dispatch_status = Convert.ToByte(pageSearch.sqlTitle.Trim().ToLower().Replace("dispatch_status", "").Replace("：", "").Replace(":", "").Replace("=", ""));
                DbSet = DbSet.Where(t => t.dispatch_status.Equals(dispatch_status));
            }
            else if (pageSearch.sqlTitle.Equals("package"))
            {
                DbSet = DbSet.Where(t => t.picked_qty == t.qty && (t.dispatch_status.Equals(3) || (t.package_qty < t.picked_qty && t.dispatch_status.Equals(5)) || t.dispatch_status.Equals(4)));
            }
            else if (pageSearch.sqlTitle.Equals("weight"))
            {
                DbSet = DbSet.Where(t => t.picked_qty == t.qty && (t.dispatch_status.Equals(3) || (t.weighing_qty < t.picked_qty && t.dispatch_status.Equals(4)) || t.dispatch_status.Equals(5)));
            }
            else if (pageSearch.sqlTitle.Equals("delivery"))
            {
                DbSet = DbSet.Where(t => t.picked_qty == t.qty && (t.dispatch_status.Equals(3) || t.dispatch_status.Equals(4) || t.dispatch_status.Equals(5) || t.dispatch_status.Equals(6)));
            }

            var query = from d in DbSet.AsNoTracking()
                        join sku in _dBContext.GetDbSet<SkuEntity>().AsNoTracking() on d.sku_id equals sku.id
                        join spu in _dBContext.GetDbSet<SpuEntity>().AsNoTracking() on sku.spu_id equals spu.id
                        select new DispatchListViewModel
                        {
                            id = d.id,
                            dispatch_no = d.dispatch_no,
                            dispatch_status = d.dispatch_status,
                            customer_id = d.customer_id,
                            customer_name = d.customer_name,
                            sku_id = d.sku_id,
                            qty = d.qty,
                            weight = d.weight,
                            volume = d.volume,
                            creator = d.creator,
                            create_time = d.create_time,
                            damage_qty = d.damage_qty,
                            lock_qty = d.lock_qty,
                            picked_qty = d.picked_qty,
                            intrasit_qty = d.intrasit_qty,
                            package_qty = d.package_qty,
                            unpackage_qty = d.picked_qty - d.package_qty,
                            weighing_qty = d.weighing_qty,
                            unweighing_qty = d.picked_qty - d.weighing_qty,
                            actual_qty = d.actual_qty,
                            sign_qty = d.sign_qty,
                            package_no = d.package_no,
                            package_person = d.package_person,
                            package_time = d.package_time,
                            weighing_no = d.weighing_no,
                            weighing_person = d.weighing_person,
                            weighing_weight = d.weighing_weight,
                            waybill_no = d.waybill_no,
                            carrier = d.carrier,
                            freightfee = d.freightfee,
                            last_update_time = d.last_update_time,
                            tenant_id = d.tenant_id,
                            sku_code = sku.sku_code,
                            spu_code = spu.spu_code,
                            spu_description = spu.spu_description,
                            spu_name = spu.spu_name,
                            bar_code = sku.bar_code,
                            unpicked_qty = d.qty - d.picked_qty,
                            length_unit = spu.length_unit,
                            volume_unit = spu.volume_unit,
                            weight_unit = spu.weight_unit,
                            pick_checker = d.pick_checker,
                            pick_checker_id = d.pick_checker_id,
                            is_todo = pageSearch.sqlTitle.Contains("dispatch_status") || (pageSearch.sqlTitle.Equals("package") && d.dispatch_status.Equals(4))
                                            || (pageSearch.sqlTitle.Equals("weight") && d.dispatch_status.Equals(5))
                                            || (pageSearch.sqlTitle.Equals("delivery") && d.dispatch_status.Equals(6)) ? false : true,
                        };
            query = query.Where(t => t.tenant_id.Equals(currentUser.tenant_id))
                 .Where(queries.AsExpression<DispatchListViewModel>());

            int totals = await query.CountAsync();
            var list = await query.OrderBy(t => t.is_todo == true ? 0 : 1).ThenByDescending(t => t.last_update_time)
                       .Skip((pageSearch.pageIndex - 1) * pageSearch.pageSize)
                       .Take(pageSearch.pageSize)
                       .ToListAsync();

            return (list, totals);
        }

        /// <summary>
        /// get dispatchlist by dispatch_no
        /// </summary>
        /// <param name="dispatch_no"></param>
        /// <param name="currentUser"></param>
        /// <returns></returns>
        public async Task<List<DispatchListViewModel>> GetByDispatchlistNo(string dispatch_no, CurrentUser currentUser)
        {
            var DbSet = _dBContext.GetDbSet<DispatchListEntity>();
            var datas = await (from d in DbSet.AsNoTracking()
                               join sku in _dBContext.GetDbSet<SkuEntity>().AsNoTracking() on d.sku_id equals sku.id
                               join spu in _dBContext.GetDbSet<SpuEntity>().AsNoTracking() on sku.spu_id equals spu.id
                               where d.dispatch_no == dispatch_no && d.tenant_id == currentUser.tenant_id
                               select new DispatchListViewModel
                               {
                                   id = d.id,
                                   dispatch_no = d.dispatch_no,
                                   dispatch_status = d.dispatch_status,
                                   customer_id = d.customer_id,
                                   customer_name = d.customer_name,
                                   sku_id = d.sku_id,
                                   qty = d.qty,
                                   weight = d.weight,
                                   volume = d.volume,
                                   creator = d.creator,
                                   create_time = d.create_time,
                                   damage_qty = d.damage_qty,
                                   lock_qty = d.lock_qty,
                                   picked_qty = d.picked_qty,
                                   intrasit_qty = d.intrasit_qty,
                                   package_qty = d.package_qty,
                                   unpackage_qty = d.picked_qty - d.package_qty,
                                   weighing_qty = d.weighing_qty,
                                   unweighing_qty = d.picked_qty - d.weighing_qty,
                                   actual_qty = d.actual_qty,
                                   sign_qty = d.sign_qty,
                                   package_no = d.package_no,
                                   package_person = d.package_person,
                                   package_time = d.package_time,
                                   weighing_no = d.weighing_no,
                                   weighing_person = d.weighing_person,
                                   weighing_weight = d.weighing_weight,
                                   waybill_no = d.waybill_no,
                                   carrier = d.carrier,
                                   freightfee = d.freightfee,
                                   last_update_time = d.last_update_time,
                                   tenant_id = d.tenant_id,
                                   sku_code = sku.sku_code,
                                   spu_code = spu.spu_code,
                                   spu_description = spu.spu_description,
                                   spu_name = spu.spu_name,
                                   bar_code = sku.bar_code,
                                   unpicked_qty = d.qty - d.picked_qty,
                                   sku_name = sku.sku_name,
                                   unit = sku.unit,
                                   pick_checker = d.pick_checker,
                                   pick_checker_id = d.pick_checker_id,
                               }).ToListAsync();
            return datas;
        }

        /// <summary>
        /// update dispatchlist with same dispatch_no
        /// </summary>
        /// <param name="viewModels"></param>
        /// <param name="currentUser"></param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> UpdateAsycn(List<DispatchListViewModel> viewModels, CurrentUser currentUser)
        {
            var DBSet = _dBContext.GetDbSet<DispatchListEntity>();
            var dispatch_no = viewModels.FirstOrDefault().dispatch_no;
            var dispatch_status = viewModels.FirstOrDefault().dispatch_status;
            var entities = await (DBSet.Where(t => t.dispatch_no == dispatch_no && t.tenant_id == currentUser.tenant_id)).ToListAsync();
            var delete_id_list = new List<int>();
            var sku_id_list = viewModels.Select(t => t.sku_id).ToList();
            var skus = await (_dBContext.GetDbSet<SkuEntity>().AsNoTracking().Where(t => sku_id_list.Contains(t.id))).ToListAsync();
            var now_time = DateTime.Now;
            if (entities.Any(t => t.dispatch_status != 1 && t.dispatch_status != 0))
            {
                return (false, "[202]" + _stringLocalizer["data_changed"]);
            }
            foreach (var vm in viewModels)
            {
                if (vm.id < 0)
                {
                    var entity = entities.FirstOrDefault(t => t.id == -vm.id);
                    if (entity == null)
                    {
                        return (false, "[202]" + _stringLocalizer["data_changed"]);
                    }
                    DBSet.Remove(entity);
                    delete_id_list.Add(entity.id);
                }
                else if (vm.id > 0)
                {
                    var entity = entities.FirstOrDefault(t => t.id == vm.id);
                    if (entity == null)
                    {
                        return (false, "[202]" + _stringLocalizer["data_changed"]);
                    }
                    entity.sku_id = vm.sku_id;
                    entity.qty = vm.qty;
                    entity.last_update_time = now_time;
                    var sku = skus.FirstOrDefault(t => t.id == entity.sku_id);
                    if (sku != null)
                    {
                        entity.volume = sku.volume * entity.qty;
                        entity.weight = sku.weight * entity.qty;
                    }
                }
                else if (vm.id == 0)
                {
                    var entity = new DispatchListEntity
                    {
                        id = 0,
                        dispatch_no = dispatch_no,
                        creator = currentUser.user_name,
                        create_time = now_time,
                        last_update_time = now_time,
                        dispatch_status = dispatch_status,
                        sku_id = vm.sku_id,
                        qty = vm.qty
                    };
                    var sku = skus.FirstOrDefault(t => t.id == entity.sku_id);
                    if (sku != null)
                    {
                        entity.volume = sku.volume * entity.qty;
                        entity.weight = sku.weight * entity.qty;
                    }
                    entities.Add(entity);
                    DBSet.Add(entity);
                }
            }
            var repeat_skus_id_list = entities.Where(t => !delete_id_list.Contains(t.id)).GroupBy(t => t.sku_id).Select(t => new { t.Key, cnt = t.Count() }).Where(t => t.cnt > 1).Select(t => t.Key).ToList();
            if (repeat_skus_id_list.Count > 0)
            {
                var repeat_skus = (skus.Where(t => repeat_skus_id_list.Contains(t.id)).Select(t => t.sku_code).ToList());
                var msg = "";
                foreach (var sku in repeat_skus)
                {
                    msg += string.Format(_stringLocalizer["exists_entity"], _stringLocalizer["sku_code"], sku);
                }
                return (false, msg);
            }
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
        /// advanced dispatch order page search
        /// </summary>
        /// <param name="pageSearch">args</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        public async Task<(List<PreDispatchListViewModel> data, int totals)> AdvancedDispatchlistPageAsync(PageSearch pageSearch, CurrentUser currentUser)
        {
            QueryCollection queries = new QueryCollection();
            if (pageSearch.searchObjects.Any())
            {
                pageSearch.searchObjects.ForEach(s =>
                {
                    queries.Add(s);
                });
            }
            var DbSet = _dBContext.GetDbSet<DispatchListEntity>();
            var query = from d in DbSet.AsNoTracking().Where(t => t.tenant_id.Equals(currentUser.tenant_id))
                        group new { d } by new { d.dispatch_no, d.dispatch_status, d.customer_id, d.customer_name, d.creator }
                        into dg
                        select new PreDispatchListViewModel
                        {
                            dispatch_no = dg.Key.dispatch_no,
                            dispatch_status = dg.Key.dispatch_status,
                            customer_id = dg.Key.customer_id,
                            customer_name = dg.Key.customer_name,
                            qty = dg.Sum(t => t.d.qty),
                            creator = dg.Key.creator,
                        };
            query = query.Where(queries.AsExpression<PreDispatchListViewModel>());
            if (pageSearch.sqlTitle.Contains("dispatch_status"))
            {
                var dispatch_status = Convert.ToByte(pageSearch.sqlTitle.Trim().ToLower().Replace("dispatch_status", "").Replace("：", "").Replace(":", "").Replace("=", ""));
                query = query.Where(t => t.dispatch_status.Equals(dispatch_status));
            }
            else if (pageSearch.sqlTitle.Equals("todo"))
            {
                query = query.Where(t => t.dispatch_status >= 2 && t.dispatch_status <= 5);
            }
            int totals = await query.CountAsync();
            var list = await query.OrderByDescending(t => t.dispatch_no)
                       .Skip((pageSearch.pageIndex - 1) * pageSearch.pageSize)
                       .Take(pageSearch.pageSize)
                       .ToListAsync();

            #region sqlite cannot sum data of decimal type

            var dispatch_no_list = list.Select(t => t.dispatch_no).Distinct().ToList();
            var d_datas = await (from d in DbSet.AsNoTracking()
                                 join sku in _dBContext.GetDbSet<SkuEntity>().AsNoTracking() on d.sku_id equals sku.id
                                 join spu in _dBContext.GetDbSet<SpuEntity>().AsNoTracking() on sku.spu_id equals spu.id
                                 where d.tenant_id == currentUser.tenant_id && dispatch_no_list.Contains(d.dispatch_no)
                                 select new
                                 {
                                     d.dispatch_no,
                                     volume = spu.volume_unit == 1 ? d.volume : (spu.volume_unit == 0 ? d.volume / 1000 : d.volume * 1000),
                                     weight = spu.weight_unit == 0 ? d.weight / 1000000 : (spu.weight_unit == 1 ? d.weight / 1000 : d.weight)
                                 }).ToListAsync();
            list.ForEach(t =>
            {
                t.volume = d_datas.Where(d => d.dispatch_no == t.dispatch_no).Sum(t => t.volume);
                t.weight = d_datas.Where(d => d.dispatch_no == t.dispatch_no).Sum(t => t.weight);
            });

            #endregion sqlite cannot sum data of decimal type

            return (list, totals);
        }

        /// <summary>
        /// add a new Dispatchlist
        /// </summary>
        /// <param name="viewModel">viewmodel</param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> AddAsync(List<DispatchListAddViewModel> viewModel, CurrentUser currentUser)
        {
            var DbSet = _dBContext.GetDbSet<DispatchListEntity>();
            var entities = viewModel.Adapt<List<DispatchListEntity>>();
            var sku_id_list = entities.Select(t => t.sku_id).ToList();
            var skus = await _dBContext.GetDbSet<SkuEntity>().Where(t => sku_id_list.Contains(t.id)).ToListAsync();
            var dispatch_no = await _functionHelper.GetFormNoAsync("Dispatchlist");
            var now_time = DateTime.Now;
            foreach (var entity in entities)
            {
                var sku = skus.FirstOrDefault(t => t.id == entity.sku_id);
                entity.id = 0;
                entity.create_time = now_time;
                entity.creator = currentUser.user_name;
                entity.last_update_time = now_time;
                entity.tenant_id = currentUser.tenant_id;
                if (sku != null)
                {
                    entity.volume = entity.qty * sku.volume;
                    entity.weight = entity.qty * sku.weight;
                }
                entity.dispatch_no = dispatch_no;
            }
            await DbSet.AddRangeAsync(entities);
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
        /// <param name="dispatch_no">dispatch_no</param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> DeleteAsync(string dispatch_no, CurrentUser currentUser)
        {
            var entities = await _dBContext.GetDbSet<DispatchListEntity>().Where(t => t.dispatch_no.Equals(dispatch_no) && t.tenant_id == currentUser.tenant_id).ToListAsync();
            if (entities.Any(t => t.dispatch_status > 1))
            {
                return (false, _stringLocalizer["status_not_delete"]);
            }
            _dBContext.GetDbSet<DispatchListEntity>().RemoveRange(entities);
            var qty = await _dBContext.SaveChangesAsync();
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
        /// Excel Import
        /// </summary>
        /// <param name="viewModels">viewModels</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> Import(List<DispatchListImportViewModel> viewModels, CurrentUser currentUser)
        {
            var DbSet = _dBContext.GetDbSet<DispatchListEntity>();
            var import_sku_code = viewModels.Select(e => e.sku_code).ToList();
            var import_customer_name = viewModels.Select(e => e.customer_name).ToList();
            var sku_list = await (from sku in _dBContext.GetDbSet<SkuEntity>()
                                  join spu in _dBContext.GetDbSet<SpuEntity>() on sku.spu_id equals spu.id
                                  where spu.tenant_id == currentUser.tenant_id && import_sku_code.Contains(sku.sku_code)
                                  select sku).ToListAsync();
            var customer_list = await _dBContext.GetDbSet<CustomerEntity>().Where(t => t.tenant_id == currentUser.tenant_id && import_customer_name.Contains(t.customer_name)).ToListAsync();
            var entities = new List<DispatchListEntity>();
            var groups = viewModels.Select(t => t.import_group).Distinct().ToList();
            var groups_code = await _functionHelper.GetFormNoListAsync("Dispatchlist", groups.Count);
            var group_code_dic = new Dictionary<int, string>();
            var now_time = DateTime.Now;
            for (int i = 0; i < groups.Count(); i++)
            {
                group_code_dic.Add(groups[i], groups_code[i]);
            }
            foreach (var vm in viewModels)
            {
                var customer = customer_list.FirstOrDefault(t => t.customer_name == vm.customer_name);
                if (customer == null)
                {
                    return (false, _stringLocalizer["customer_name"] + ":" + vm.customer_name + " " + _stringLocalizer["not_exists_entity"]);
                }
                var sku = sku_list.FirstOrDefault(t => t.sku_code == vm.sku_code);
                if (sku == null)
                {
                    return (false, _stringLocalizer["sku_name"] + ":" + vm.sku_name + "-" + _stringLocalizer["sku_code"] + ":" + vm.sku_code + " " + _stringLocalizer["not_exists_entity"]);
                }
                entities.Add(new DispatchListEntity
                {
                    customer_id = customer.id,
                    customer_name = vm.customer_name,
                    sku_id = sku.id,
                    qty = vm.qty,
                    creator = currentUser.user_name,
                    create_time = now_time,
                    last_update_time = now_time,
                    tenant_id = currentUser.tenant_id,
                    dispatch_no = group_code_dic[vm.import_group],
                });
            }
            await DbSet.AddRangeAsync(entities);
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

        #endregion Api
    }
}
