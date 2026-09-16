/*
 * date：2022-12-22
 * developer：AMo
 */

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ModernWMS.Core;
using ModernWMS.Core.DBContext;
using ModernWMS.Core.DynamicSearch;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.Core.Services;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.IServices;

namespace ModernWMS.WMS.Services
{
    /// <summary>
    ///  Asnmaster Service (the "Arrival list" region, split out of AsnService)
    /// </summary>
    public class AsnmasterService : BaseService<AsnmasterEntity>, IAsnmasterService
    {
        #region Args

        /// <summary>
        /// The DBContext
        /// </summary>
        private readonly SqlDBContext _dBContext;

        /// <summary>
        /// Localizer Service
        /// </summary>
        private readonly IStringLocalizer<Core.MultiLanguage> _stringLocalizer;

        /// <summary>
        /// functions
        /// </summary>
        private readonly FunctionHelper _functionHelper;

        #endregion Args

        #region constructor

        /// <summary>
        ///Asnmaster  constructor
        /// </summary>
        /// <param name="dBContext">The DBContext</param>
        /// <param name="stringLocalizer">Localizer</param>
        /// <param name="functionHelper">functions</param>
        public AsnmasterService(
            SqlDBContext dBContext
            , IStringLocalizer<Core.MultiLanguage> stringLocalizer
            , FunctionHelper functionHelper
            )
        {
            this._dBContext = dBContext;
            this._stringLocalizer = stringLocalizer;
            this._functionHelper = functionHelper;
        }

        #endregion constructor

        #region Arrival list

        /// <summary>
        /// Arrival list
        /// </summary>
        /// <param name="pageSearch">args</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        public async Task<(List<AsnmasterBothViewModel> data, int totals)> PageAsnmasterAsync(PageSearch pageSearch, CurrentUser currentUser)
        {
            QueryCollection queries = new QueryCollection();
            if (pageSearch.searchObjects.Any())
            {
                pageSearch.searchObjects.ForEach(s =>
                {
                    queries.Add(s);
                });
            }
            Byte asn_status = 255;
            if (pageSearch.sqlTitle.ToLower().Contains("asn_status"))
            {
                asn_status = Convert.ToByte(pageSearch.sqlTitle.Trim().ToLower().Replace("asn_status", "").Replace("：", "").Replace(":", "").Replace("=", ""));
                asn_status = asn_status.Equals(4) ? (Byte)255 : asn_status;
            }
            var Spus = _dBContext.GetDbSet<SpuEntity>();
            var Skus = _dBContext.GetDbSet<SkuEntity>();
            var Asns = _dBContext.GetDbSet<AsnEntity>();
            var Asnmasters = _dBContext.GetDbSet<AsnmasterEntity>();
            var query = from m in Asnmasters.AsNoTracking()
                        where m.tenant_id == currentUser.tenant_id
                        && (asn_status == 255 || m.asn_status == asn_status)
                        select new AsnmasterBothViewModel
                        {
                            id = m.id,
                            asn_no = m.asn_no,
                            asn_batch = m.asn_batch,
                            estimated_arrival_time = m.estimated_arrival_time,
                            asn_status = m.asn_status,
                            weight = m.weight,
                            volume = m.volume,
                            goods_owner_id = m.goods_owner_id,
                            goods_owner_name = m.goods_owner_name,
                            creator = m.creator,
                            create_time = m.create_time,
                            last_update_time = m.last_update_time,
                            detailList = (from a in Asns.AsNoTracking()
                                          join p in Spus.AsNoTracking() on a.spu_id equals p.id
                                          join k in Skus.AsNoTracking() on a.sku_id equals k.id
                                          where a.asnmaster_id == m.id
                                          select new AsnmasterDetailViewModel
                                          {
                                              id = a.id,
                                              asnmaster_id = a.asnmaster_id,
                                              asn_status = a.asn_status,
                                              spu_id = a.spu_id,
                                              spu_code = p.spu_code,
                                              spu_name = p.spu_name,
                                              sku_id = a.sku_id,
                                              sku_code = k.sku_code,
                                              sku_name = k.sku_name,
                                              origin = p.origin,
                                              length_unit = p.length_unit,
                                              volume_unit = p.volume_unit,
                                              weight_unit = p.weight_unit,
                                              asn_qty = a.asn_qty,
                                              actual_qty = a.actual_qty,
                                              weight = a.weight,
                                              volume = a.volume,
                                              supplier_id = a.supplier_id,
                                              supplier_name = a.supplier_name,
                                              is_valid = a.is_valid,
                                              expiry_date = a.expiry_date,
                                              price = a.price,
                                              sorted_qty = a.sorted_qty,
                                          }).ToList()
                        };
            query = query.Where(queries.AsExpression<AsnmasterBothViewModel>());
            int totals = await query.CountAsync();
            var list = await query.OrderByDescending(t => t.last_update_time)
                       .Skip((pageSearch.pageIndex - 1) * pageSearch.pageSize)
                       .Take(pageSearch.pageSize)
                       .ToListAsync();
            return (list, totals);
        }

        /// <summary>
        /// get Arrival list
        /// </summary>
        /// <param name="id"></param>
        /// <param name="currentUser"></param>
        /// <returns></returns>
        public async Task<AsnmasterBothViewModel> GetAsnmasterAsync(int id, CurrentUser currentUser)
        {
            var Spus = _dBContext.GetDbSet<SpuEntity>();
            var Skus = _dBContext.GetDbSet<SkuEntity>();
            var Asns = _dBContext.GetDbSet<AsnEntity>();
            var Asnmasters = _dBContext.GetDbSet<AsnmasterEntity>();
            var query = from m in Asnmasters.AsNoTracking()
                        where m.tenant_id == currentUser.tenant_id
                        && m.id == id
                        select new AsnmasterBothViewModel
                        {
                            id = m.id,
                            asn_no = m.asn_no,
                            asn_batch = m.asn_batch,
                            estimated_arrival_time = m.estimated_arrival_time,
                            asn_status = m.asn_status,
                            weight = m.weight,
                            volume = m.volume,
                            goods_owner_id = m.goods_owner_id,
                            goods_owner_name = m.goods_owner_name,
                            creator = m.creator,
                            create_time = m.create_time,
                            last_update_time = m.last_update_time,
                            detailList = (from a in Asns.AsNoTracking()
                                          join p in Spus.AsNoTracking() on a.spu_id equals p.id
                                          join k in Skus.AsNoTracking() on a.sku_id equals k.id
                                          where a.asnmaster_id == m.id
                                          select new AsnmasterDetailViewModel
                                          {
                                              id = a.id,
                                              asnmaster_id = a.asnmaster_id,
                                              asn_status = a.asn_status,
                                              spu_id = a.spu_id,
                                              spu_code = p.spu_code,
                                              spu_name = p.spu_name,
                                              sku_id = a.sku_id,
                                              sku_code = k.sku_code,
                                              sku_name = k.sku_name,
                                              origin = p.origin,
                                              length_unit = p.length_unit,
                                              volume_unit = p.volume_unit,
                                              weight_unit = p.weight_unit,
                                              asn_qty = a.asn_qty,
                                              actual_qty = a.actual_qty,
                                              weight = a.weight,
                                              volume = a.volume,
                                              supplier_id = a.supplier_id,
                                              supplier_name = a.supplier_name,
                                              is_valid = a.is_valid,
                                              sorted_qty = a.sorted_qty,
                                              expiry_date = a.expiry_date,
                                              price = a.price
                                          }).ToList()
                        };
            var data = await query.FirstOrDefaultAsync();
            return data ?? new AsnmasterBothViewModel();
        }

        /// <summary>
        /// add a new record
        /// </summary>
        /// <param name="viewModel">viewmodel</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        public async Task<(int id, string msg)> AddAsnmasterAsync(AsnmasterBothViewModel viewModel, CurrentUser currentUser)
        {
            var Asns = _dBContext.GetDbSet<AsnEntity>();
            var Asnmasters = _dBContext.GetDbSet<AsnmasterEntity>();
            string asn_no = await _functionHelper.GetFormNoAsync("Asnmaster");
            var entity = new AsnmasterEntity
            {
                id = 0,
                asn_no = asn_no,
                asn_batch = viewModel.asn_batch,
                estimated_arrival_time = viewModel.estimated_arrival_time,
                asn_status = 0,
                weight = viewModel.weight,
                volume = viewModel.volume,
                goods_owner_id = viewModel.goods_owner_id,
                goods_owner_name = viewModel.goods_owner_name,
                creator = currentUser.user_name,
                create_time = DateTime.Now,
                last_update_time = DateTime.Now,
                tenant_id = currentUser.tenant_id,
                detailList = viewModel.detailList.Select(d => new AsnEntity
                {
                    id = 0,
                    asnmaster_id = 0,
                    asn_no = asn_no,
                    asn_status = 0,
                    spu_id = d.spu_id,
                    sku_id = d.sku_id,
                    asn_qty = d.asn_qty,
                    actual_qty = d.actual_qty,
                    weight = d.weight,
                    volume = d.volume,
                    supplier_id = d.supplier_id,
                    supplier_name = d.supplier_name,
                    goods_owner_id = viewModel.goods_owner_id,
                    goods_owner_name = viewModel.goods_owner_name,
                    creator = currentUser.user_name,
                    create_time = DateTime.Now,
                    last_update_time = DateTime.Now,
                    is_valid = true,
                    tenant_id = currentUser.tenant_id,
                    price = d.price
                }).ToList()
            };
            await Asnmasters.AddAsync(entity);
            await _dBContext.SaveChangesAsync();
            if (entity.id > 0)
            {
                return (entity.id, _stringLocalizer["save_success"]);
            }
            else
            {
                return (0, _stringLocalizer["save_failed"]);
            }
        }

        /// <summary>
        /// add a new record
        /// </summary>
        /// <param name="viewModel">viewmodel</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> UpdateAsnmasterAsync(AsnmasterBothViewModel viewModel, CurrentUser currentUser)
        {
            var Asns = _dBContext.GetDbSet<AsnEntity>();
            var Asnmasters = _dBContext.GetDbSet<AsnmasterEntity>();

            var entity = await Asnmasters.Include(t => t.detailList)
                .FirstOrDefaultAsync(t => t.id.Equals(viewModel.id) && t.tenant_id == currentUser.tenant_id);
            if (entity == null)
            {
                return (false, _stringLocalizer["not_exists_entity"]);
            }
            entity.asn_batch = viewModel.asn_batch;
            entity.estimated_arrival_time = viewModel.estimated_arrival_time;
            entity.weight = viewModel.weight;
            entity.volume = viewModel.volume;
            entity.goods_owner_id = viewModel.goods_owner_id;
            entity.goods_owner_name = viewModel.goods_owner_name;
            entity.last_update_time = DateTime.Now;
            if (viewModel.detailList.Any(t => t.id > 0))
            {
                entity.detailList.ForEach(d =>
                {
                    var vm = viewModel.detailList.Where(t => t.id > 0)
                    .FirstOrDefault(t => t.id == d.id);
                    if (vm != null)
                    {
                        d.spu_id = vm.spu_id;
                        d.sku_id = vm.sku_id;
                        d.asn_qty = vm.asn_qty;
                        d.actual_qty = vm.actual_qty;
                        d.weight = vm.weight;
                        d.volume = vm.volume;
                        d.supplier_id = vm.supplier_id;
                        d.supplier_name = vm.supplier_name;
                        d.goods_owner_id = viewModel.goods_owner_id;
                        d.goods_owner_name = viewModel.goods_owner_name;
                        d.last_update_time = DateTime.Now;
                        d.price = vm.price;
                    }
                });
            }
            if (viewModel.detailList.Any(d => d.id == 0))
            {
                var adds = viewModel.detailList.Where(d => d.id == 0)
                    .Select(d => new AsnEntity
                    {
                        id = 0,
                        asnmaster_id = 0,
                        asn_no = viewModel.asn_no,
                        asn_status = 0,
                        spu_id = d.spu_id,
                        sku_id = d.sku_id,
                        asn_qty = d.asn_qty,
                        actual_qty = d.actual_qty,
                        weight = d.weight,
                        volume = d.volume,
                        supplier_id = d.supplier_id,
                        supplier_name = d.supplier_name,
                        goods_owner_id = viewModel.goods_owner_id,
                        goods_owner_name = viewModel.goods_owner_name,
                        creator = currentUser.user_name,
                        create_time = DateTime.Now,
                        last_update_time = DateTime.Now,
                        is_valid = true,
                        tenant_id = currentUser.tenant_id,
                        price = d.price
                    }).ToList();
                entity.detailList.AddRange(adds);
            }
            if (viewModel.detailList.Any(t => t.id < 0))
            {
                var delIds = viewModel.detailList.Where(t => t.id < 0).Select(t => t.id * -1).ToList();
                entity.detailList.RemoveAll(entity => delIds.Contains(entity.id));
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
        /// delete a record
        /// </summary>
        /// <param name="id">id</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> DeleteAsnmasterAsync(int id, CurrentUser currentUser)
        {
            var belongsToTenant = await _dBContext.GetDbSet<AsnmasterEntity>().AsNoTracking().AnyAsync(t => t.id.Equals(id) && t.tenant_id == currentUser.tenant_id);
            if (!belongsToTenant)
            {
                return (false, _stringLocalizer["delete_failed"]);
            }
            var qty = await _dBContext.GetDbSet<AsnEntity>().Where(t => t.asnmaster_id.Equals(id)).ExecuteDeleteAsync();
            qty += await _dBContext.GetDbSet<AsnmasterEntity>().Where(t => t.id.Equals(id) && t.tenant_id == currentUser.tenant_id).ExecuteDeleteAsync();
            if (qty > 0)
            {
                return (true, _stringLocalizer["delete_success"]);
            }
            else
            {
                return (false, _stringLocalizer["delete_failed"]);
            }
        }

        #endregion Arrival list
    }
}
