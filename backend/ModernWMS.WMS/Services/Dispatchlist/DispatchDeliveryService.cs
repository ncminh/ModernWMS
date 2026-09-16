/*
 * date：2022-12-27
 * developer：NoNo
 */

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ModernWMS.Core;
using ModernWMS.Core.DBContext;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Services;
using ModernWMS.Core.Utility;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.IServices;

namespace ModernWMS.WMS.Services
{
    /// <summary>
    ///  DispatchDelivery Service (package/weight/delivery/freight/sign sub-flow,
    /// split out of DispatchlistService). Delivery is the only method that writes
    /// StockEntity.
    /// </summary>
    public class DispatchDeliveryService : BaseService<DispatchlistEntity>, IDispatchDeliveryService
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
        /// DispatchDelivery constructor
        /// </summary>
        /// <param name="dBContext">The DBContext</param>
        /// <param name="stringLocalizer">Localizer</param>
        public DispatchDeliveryService(
            SqlDBContext dBContext
          , IStringLocalizer<ModernWMS.Core.MultiLanguage> stringLocalizer
            )
        {
            this._dBContext = dBContext;
            this._stringLocalizer = stringLocalizer;
        }

        #endregion constructor

        /// <summary>
        /// get package or weight  code
        /// </summary>
        /// <returns></returns>
        private string GetPackageOrWeightCode()
        {
            string date = DateTime.Now.ToString("yyyy" + "MM" + "dd");
            DateTime _dtStart = new DateTime(1970, 1, 1, 8, 0, 0);
            long timeStamp = Convert.ToInt32(DateTime.Now.Subtract(_dtStart).TotalSeconds);
            return date + timeStamp.ToString();
        }

        /// <summary>
        ///  package
        /// </summary>
        /// <param name="viewModels">viewModels</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public async Task<(bool flag, string msg)> Package(List<DispatchlistPackageViewModel> viewModels, CurrentUser currentUser)
        {
            var DBSet = _dBContext.GetDbSet<DispatchlistEntity>();
            var dispatchlist_id_list = viewModels.Select(t => t.id).ToList();
            var entities = await DBSet.Where(t => dispatchlist_id_list.Contains(t.id) && t.tenant_id == currentUser.tenant_id).ToListAsync();
            var now_time = DateTime.Now;
            var code = GetPackageOrWeightCode();
            foreach (var vm in viewModels)
            {
                var entity = entities.FirstOrDefault(t => t.id == vm.id && t.dispatch_status == vm.dispatch_status);
                if (entity == null)
                {
                    return (false, "[202]" + _stringLocalizer["data_changed"]);
                }
                if ((entity.package_qty + vm.package_qty) > entity.picked_qty)
                {
                    return (false, "[202]" + _stringLocalizer["unpackgeqty_lessthen"]);
                }
                entity.last_update_time = now_time;
                entity.package_person = currentUser.user_name;
                entity.package_qty += vm.package_qty;
                entity.package_time = now_time;
                entity.package_no = code;
                entity.dispatch_status = 4;
            }
            var saved = false;
            int res = 0;
            while (!saved)
            {
                try
                {
                    // Attempt to save changes to the database
                    res = await _dBContext.SaveChangesAsync();
                    saved = true;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    foreach (var entry in ex.Entries)
                    {
                        if (entry.Entity is DispatchlistEntity)
                        {
                            var proposedValues = entry.CurrentValues;
                            var databaseValues = entry.GetDatabaseValues();
                            var t_vm = viewModels.FirstOrDefault(t => t.id == UtilConvert.ObjToInt(databaseValues["id"]));
                            if (t_vm == null)
                            {
                                return (false, "[202]" + _stringLocalizer["data_changed"]);
                            }
                            if (UtilConvert.ObjToInt(databaseValues["package_qty"]) + t_vm.package_qty > t_vm.picked_qty)
                            {
                                return (false, "[202]" + _stringLocalizer["data_changed"]);
                            }
                            else
                            {
                                proposedValues["package_qty"] = UtilConvert.ObjToInt(databaseValues["package_qty"]) + t_vm.package_qty;
                                proposedValues["last_update_time"] = DateTime.Now;
                                if (UtilConvert.ObjToInt(databaseValues["package_qty"]) + t_vm.package_qty == UtilConvert.ObjToInt(databaseValues["picked_qty"]))
                                {
                                    proposedValues["dispatch_status"] = 4;
                                }
                            }
                            // Refresh original values to bypass next concurrency check
                            entry.OriginalValues.SetValues(databaseValues);
                        }
                        else
                        {
                            throw new NotSupportedException(_stringLocalizer["try_agin"]);
                        }
                    }
                }
            }
            if (res > 0)
            {
                return (true, _stringLocalizer["operation_success"]);
            }
            else
            {
                return (false, _stringLocalizer["operation_failed"]);
            }
        }

        /// <summary>
        ///  weight
        /// </summary>
        /// <param name="viewModels">viewModels</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public async Task<(bool flag, string msg)> Weight(List<DispatchlistWeightViewModel> viewModels, CurrentUser currentUser)
        {
            var DBSet = _dBContext.GetDbSet<DispatchlistEntity>();
            var dispatchlist_id_list = viewModels.Select(t => t.id).ToList();
            var entities = await DBSet.Where(t => dispatchlist_id_list.Contains(t.id) && t.tenant_id == currentUser.tenant_id).ToListAsync();
            var now_time = DateTime.Now;
            var code = GetPackageOrWeightCode();
            foreach (var vm in viewModels)
            {
                var entity = entities.FirstOrDefault(t => t.id == vm.id && t.dispatch_status == vm.dispatch_status);
                if (entity == null)
                {
                    return (false, "[202]" + _stringLocalizer["data_changed"]);
                }
                if ((entity.weighing_qty + vm.weighing_qty) > entity.picked_qty)
                {
                    return (false, "[202]" + _stringLocalizer["unweightqty_lessthen"]);
                }
                entity.last_update_time = now_time;
                entity.weighing_person = currentUser.user_name;
                entity.weighing_qty += vm.weighing_qty;
                entity.weighing_weight += vm.weighing_weight;
                entity.weighing_no = code;
                entity.dispatch_status = 5;
            }
            var saved = false;
            int res = 0;
            while (!saved)
            {
                try
                {
                    // Attempt to save changes to the database
                    res = await _dBContext.SaveChangesAsync();
                    saved = true;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    foreach (var entry in ex.Entries)
                    {
                        if (entry.Entity is StockEntity)
                        {
                            var proposedValues = entry.CurrentValues;
                            var databaseValues = entry.GetDatabaseValues();

                            var t_vm = viewModels.FirstOrDefault(t => t.id == UtilConvert.ObjToInt(databaseValues["id"]));
                            if (t_vm == null)
                            {
                                return (false, "[202]" + _stringLocalizer["data_changed"]);
                            }
                            if (UtilConvert.ObjToInt(databaseValues["weighing_qty"]) + t_vm.weighing_qty > t_vm.picked_qty)
                            {
                                return (false, "[202]" + _stringLocalizer["data_changed"]);
                            }
                            else
                            {
                                proposedValues["weighing_qty"] = UtilConvert.ObjToInt(databaseValues["weighing_qty"]) + t_vm.weighing_qty;
                                proposedValues["weighing_weight"] = UtilConvert.ObjToInt(databaseValues["weighing_weight"]) + t_vm.weighing_weight;
                                if (UtilConvert.ObjToInt(databaseValues["weighing_qty"]) + t_vm.weighing_qty == UtilConvert.ObjToInt(databaseValues["picked_qty"]))
                                {
                                    proposedValues["dispatch_status"] = 5;
                                }
                                proposedValues["last_update_time"] = now_time;
                            }
                            // Refresh original values to bypass next concurrency check
                            entry.OriginalValues.SetValues(databaseValues);
                        }
                        else
                        {
                            throw new NotSupportedException(_stringLocalizer["try_agin"]);
                        }
                    }
                }
            }
            if (res > 0)
            {
                return (true, _stringLocalizer["operation_success"]);
            }
            else
            {
                return (false, _stringLocalizer["operation_failed"]);
            }
        }

        /// <summary>
        /// dispatchpicklist outbound delivery
        /// </summary>
        /// <param name="viewModels">viewModels</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public async Task<(bool flag, string msg)> Delivery(List<DispatchlistDeliveryViewModel> viewModels, CurrentUser currentUser)
        {
            var DBSet = _dBContext.GetDbSet<DispatchlistEntity>();
            var dispatchlist_id_list = viewModels.Select(t => t.id).ToList();
            var pick_DBSet = _dBContext.GetDbSet<DispatchpicklistEntity>();
            var stock_DBSet = _dBContext.GetDbSet<StockEntity>();
            var entities = await DBSet.Where(t => dispatchlist_id_list.Contains(t.id) && t.tenant_id == currentUser.tenant_id).ToListAsync();
            var now_time = DateTime.Now;
            foreach (var entity in entities)
            {
                if (entity.dispatch_status != 3 && entity.dispatch_status != 4 && entity.dispatch_status != 5)
                {
                    return (false, "[202]" + _stringLocalizer["data_changed"]);
                }
                entity.last_update_time = now_time;
                entity.dispatch_status = 6;
                entity.lock_qty = 0;
                entity.actual_qty = entity.picked_qty;
                entity.intrasit_qty = entity.picked_qty;
            }
            var pick_sql = pick_DBSet.Where(t => dispatchlist_id_list.Contains(t.dispatchlist_id));
            var pick_datas = await pick_sql.ToListAsync();
            var picks_g = pick_sql.AsNoTracking().GroupBy(e => new { e.goods_location_id, e.sku_id, e.goods_owner_id, e.series_number, e.expiry_date, e.price,e.putaway_date }).Select(c => new { c.Key.goods_location_id, c.Key.sku_id, c.Key.goods_owner_id, c.Key.series_number, c.Key.expiry_date, c.Key.price,c.Key.putaway_date, picked_qty = c.Sum(t => t.picked_qty) });
            var picks = await picks_g.ToListAsync();
            var stocks = await (from stock in stock_DBSet
                                where pick_sql.Any(t => t.goods_location_id == stock.goods_location_id && t.sku_id == stock.sku_id && t.goods_owner_id == stock.goods_owner_id && t.series_number == stock.series_number && t.expiry_date == stock.expiry_date && t.price == stock.price && t.putaway_date == stock.putaway_date)
                                select stock).ToListAsync();
            foreach (var pick in picks)
            {
                var s = stocks.FirstOrDefault(t => t.goods_location_id == pick.goods_location_id && t.sku_id == pick.sku_id && t.goods_owner_id == pick.goods_owner_id && t.series_number == pick.series_number && t.expiry_date == pick.expiry_date && t.price == pick.price && t.putaway_date == pick.putaway_date);
                if (s == null)
                {
                    return (false, "[202]" + _stringLocalizer["data_changed"]);
                }
                s.qty -= pick.picked_qty;
                s.last_update_time = now_time;
                stock_DBSet.Update(s);
            }
            foreach (var pick in pick_datas)
            {
                pick.is_update_stock = true;
                pick.last_update_time = now_time;
            }
            var saved = false;
            int res = 0;
            while (!saved)
            {
                try
                {
                    // Attempt to save changes to the database
                    res = await _dBContext.SaveChangesAsync();
                    saved = true;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    foreach (var entry in ex.Entries)
                    {
                        if (entry.Entity is DispatchlistEntity)
                        {
                            var proposedValues = entry.CurrentValues;
                            var databaseValues = entry.GetDatabaseValues();
                            if (UtilConvert.ObjToInt(databaseValues["dispatch_status"]) != 3 && UtilConvert.ObjToInt(databaseValues["dispatch_status"]) != 4 && UtilConvert.ObjToInt(databaseValues["dispatch_status"]) != 5)
                            {
                                return (false, "[202]" + _stringLocalizer["data_changed"]);
                            }
                            proposedValues["last_update_time"] = now_time;
                        }
                        else if (entry.Entity is StockEntity)
                        {
                            var proposedValues = entry.CurrentValues;
                            var databaseValues = entry.GetDatabaseValues();
                            var t_p = picks.FirstOrDefault(t => t.goods_location_id == UtilConvert.ObjToInt(databaseValues["goods_location_id"]) && t.sku_id == UtilConvert.ObjToInt(databaseValues["sku_id"]) && t.goods_owner_id == UtilConvert.ObjToInt(databaseValues["goods_owner_id"]));
                            if (t_p == null)
                            {
                                return (false, "[202]" + _stringLocalizer["data_changed"]);
                            }
                            proposedValues["qty"] = UtilConvert.ObjToInt(databaseValues["qty"]) - t_p.picked_qty;
                            proposedValues["last_update_time"] = now_time;
                            // Refresh original values to bypass next concurrency check
                            entry.OriginalValues.SetValues(databaseValues);
                        }
                        else
                        {
                            throw new NotSupportedException(_stringLocalizer["try_agin"]);
                        }
                    }
                }
            }
            if (res > 0)
            {
                return (true, _stringLocalizer["operation_success"]);
            }
            else
            {
                return (false, _stringLocalizer["operation_failed"]);
            }
        }

        /// <summary>
        ///  set dispatchlist freightfee
        /// </summary>
        /// <param name="viewModels"></param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> SetFreightfee(List<DispatchlistFreightfeeViewModel> viewModels, CurrentUser currentUser)
        {
            var DBSet = _dBContext.GetDbSet<DispatchlistEntity>();
            var dispatchlist_id_list = viewModels.Select(t => t.id).ToList();
            var freightfee_id_list = viewModels.Select(t => t.freightfee_id).Distinct().ToList();
            var entities = await DBSet.Where(t => dispatchlist_id_list.Contains(t.id) && t.tenant_id == currentUser.tenant_id).ToListAsync();
            var freightfees = await _dBContext.GetDbSet<FreightfeeEntity>().Where(t => freightfee_id_list.Contains(t.id)).ToListAsync();
            var now_time = DateTime.Now;
            foreach (var entity in entities)
            {
                var vm = viewModels.FirstOrDefault(t => t.id == entity.id);
                if (vm != null)
                {
                    var freightfee = freightfees.FirstOrDefault(t => t.id == vm.freightfee_id);
                    if (freightfee != null)
                    {
                        entity.last_update_time = now_time;
                        entity.carrier = freightfee.carrier;
                        entity.waybill_no = vm.waybill_no;
                        if (entity.weighing_no != "")
                        {
                            entity.freightfee = entity.weighing_weight * freightfee.price_per_weight > freightfee.min_payment ? entity.weighing_weight * freightfee.price_per_weight : freightfee.min_payment;
                        }
                        else
                        {
                            entity.freightfee = Math.Max(Math.Max(entity.weight * freightfee.price_per_weight, entity.volume * freightfee.price_per_volume), freightfee.min_payment);
                        }
                    }
                }
            }
            var res = await _dBContext.SaveChangesAsync();
            if (res > 0)
            {
                return (true, _stringLocalizer["operation_success"]);
            }
            else
            {
                return (false, _stringLocalizer["operation_failed"]);
            }
        }

        /// <summary>
        /// sign for arrival
        /// </summary>
        /// <param name="viewModels">viewModels</param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> SignForArrival(List<DispatchlistSignViewModel> viewModels, CurrentUser currentUser)
        {
            var DBSet = _dBContext.GetDbSet<DispatchlistEntity>();
            var dispatchlist_id_list = viewModels.Select(t => t.id).ToList();
            var entities = await DBSet.Where(t => dispatchlist_id_list.Contains(t.id) && t.tenant_id == currentUser.tenant_id).ToListAsync();
            var now_time = DateTime.Now;
            foreach (var entity in entities)
            {
                var vm = viewModels.FirstOrDefault(t => t.id == entity.id && t.dispatch_status == entity.dispatch_status);
                if (vm == null)
                {
                    return (false, "[202]" + _stringLocalizer["data_changed"]);
                }
                entity.sign_qty = entity.actual_qty - vm.damage_qty;
                entity.damage_qty = vm.damage_qty;
                entity.last_update_time = now_time;
                entity.dispatch_status = 7;
            }
            var res = await _dBContext.SaveChangesAsync();
            if (res > 0)
            {
                return (true, _stringLocalizer["operation_success"]);
            }
            else
            {
                return (false, _stringLocalizer["operation_failed"]);
            }
        }
    }
}
