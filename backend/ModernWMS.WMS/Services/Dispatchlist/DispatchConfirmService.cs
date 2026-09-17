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
    ///  DispatchConfirm Service (confirm/pick sub-flow, split out of DispatchlistService).
    /// </summary>
    public class DispatchConfirmService : BaseService<DispatchlistEntity>, IDispatchConfirmService
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
        /// DispatchConfirm constructor
        /// </summary>
        /// <param name="dBContext">The DBContext</param>
        /// <param name="stringLocalizer">Localizer</param>
        /// <param name="functionHelper">functions</param>
        public DispatchConfirmService(
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

        /// <summary>
        /// get pick list by dispatch_id
        /// </summary>
        /// <param name="dispatch_id">dispatch_id</param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        public async Task<List<DispatchpicklistViewModel>> GetPickListByDispatchID(int dispatch_id, CurrentUser currentUser)
        {
            var datas = await (from dpl in _dBContext.GetDbSet<DispatchpicklistEntity>().AsNoTracking()
                               join dl in _dBContext.GetDbSet<DispatchlistEntity>().AsNoTracking() on dpl.dispatchlist_id equals dl.id
                               join sku in _dBContext.GetDbSet<SkuEntity>().AsNoTracking() on dpl.sku_id equals sku.id
                               join spu in _dBContext.GetDbSet<SpuEntity>().AsNoTracking() on sku.spu_id equals spu.id
                               join owner in _dBContext.GetDbSet<GoodsownerEntity>().AsNoTracking() on dpl.goods_owner_id equals owner.id into o_left
                               from owner in o_left.DefaultIfEmpty()
                               join location in _dBContext.GetDbSet<GoodslocationEntity>().AsNoTracking() on dpl.goods_location_id equals location.id
                               where dpl.dispatchlist_id == dispatch_id && dl.tenant_id == currentUser.tenant_id
                               select new DispatchpicklistViewModel
                               {
                                   id = dpl.id,
                                   dispatchlist_id = dpl.dispatchlist_id,
                                   goods_owner_id = dpl.goods_owner_id,
                                   goods_location_id = dpl.goods_location_id,
                                   sku_id = dpl.sku_id,
                                   pick_qty = dpl.pick_qty,
                                   picked_qty = dpl.picked_qty,
                                   goods_owner_name = owner.goods_owner_name == null ? "" : owner.goods_owner_name,
                                   sku_code = sku.sku_code,
                                   spu_code = spu.spu_code,
                                   spu_description = spu.spu_description,
                                   spu_name = spu.spu_name,
                                   bar_code = sku.bar_code,
                                   location_name = location.location_name,
                                   warehouse_area_name = location.warehouse_area_name,
                                   warehouse_area_property = location.warehouse_area_property,
                                   warehouse_name = location.warehouse_name,
                                   series_number = dpl.series_number,
                                   expiry_date = dpl.expiry_date,
                                   price = dpl.price,
                                   picker = dpl.picker,
                                   picker_id = dpl.picker_id,
                                   putaway_date = dpl.putaway_date,
                               }).ToListAsync();
            return datas;
        }

        /// <summary>
        /// Dispatchlist details with available stock
        /// </summary>
        /// <param name="dispatch_no">dispatch_no</param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        public async Task<List<DispatchlistConfirmDetailViewModel>> ConfirmOrderCheck(string dispatch_no, CurrentUser currentUser)
        {
            var DbSet = _dBContext.GetDbSet<DispatchlistEntity>();
            var dispatchpick_DBSet = _dBContext.GetDbSet<DispatchpicklistEntity>();
            var stock_DbSet = _dBContext.GetDbSet<StockEntity>();
            var asn_DBSet = _dBContext.GetDbSet<AsnEntity>();
            var dispatch_DBSet = _dBContext.GetDbSet<DispatchlistEntity>();
            var sku_DBSet = _dBContext.GetDbSet<SkuEntity>().AsNoTracking();
            var spu_DBSet = _dBContext.GetDbSet<SpuEntity>().AsNoTracking();
            var processdetail_DBSet = _dBContext.GetDbSet<StockProcessDetailEntity>().AsNoTracking();
            var move_DBSet = _dBContext.GetDbSet<StockMoveEntity>();
            var owner_DBSet = _dBContext.GetDbSet<GoodsownerEntity>();
            var location_DBSet = _dBContext.GetDbSet<GoodslocationEntity>();
            var stock_group_datas = from stock in stock_DbSet.AsNoTracking()
                                    join gl in _dBContext.GetDbSet<GoodslocationEntity>().AsNoTracking() on stock.goods_location_id equals gl.id
                                    where stock.tenant_id == currentUser.tenant_id
                                    group stock by new { stock.id, stock.sku_id, stock.goods_location_id, stock.goods_owner_id, stock.series_number, stock.expiry_date, stock.price,stock.putaway_date } into sg
                                    select new
                                    {
                                        stock_id = sg.Key.id,
                                        goods_owner_id = sg.Key.goods_owner_id,
                                        sku_id = sg.Key.sku_id,
                                        goods_location_id = sg.Key.goods_location_id,
                                        series_number = sg.Key.series_number,
                                        sg.Key.expiry_date,
                                        sg.Key.price,
                                        sg.Key.putaway_date,
                                        qty_frozen = sg.Where(t => t.is_freeze == true).Sum(e => e.qty),
                                        qty = sg.Sum(t => t.qty)
                                    };
            var dispatch_group_datas = from dp in DbSet.AsNoTracking()
                                       join dpp in dispatchpick_DBSet.AsNoTracking() on dp.id equals dpp.dispatchlist_id
                                       where dp.dispatch_status > 1 && dp.dispatch_status < 6
                                       group dpp by new { dpp.sku_id, dpp.goods_location_id, dpp.goods_owner_id, dpp.series_number, dpp.expiry_date, dpp.price,dpp.putaway_date } into dg
                                       select new
                                       {
                                           goods_owner_id = dg.Key.goods_owner_id,
                                           sku_id = dg.Key.sku_id,
                                           goods_location_id = dg.Key.goods_location_id,
                                           series_number = dg.Key.series_number,
                                           dg.Key.expiry_date,
                                           dg.Key.price,
                                           dg.Key.putaway_date,
                                           qty_locked = dg.Sum(t => t.pick_qty)
                                       };
            var process_locked_group_datas = from pd in processdetail_DBSet
                                             where pd.is_update_stock == false
                                             group pd by new { pd.sku_id, pd.goods_location_id, pd.goods_owner_id, pd.series_number, pd.expiry_date, pd.price,pd.putaway_date } into pdg
                                             select new
                                             {
                                                 goods_owner_id = pdg.Key.goods_owner_id,
                                                 sku_id = pdg.Key.sku_id,
                                                 goods_location_id = pdg.Key.goods_location_id,
                                                 series_number = pdg.Key.series_number,
                                                 pdg.Key.expiry_date,
                                                 pdg.Key.price,
                                                 pdg.Key.putaway_date,
                                                 qty_locked = pdg.Sum(t => t.qty)
                                             };
            var move_locked_group_datas = from m in move_DBSet.AsNoTracking()
                                          where m.move_status == 0
                                          group m by new { m.sku_id, m.orig_goods_location_id, m.goods_owner_id, m.series_number, m.expiry_date, m.price,m.putaway_date } into mg
                                          select new
                                          {
                                              goods_owner_id = mg.Key.goods_owner_id,
                                              sku_id = mg.Key.sku_id,
                                              goods_location_id = mg.Key.orig_goods_location_id,
                                              series_number = mg.Key.series_number,
                                              mg.Key.expiry_date,
                                              mg.Key.price,
                                              mg.Key.putaway_date,
                                              qty_locked = mg.Sum(t => t.qty)
                                          };
            var datas = await (from dl in DbSet
                               join sg in stock_group_datas on dl.sku_id equals sg.sku_id into sg_left
                               from sg in sg_left.DefaultIfEmpty()
                               join dp in dispatch_group_datas on new { sg.sku_id, sg.goods_location_id, sg.goods_owner_id, sg.series_number, sg.expiry_date, sg.price,sg.putaway_date } equals new { dp.sku_id, dp.goods_location_id, dp.goods_owner_id, dp.series_number, dp.expiry_date, dp.price,dp.putaway_date } into dp_left
                               from dp in dp_left.DefaultIfEmpty()
                               join pl in process_locked_group_datas on new { sg.sku_id, sg.goods_location_id, sg.goods_owner_id, sg.series_number, sg.expiry_date, sg.price,sg.putaway_date } equals new { pl.sku_id, pl.goods_location_id, pl.goods_owner_id, pl.series_number, pl.expiry_date, pl.price,pl.putaway_date } into pl_left
                               from pl in pl_left.DefaultIfEmpty()
                               join m in move_locked_group_datas on new { sg.sku_id, sg.goods_location_id, sg.goods_owner_id, sg.series_number, sg.expiry_date, sg.price,sg.putaway_date } equals new { m.sku_id, m.goods_location_id, m.goods_owner_id, m.series_number, m.expiry_date, m.price,m.putaway_date } into m_left
                               from m in m_left.DefaultIfEmpty()
                               join sku in sku_DBSet on dl.sku_id equals sku.id
                               join spu in spu_DBSet on sku.spu_id equals spu.id
                               join owner in owner_DBSet.AsNoTracking() on sg.goods_owner_id equals owner.id into owner_left
                               from owner in owner_left.DefaultIfEmpty()
                               join gl in location_DBSet.Where(t => t.warehouse_area_property != 5).AsNoTracking() on sg.goods_location_id equals gl.id into gl_left
                               from gl in gl_left.DefaultIfEmpty()
                               where dl.dispatch_no == dispatch_no && dl.tenant_id == currentUser.tenant_id && (dl.dispatch_status == 0 || dl.dispatch_status == 1)
                               select new
                               {
                                   stock_id = sg.stock_id == null ? 0 : sg.stock_id,
                                   goods_owner_name = owner.goods_owner_name == null ? "" : owner.goods_owner_name,
                                   goods_location_id = sg.goods_location_id == null ? 0 : sg.goods_location_id,
                                   goods_owner_id = sg.goods_owner_id == null ? 0 : sg.goods_owner_id,
                                   spu_name = spu.spu_name,
                                   spu_code = spu.spu_code,
                                   sku_code = sku.sku_code,
                                   qty_available = (sg.qty == null ? 0 : sg.qty) - (sg.qty_frozen == null ? 0 : sg.qty_frozen) - (dp.qty_locked == null ? 0 : dp.qty_locked) - (pl.qty_locked == null ? 0 : pl.qty_locked) - (m.qty_locked == null ? 0 : m.qty_locked),
                                   qty = dl.qty,
                                   sku_id = dl.sku_id,
                                   id = dl.id,
                                   spu_description = spu.spu_description,
                                   dispatch_status = dl.dispatch_status,
                                   bar_code = sku.bar_code,
                                   customer_id = dl.customer_id,
                                   customer_name = dl.customer_name,
                                   dispatch_no = dl.dispatch_no,
                                   location_name = gl.location_name == null ? "" : gl.location_name,
                                   warehouse_area_name = gl.warehouse_area_name == null ? "" : gl.warehouse_area_name,
                                   warehouse_name = gl.warehouse_name == null ? "" : gl.warehouse_name,
                                   series_number = sg.series_number==null ? "":sg.series_number,
                                   expiry_date = sg.expiry_date==null ? UtilConvert.MinDate : sg.expiry_date,
                                   price = sg.price == null ? 0:sg.price,
                                    putaway_date = sg.putaway_date == null ? UtilConvert.MinDate:sg.putaway_date,
                               }).ToListAsync();
            var res = (from d in datas
                       group d by new
                       {
                           d.spu_name,
                           d.spu_code,
                           d.sku_code,
                           d.qty,
                           d.sku_id,
                           d.id,
                           d.spu_description,
                           d.dispatch_status,
                           d.bar_code,
                           d.customer_id,
                           d.customer_name,
                           d.dispatch_no,
                       }
                       into dg
                       select new DispatchlistConfirmDetailViewModel
                       {
                           dispatchlist_id = dg.Key.id,
                           sku_id = dg.Key.sku_id,
                           dispatch_no = dg.Key.dispatch_no,
                           sku_code = dg.Key.sku_code,
                           spu_code = dg.Key.spu_code,
                           dispatch_status = dg.Key.dispatch_status,
                           spu_description = dg.Key.spu_description,
                           spu_name = dg.Key.spu_name,
                           bar_code = dg.Key.bar_code,
                           customer_id = dg.Key.customer_id,
                           customer_name = dg.Key.customer_name,
                           qty = dg.Key.qty,
                           qty_available = dg.Sum(t => t.qty_available),
                           confirm = dg.Key.qty > dg.Sum(t => t.qty_available) ? false : true
                       }).ToList();
            foreach (var r in res)
            {
                var picklist = (from d in datas.Where(t => t.sku_id == r.sku_id && t.stock_id > 0).OrderBy(o => o.qty_available)
                                select new DispatchlistConfirmPickDetailViewModel
                                {
                                    stock_id = d.stock_id,
                                    dispatchlist_id = r.dispatchlist_id,
                                    goods_location_id = d.goods_location_id,
                                    qty_available = d.qty_available,
                                    goods_owner_id = d.goods_owner_id,
                                    goods_owner_name = d.goods_owner_name,
                                    location_name = d.location_name,
                                    warehouse_area_name = d.warehouse_area_name,
                                    warehouse_name = d.warehouse_name,
                                    pick_qty = 0,
                                    series_number = d.series_number,
                                    expiry_date = d.expiry_date,
                                    price = d.price,
                                    putaway_date=d.putaway_date,
                                }
                              ).OrderByDescending(o => o.qty_available).ToList();
                int pick_qty = 0;
                foreach (var pick in picklist)
                {
                    if (pick_qty >= r.qty)
                    {
                        break;
                    }
                    pick.pick_qty = (r.qty <= (pick_qty + pick.qty_available)) ? (r.qty - pick_qty) : pick.qty_available;
                    pick_qty += pick.pick_qty;
                }
                r.pick_list = picklist.Where(t => t.qty_available > 0).ToList();
            }

            return res;
        }

        /// <summary>
        ///  Confirm orders and create  dispatchpicklist
        /// </summary>
        /// <param name="viewModels">viewModels</param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> ConfirmOrder(List<DispatchlistConfirmDetailViewModel> viewModels, CurrentUser currentUser)
        {
            var DBSet = _dBContext.GetDbSet<DispatchlistEntity>();
            var dispatchlist_id_list = viewModels.Select(t => t.dispatchlist_id).ToList();
            var dispatchlist_datas = await DBSet.Where(t => dispatchlist_id_list.Contains(t.id) && t.tenant_id == currentUser.tenant_id).ToListAsync();
            var pick_DBSet = _dBContext.GetDbSet<DispatchpicklistEntity>();
            var stock_DBSet = _dBContext.GetDbSet<StockEntity>();
            var pick_datas = new List<DispatchpicklistEntity>();
            var stock_id_list = new List<int>();
            var processdetail_DBSet = _dBContext.GetDbSet<StockProcessDetailEntity>().AsNoTracking();
            var move_DBSet = _dBContext.GetDbSet<StockMoveEntity>();
            var new_dispatchlists = new List<DispatchlistEntity>();
            var topick_viewmodels = new List<StockViewModel>();
            var sku_id_list = viewModels.Select(t => t.sku_id).ToList();
            var now_time = DateTime.Now;
            foreach (var vm in viewModels.Where(t => t.confirm == true).ToList())
            {
                stock_id_list.AddRange(vm.pick_list.Where(t => t.pick_qty > 0).Select(t => t.stock_id).ToList());
            }

            foreach (var vm in viewModels)
            {
                var d = dispatchlist_datas.Where(t => t.id == vm.dispatchlist_id).FirstOrDefault();
                if (d == null)
                {
                    return (false, "[202]" + _stringLocalizer["data_changed"]);
                }
                if (vm.confirm == true)
                {
                    d.dispatch_status = 2;
                    d.last_update_time = now_time;
                    d.lock_qty = vm.pick_list.Sum(t => t.pick_qty);
                    foreach (var p in vm.pick_list.Where(t => t.pick_qty > 0).ToList())
                    {
                        pick_datas.Add(new DispatchpicklistEntity
                        {
                            sku_id = vm.sku_id,
                            is_update_stock = false,
                            dispatchlist_id = p.dispatchlist_id,
                            goods_location_id = p.goods_location_id,
                            goods_owner_id = p.goods_owner_id,
                            last_update_time = now_time,
                            series_number = p.series_number,
                            expiry_date = p.expiry_date,
                            price = p.price,
                            pick_qty = p.pick_qty,
                            putaway_date = p.putaway_date,
                        });
                        topick_viewmodels.Add(new StockViewModel { id = p.stock_id, qty = p.pick_qty });
                    }
                    if (d.lock_qty < d.qty)
                    {
                        new_dispatchlists.Add(new DispatchlistEntity
                        {
                            sku_id = vm.sku_id,
                            dispatch_status = 1,
                            qty = d.qty - d.lock_qty,
                            tenant_id = currentUser.tenant_id,
                            customer_id = d.customer_id,
                            customer_name = d.customer_name,
                        });
                        d.qty = d.lock_qty;
                    }
                }
                else
                {
                    new_dispatchlists.Add(new DispatchlistEntity
                    {
                        sku_id = vm.sku_id,
                        dispatch_status = 1,
                        qty = vm.qty,
                        tenant_id = currentUser.tenant_id,
                        customer_id = d.customer_id,
                        customer_name = d.customer_name,
                    });
                    DBSet.Remove(d);
                }
            }
            var stock_group_datas = from stock in stock_DBSet.AsNoTracking()
                                    where stock_id_list.Contains(stock.id)
                                    group stock by new { stock.id, stock.sku_id, stock.goods_location_id, stock.goods_owner_id, stock.series_number, stock.expiry_date, stock.price,stock.putaway_date } into sg
                                    select new
                                    {
                                        stock_id = sg.Key.id,
                                        goods_owner_id = sg.Key.goods_owner_id,
                                        sku_id = sg.Key.sku_id,
                                        goods_location_id = sg.Key.goods_location_id,
                                        series_number = sg.Key.series_number,
                                        sg.Key.expiry_date,
                                        sg.Key.price,
                                        sg.Key.putaway_date,
                                        qty_frozen = sg.Where(t => t.is_freeze == true).Sum(e => e.qty),
                                        qty = sg.Sum(t => t.qty)
                                    };
            var dispatch_group_datas = from dp in DBSet.AsNoTracking()
                                       join dpp in pick_DBSet.AsNoTracking() on dp.id equals dpp.dispatchlist_id
                                       where dp.dispatch_status > 1 && dp.dispatch_status < 6
                                       group dpp by new { dpp.sku_id, dpp.goods_location_id, dpp.goods_owner_id, dpp.series_number, dpp.expiry_date, dpp.price,dpp.putaway_date } into dg
                                       select new
                                       {
                                           goods_owner_id = dg.Key.goods_owner_id,
                                           sku_id = dg.Key.sku_id,
                                           goods_location_id = dg.Key.goods_location_id,
                                           series_number = dg.Key.series_number,
                                           dg.Key.expiry_date,
                                           dg.Key.price,
                                           dg.Key.putaway_date,
                                           qty_locked = dg.Sum(t => t.pick_qty)
                                       };
            var process_locked_group_datas = from pd in processdetail_DBSet
                                             where pd.is_update_stock == false
                                             group pd by new { pd.sku_id, pd.goods_location_id, pd.goods_owner_id, pd.series_number, pd.expiry_date, pd.price,pd.putaway_date } into pdg
                                             select new
                                             {
                                                 goods_owner_id = pdg.Key.goods_owner_id,
                                                 sku_id = pdg.Key.sku_id,
                                                 goods_location_id = pdg.Key.goods_location_id,
                                                 series_number = pdg.Key.series_number,
                                                 pdg.Key.expiry_date,
                                                 pdg.Key.price,
                                                 pdg.Key.putaway_date,
                                                 qty_locked = pdg.Sum(t => t.qty)
                                             };
            var move_locked_group_datas = from m in move_DBSet.AsNoTracking()
                                          where m.move_status == 0
                                          group m by new { m.sku_id, m.orig_goods_location_id, m.goods_owner_id, m.series_number, m.expiry_date, m.price,m.putaway_date } into mg
                                          select new
                                          {
                                              goods_owner_id = mg.Key.goods_owner_id,
                                              sku_id = mg.Key.sku_id,
                                              goods_location_id = mg.Key.orig_goods_location_id,
                                              series_number = mg.Key.series_number,
                                              mg.Key.expiry_date,
                                              mg.Key.price,
                                              mg.Key.putaway_date,
                                              qty_locked = mg.Sum(t => t.qty)
                                          };
            var stock_datas = await (from sg in stock_group_datas
                                     join dp in dispatch_group_datas on new { sg.sku_id, sg.goods_location_id, sg.goods_owner_id, sg.series_number, sg.expiry_date, sg.price,sg.putaway_date } equals new { dp.sku_id, dp.goods_location_id, dp.goods_owner_id, dp.series_number, dp.expiry_date, dp.price,dp.putaway_date } into dp_left
                                     from dp in dp_left.DefaultIfEmpty()
                                     join pl in process_locked_group_datas on new { sg.sku_id, sg.goods_location_id, sg.goods_owner_id, sg.series_number, sg.expiry_date, sg.price,sg.putaway_date } equals new { pl.sku_id, pl.goods_location_id, pl.goods_owner_id, pl.series_number, pl.expiry_date, pl.price,pl.putaway_date } into pl_left
                                     from pl in pl_left.DefaultIfEmpty()
                                     join m in move_locked_group_datas on new { sg.sku_id, sg.goods_location_id, sg.goods_owner_id, sg.series_number, sg.expiry_date, sg.price,sg.putaway_date } equals new { m.sku_id, m.goods_location_id, m.goods_owner_id, m.series_number, m.expiry_date, m.price,m.putaway_date } into m_left
                                     from m in m_left.DefaultIfEmpty()
                                     select new
                                     {
                                         stock_id = sg.stock_id,
                                         qty_available = (sg.qty == null ? 0 : sg.qty) - (sg.qty_frozen == null ? 0 : sg.qty_frozen) - (dp.qty_locked == null ? 0 : dp.qty_locked) - (pl.qty_locked == null ? 0 : pl.qty_locked) - (m.qty_locked == null ? 0 : m.qty_locked),
                                     }).ToListAsync();
            var if_not_stock = (from tp in topick_viewmodels
                                join s in stock_datas on tp.id equals s.stock_id
                                where tp.qty > s.qty_available
                                select tp).Any();
            if (if_not_stock)
            {
                return (false, "[202]" + _stringLocalizer["data_changed"]);
            }
            await pick_DBSet.AddRangeAsync(pick_datas);
            var dispatch_no = await _functionHelper.GetFormNoAsync("Dispatchlist");
            var sku_datas = await _dBContext.GetDbSet<SkuEntity>().Where(t => sku_id_list.Contains(t.id)).ToListAsync();
            foreach (var nd in new_dispatchlists)
            {
                nd.dispatch_no = dispatch_no;
                nd.creator = currentUser.user_name;
                nd.create_time = DateTime.Now;
                var sku = sku_datas.FirstOrDefault(e => e.id == nd.sku_id);
                if (sku != null)
                {
                    nd.weight = nd.qty * sku.weight;
                    nd.volume = nd.qty * sku.volume;
                }
            };
            await DBSet.AddRangeAsync(new_dispatchlists);
            var qty = await _dBContext.SaveChangesAsync();

            return (true, _stringLocalizer["operation_success"]);
        }

        /// <summary>
        ///  cancel order opration
        /// </summary>
        /// <param name="viewModel">viewmodel</param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> CancelOrderOpration(CancelOrderOprationViewModel viewModel, CurrentUser currentUser)
        {
            var DBSet = _dBContext.GetDbSet<DispatchlistEntity>();
            var pick_DBSet = _dBContext.GetDbSet<DispatchpicklistEntity>();
            var entities = await DBSet.Where(t => t.dispatch_no == viewModel.dispatch_no && t.tenant_id == currentUser.tenant_id && t.dispatch_status == viewModel.dispatch_status).ToListAsync();
            if (entities.Count == 0)
            {
                return (false, _stringLocalizer["status_changed"]);
            }
            var now_time = DateTime.Now;
            var dispatch_id_list = entities.Select(t => t.id).ToList();
            var pick_entities = await pick_DBSet.Where(t => dispatch_id_list.Contains(t.dispatchlist_id)).ToListAsync();
            if (viewModel.dispatch_status == 3)
            {
                foreach (var pick in pick_entities)
                {
                    pick.picked_qty = 0;
                    pick.last_update_time = now_time;
                }
                foreach (var entity in entities)
                {
                    entity.picked_qty = 0;
                    entity.last_update_time = now_time;
                    entity.dispatch_status = 2;
                }
            }
            else if (viewModel.dispatch_status == 2)
            {
                pick_DBSet.RemoveRange(pick_entities);
                foreach (var entity in entities)
                {
                    entity.lock_qty = 0;
                    entity.last_update_time = now_time;
                    entity.dispatch_status = 1;
                }
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
                            if (UtilConvert.ObjToInt(databaseValues["dispatch_status"]) != viewModel.dispatch_status)
                                return (false, "[202]" + _stringLocalizer["data_changed"]);
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
        /// cancel dispatchlist detail opration
        /// </summary>
        /// <param name="id">dispatchlist_id</param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> CancelDispatchlistDetailOpration(int id, CurrentUser currentUser)
        {
            var DBSet = _dBContext.GetDbSet<DispatchlistEntity>();
            var entity = await DBSet.Where(t => t.id == id && t.tenant_id == currentUser.tenant_id).FirstOrDefaultAsync();
            var now_time = DateTime.Now;
            if (entity == null)
            {
                return (false, _stringLocalizer["not_exists_entity"]);
            }
            if (entity.dispatch_status == 4)
            {
                if (entity.weighing_no == "")
                {
                    entity.dispatch_status = 3;
                }
                else
                {
                    entity.dispatch_status = 5;
                }
                entity.package_no = "";
                entity.package_qty = 0;
                entity.package_time = UtilConvert.MinDate;
                entity.package_person = "";
            }
            else if (entity.dispatch_status == 5)
            {
                if (entity.package_no == "")
                {
                    entity.dispatch_status = 3;
                }
                else
                {
                    entity.dispatch_status = 4;
                }
                entity.weighing_no = "";
                entity.weighing_qty = 0;
                entity.weighing_weight = 0;
                entity.weighing_person = "";
            }
            else
            {
                return (false, _stringLocalizer["status_changed"]);
            }
            entity.last_update_time = now_time;
            var qty = await _dBContext.SaveChangesAsync();
            if (qty > 0)
            {
                return (true, _stringLocalizer["operation_success"]);
            }
            else
            {
                return (false, _stringLocalizer["operation_failed"]);
            }
        }

        /// <summary>
        /// confirm dispatchpicklist picked by dispatch_no
        /// </summary>
        /// <param name="dispatch_no">dispatch_no</param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> ConfirmPickByDispatchNo(string dispatch_no, CurrentUser currentUser)
        {
            var DBSet = _dBContext.GetDbSet<DispatchlistEntity>();
            var pick_DBSet = _dBContext.GetDbSet<DispatchpicklistEntity>();
            var entities = await DBSet.Where(t => t.dispatch_status == 2 && t.dispatch_no == dispatch_no && t.tenant_id == currentUser.tenant_id).ToListAsync();
            var dispatchlist_id_list = entities.Select(t => t.id).ToList();
            var pick_datas = await pick_DBSet.Where(t => dispatchlist_id_list.Contains(t.dispatchlist_id)).ToListAsync();
            var now_time = DateTime.Now;
            entities.ForEach(t =>
            {
                t.picked_qty = t.lock_qty;
                t.dispatch_status = 3;
                t.last_update_time = now_time;
                t.pick_checker = currentUser.user_name;
                t.pick_checker_id = currentUser.user_id;
            });
            pick_datas.ForEach(t =>
            {
                t.picked_qty = t.pick_qty;
                t.last_update_time = now_time;
            });
            var qty = await _dBContext.SaveChangesAsync();
            if (qty > 0)
            {
                return (true, _stringLocalizer["operation_success"]);
            }
            else
            {
                return (false, _stringLocalizer["operation_failed"]);
            }
        }

        /// <summary>
        /// confirm pick detail
        /// </summary>
        /// <param name="picklist_id">dispatch list pick detail id</param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> ConfirmPickDetail(List<int> picklist_id, CurrentUser currentUser)
        {
            var DBSet = _dBContext.GetDbSet<DispatchlistEntity>();
            var pick_DBSet = _dBContext.GetDbSet<DispatchpicklistEntity>();
            var pick_datas = await (from p in pick_DBSet
                                    join d in DBSet on p.dispatchlist_id equals d.id
                                    where picklist_id.Contains(p.id) && d.tenant_id == currentUser.tenant_id
                                    select p).ToListAsync();
            if (pick_datas.Any(t=>t.picker_id > 0) || pick_datas.Any(t=>t.picked_qty>0))
            {
                return (false, _stringLocalizer["data_changed"]);
            }
            pick_datas.ForEach(t=>
            {
                t.picker = currentUser.user_name;
                t.picker_id = currentUser.user_id;
            });
            var qty = await _dBContext.SaveChangesAsync();
            if (qty > 0)
            {
                return (true, _stringLocalizer["operation_success"]);
            }
            else
            {
                return (false, _stringLocalizer["operation_failed"]);
            }
        }

        /// <summary>
        /// cancel confirm pick detail
        /// </summary>
        /// <param name="picklist_id">dispatch list pick detail id</param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> CancelConfirmPickDetail(List<int> picklist_id, CurrentUser currentUser)
        {
            var DBSet = _dBContext.GetDbSet<DispatchlistEntity>();
            var pick_DBSet = _dBContext.GetDbSet<DispatchpicklistEntity>();
            var pick_datas = await (from p in pick_DBSet
                                    join d in DBSet on p.dispatchlist_id equals d.id
                                    where picklist_id.Contains(p.id) && d.tenant_id == currentUser.tenant_id
                                    select p).ToListAsync();
            if (pick_datas.Any(t =>t.picker_id == 0) || pick_datas.Any(t => t.picked_qty > 0))
            {
                return (false, _stringLocalizer["data_changed"]);
            }
            pick_datas.ForEach(t =>
            {
                t.picker = "";
                t.picker_id = 0;
            });
            var qty = await _dBContext.SaveChangesAsync();
            if (qty > 0)
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
