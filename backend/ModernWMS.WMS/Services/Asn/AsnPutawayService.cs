/*
 * date：2022-12-22
 * developer：AMo
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
    ///  Asn putaway Service (part of the "Flow Api" region, split out of AsnService).
    /// The only part of the Asn workflow that writes StockEntity.
    /// </summary>
    public class AsnPutawayService : BaseService<AsnEntity>, IAsnPutawayService
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

        #endregion Args

        #region constructor

        /// <summary>
        ///AsnPutaway  constructor
        /// </summary>
        /// <param name="dBContext">The DBContext</param>
        /// <param name="stringLocalizer">Localizer</param>
        public AsnPutawayService(
            SqlDBContext dBContext
            , IStringLocalizer<Core.MultiLanguage> stringLocalizer
            )
        {
            this._dBContext = dBContext;
            this._stringLocalizer = stringLocalizer;
        }

        #endregion constructor

        /// <summary>
        /// get pending putaway data by asn_id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<List<AsnPendingPutawayViewModel>> GetPendingPutawayDataAsync(int id, CurrentUser currentUser)
        {
            var Asns = _dBContext.GetDbSet<AsnEntity>();
            var Asnsorts = _dBContext.GetDbSet<AsnSortEntity>();

            var data = await (from m in Asns.AsNoTracking()
                              join s in Asnsorts.AsNoTracking() on m.id equals s.asn_id
                              where m.id == id && s.putaway_qty < s.sorted_qty && m.tenant_id == currentUser.tenant_id
                              group new { m, s } by new { m.id, m.goods_owner_id, m.goods_owner_name, s.series_number }
                       into g
                              select new AsnPendingPutawayViewModel
                              {
                                  asn_id = g.Key.id,
                                  goods_owner_id = g.Key.goods_owner_id,
                                  goods_owner_name = g.Key.goods_owner_name,
                                  series_number = g.Key.series_number,
                                  sorted_qty = g.Sum(o => o.s.sorted_qty - o.s.putaway_qty)
                              }).ToListAsync();
            return data;
        }

        /// <summary>
        /// PutAway
        /// </summary>
        /// <param name="viewModels">args</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> PutAwayAsync(List<AsnPutAwayInputViewModel> viewModels, CurrentUser currentUser)
        {
            viewModels.RemoveAll(v => v.putaway_qty < 1);
            if (viewModels.Any(t => t.goods_location_id == 0))
            {
                return (false, "[202]" + string.Format(_stringLocalizer["Required"], _stringLocalizer["location_name"]));
            }
            var Asns = _dBContext.GetDbSet<AsnEntity>();
            var Goodslocations = _dBContext.GetDbSet<GoodsLocationEntity>();
            var Stocks = _dBContext.GetDbSet<StockEntity>();
            var Asnsorts = _dBContext.GetDbSet<AsnSortEntity>();

            var LocationIdList = viewModels.Where(v => v.goods_location_id > 0)
                                           .Select(v => v.goods_location_id)
                                           .Distinct().ToList();

            var Locations = await Goodslocations.Where(t => LocationIdList.Contains(t.id) && t.tenant_id == currentUser.tenant_id)
                                                .ToListAsync();
            if (!Locations.Any() || LocationIdList.Count != Locations.Count)
            {
                return (false, "[202]" + string.Format(_stringLocalizer["Required"], _stringLocalizer["location_name"]));
            }
            int sumPutawayQty = viewModels.Sum(v => v.putaway_qty);
            var entity = await Asns.FirstOrDefaultAsync(t => t.id == viewModels[0].asn_id && t.tenant_id == currentUser.tenant_id);
            if (entity == null)
            {
                return (false, "[202]" + _stringLocalizer["not_exists_entity"]);
            }
            else if (entity.asn_status != 3)
            {
                return (false, "[202]" + $"{entity.asn_no}{_stringLocalizer["ASN_Status_Is_Not_Sorted"]}");
            }
            else if (entity.actual_qty + sumPutawayQty > entity.sorted_qty)
            {
                return (false, "[202]" + $"{entity.asn_no}{_stringLocalizer["ASN_Total_PutAway_Qty_Greater_Than_Sorted_Qty"]}");
            }
            entity.actual_qty += sumPutawayQty;
            if (entity.actual_qty.Equals(entity.sorted_qty))
            {
                entity.asn_status = 4;
            }
            entity.last_update_time = DateTime.Now;
            // expiry_date
            var expiry_date = entity.expiry_date;

            // 获取已上架数小于分拣数的分拣记录
            var sortEntities = await Asnsorts.Where(t => t.asn_id == viewModels[0].asn_id && t.sorted_qty > t.putaway_qty).ToListAsync();

            foreach (var viewModel in viewModels)
            {
                // 根据sn码，将本次上架数量反写到分拣记录中。如果sn码是空的，则分摊进去
                var sortList = sortEntities.Where(s => s.series_number == viewModel.series_number).ToList();
                if (sortList.Any())
                {
                    int left_putaway_qty = viewModel.putaway_qty;
                    sortList.ForEach(s =>
                    {
                        if (left_putaway_qty > 0)
                        {
                            int can_putaway_qty = s.sorted_qty - s.putaway_qty;
                            if (left_putaway_qty > can_putaway_qty)
                            {
                                s.putaway_qty += can_putaway_qty;
                                left_putaway_qty -= can_putaway_qty;
                            }
                            else
                            {
                                s.putaway_qty += left_putaway_qty;
                                left_putaway_qty = 0;
                            }
                        }
                    });
                }

                var Location = Locations.FirstOrDefault(t => t.id == viewModel.goods_location_id);
                if (Location != null && Location.warehouse_area_property.Equals(5))
                {
                    entity.damage_qty += viewModel.putaway_qty;
                }
                DateTime putaway_date = DateTime.Now.ToString("yyyy-MM-dd").ObjToDate();
                // 2024年3月14日 09:40:25 增加单价
                var stockEntity = await Stocks.FirstOrDefaultAsync(t => t.sku_id.Equals(entity.sku_id)
                                                                              && t.goods_location_id.Equals(viewModel.goods_location_id)
                                                                              && t.goods_owner_id.Equals(viewModel.goods_owner_id)
                                                                              && t.series_number.Equals(viewModel.series_number)
                                                                              && t.expiry_date.Equals(expiry_date)
                                                                              && t.price.Equals(entity.price)
                                                                              && t.putaway_date.Equals(putaway_date)
                                                                              );
                if (stockEntity == null)
                {
                    stockEntity = new StockEntity
                    {
                        sku_id = entity.sku_id,
                        goods_location_id = viewModel.goods_location_id,
                        goods_owner_id = entity.goods_owner_id,
                        series_number = viewModel.series_number,
                        qty = viewModel.putaway_qty,
                        is_freeze = false,
                        last_update_time = DateTime.Now,
                        tenant_id = currentUser.tenant_id,
                        expiry_date = expiry_date,
                        price = entity.price,
                        putaway_date = putaway_date,
                        id = 0
                    };
                    await Stocks.AddAsync(stockEntity);
                }
                else
                {
                    stockEntity.qty += viewModel.putaway_qty;
                    stockEntity.last_update_time = DateTime.Now;
                }
            }
            var qty = await _dBContext.SaveChangesAsync();
            if (qty > 0)
            {
                return (true, _stringLocalizer["putaway_success"]);
            }
            else
            {
                return (false, _stringLocalizer["putaway_failed"]);
            }
        }
    }
}
