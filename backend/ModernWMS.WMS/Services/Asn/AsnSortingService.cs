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
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.IServices;

namespace ModernWMS.WMS.Services
{
    /// <summary>
    ///  Asn sorting Service (part of the "Flow Api" region, split out of AsnService)
    /// </summary>
    public class AsnSortingService : BaseService<AsnsortEntity>, IAsnSortingService
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
        ///AsnSorting  constructor
        /// </summary>
        /// <param name="dBContext">The DBContext</param>
        /// <param name="stringLocalizer">Localizer</param>
        /// <param name="functionHelper">functions</param>
        public AsnSortingService(
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

        /// <summary>
        /// sorting， add a new asnsort record and update asn sorted_qty
        /// </summary>
        /// <param name="viewModels">args</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> SortingAsync(List<AsnsortInputViewModel> viewModels, CurrentUser currentUser)
        {
            var Asns = _dBContext.GetDbSet<AsnEntity>();
            var Asnsorts = _dBContext.GetDbSet<AsnsortEntity>();
            var idList = viewModels.Select(t => t.asn_id).ToList().Distinct().ToList();
            var entities = await Asns.Where(t => idList.Contains(t.id) && t.tenant_id == currentUser.tenant_id).ToListAsync();

            if (!entities.Any())
            {
                return (false, "[202]" + _stringLocalizer["not_exists_entity"]);
            }
            else if (entities.Any(t => t.asn_status != 2))
            {
                return (false, "[202]" + $"{_stringLocalizer["ASN_Status_Is_Not_Pre_Sort"]}");
            }
            var models = viewModels.Where(v => entities.Select(e => e.id).ToList().Contains(v.asn_id)).ToList();
            List<AsnsortEntity> sortEntities = new List<AsnsortEntity>();
            foreach (var v in models)
            {
                if (v.sorted_qty > 1 && v.is_auto_num)
                {
                    List<string> snlist = await _functionHelper.GetFormNoListAsync("Asnsort", v.sorted_qty, currentUser.tenant_id, "sn");
                    for (int i = 0; i < v.sorted_qty; i++)
                    {
                        sortEntities.Add(new AsnsortEntity
                        {
                            id = 0,
                            asn_id = v.asn_id,
                            sorted_qty = 1,
                            series_number = snlist[i],
                            create_time = DateTime.Now,
                            creator = currentUser.user_name,
                            is_valid = true,
                            last_update_time = DateTime.Now,
                            tenant_id = currentUser.tenant_id
                        });
                    }
                }
                else
                {
                    string sn = await _functionHelper.GetFormNoAsync("Asnsort", "sn");
                    sortEntities.Add(new AsnsortEntity
                    {
                        id = 0,
                        asn_id = v.asn_id,
                        sorted_qty = v.sorted_qty,
                        series_number = sn,
                        create_time = DateTime.Now,
                        creator = currentUser.user_name,
                        is_valid = true,
                        last_update_time = DateTime.Now,
                        tenant_id = currentUser.tenant_id
                    });
                }
            }
            await Asnsorts.AddRangeAsync(sortEntities);

            entities.ForEach(e =>
            {
                int sum_sorted_qty = viewModels.Where(t => t.asn_id.Equals(e.id)).Sum(v => v.sorted_qty);
                var expiry_date = viewModels.Where(t => t.asn_id.Equals(e.id)).Select(v => v.expiry_date).FirstOrDefault();
                e.sorted_qty += sum_sorted_qty;
                e.last_update_time = DateTime.Now;
                e.expiry_date = expiry_date;
            });
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
        /// get asnsorts list by asn_id
        /// </summary>
        /// <param name="asn_id">asn id</param>
        /// <returns></returns>
        public async Task<List<AsnsortViewModel>> GetAsnsortsAsync(int asn_id, CurrentUser currentUser)
        {
            var Asnsorts = _dBContext.GetDbSet<AsnsortEntity>();
            var asns = _dBContext.Set<AsnEntity>().AsNoTracking();

            var data = await (from m in asns
                              join d in Asnsorts on m.id equals d.asn_id
                              where m.id == asn_id && m.tenant_id == currentUser.tenant_id
                              select new AsnsortViewModel
                              {
                                  id = d.id,
                                  asn_id = asn_id,
                                  sorted_qty = d.sorted_qty,
                                  series_number = d.series_number,
                                  putaway_qty = d.putaway_qty,
                                  expiry_date = m.expiry_date,
                                  creator = d.creator,
                                  create_time = d.create_time,
                                  last_update_time = d.last_update_time,
                                  is_valid = d.is_valid,
                                  tenant_id = d.tenant_id
                              }).ToListAsync();
            if (data != null && data.Count > 0)
            {
                return data;
            }
            else
            {
                return new List<AsnsortViewModel>();
            }
        }

        /// <summary>
        /// update or delete asnsorts data
        /// </summary>
        /// <param name="entities">data</param>
        /// <param name="user">CurrentUser</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> ModifyAsnsortsAsync(List<AsnsortEntity> entities, CurrentUser user)
        {
            var Asnsorts = _dBContext.GetDbSet<AsnsortEntity>();
            if (entities.Any(t => t.id < 0 || t.sorted_qty == 0))
            {
                var delIDList = entities.Where(t => t.id < 0).Select(t => Math.Abs(t.id)).ToList();
                await Asnsorts.Where(t => delIDList.Contains(t.id) && t.tenant_id == user.tenant_id).ExecuteDeleteAsync();
            }
            var updateEntities = entities.Where(t => t.id > 0 && t.sorted_qty > 0).ToList();
            if (updateEntities.Any())
            {
                var updateIds = updateEntities.Select(t => t.id).ToList();
                var allowedIds = await Asnsorts.AsNoTracking()
                    .Where(t => updateIds.Contains(t.id) && t.tenant_id == user.tenant_id)
                    .Select(t => t.id).ToListAsync();
                updateEntities = updateEntities.Where(t => allowedIds.Contains(t.id)).ToList();
                updateEntities.ForEach(t =>
                {
                    t.last_update_time = DateTime.Now;
                    t.is_valid = true;
                });
                Asnsorts.UpdateRange(updateEntities);
            }

            var qty = await _dBContext.SaveChangesAsync();
            if (qty >= 0)
            {
                var Asns = _dBContext.GetDbSet<AsnEntity>();
                var asnids = entities.Select(t => t.asn_id).Distinct().ToList();

                var sumQty = await Asnsorts.AsNoTracking()
                    .Where(t => asnids.Contains(t.asn_id))
                    .GroupBy(t => t.asn_id)
                    .Select(g => new
                    {
                        asn_id = g.Key,
                        sorted_qty = g.Sum(o => o.sorted_qty)
                    }).ToListAsync();
                var asnEntities = await Asns.Where(t => asnids.Contains(t.id) && t.tenant_id == user.tenant_id).ToListAsync();
                if (asnEntities.Any())
                {
                    asnEntities.ForEach(e =>
                    {
                        var s = sumQty.FirstOrDefault(t => t.asn_id == e.id);
                        if (s != null)
                        {
                            e.sorted_qty = s.sorted_qty;
                        }
                        else
                        {
                            e.sorted_qty = 0;
                        }
                    });
                    await _dBContext.SaveChangesAsync();
                }
                return (true, _stringLocalizer["sorted_success"]);
            }
            else
            {
                return (false, _stringLocalizer["sorted_failed"]);
            }
        }

        /// <summary>
        /// Sorted
        /// change the asn_status from 2 to 3
        /// </summary>
        /// <param name="idList">id list</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> SortedAsync(List<int> idList, CurrentUser currentUser)
        {
            var Asns = _dBContext.GetDbSet<AsnEntity>();
            var entities = await Asns.Where(t => idList.Contains(t.id) && t.tenant_id == currentUser.tenant_id).ToListAsync();
            if (!entities.Any())
            {
                return (false, "[202]" + _stringLocalizer["not_exists_entity"]);
            }
            else if (entities.Any(t => t.sorted_qty < 1))
            {
                return (false, "[202]" + $"{_stringLocalizer["ASN_Status_Is_Not_Sorting"]}");
            }
            entities.ForEach(e =>
            {
                e.asn_status = 3;
                if (e.sorted_qty > e.asn_qty)
                {
                    e.more_qty = e.sorted_qty - e.asn_qty;
                }
                else if (e.sorted_qty < e.asn_qty)
                {
                    e.shortage_qty = e.asn_qty - e.sorted_qty;
                }
                e.last_update_time = DateTime.Now;
            });
            var qty = await _dBContext.SaveChangesAsync();
            if (qty > 0)
            {
                return (true, _stringLocalizer["sorted_success"]);
            }
            else
            {
                return (false, _stringLocalizer["sorted_failed"]);
            }
        }

        /// <summary>
        /// Cancel sorted
        /// change the asn_status from 3 to 2
        /// </summary>
        /// <param name="idList">id list</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> SortedCancelAsync(List<int> idList, CurrentUser currentUser)
        {
            var Asns = _dBContext.GetDbSet<AsnEntity>();
            var entities = await Asns.Where(t => idList.Contains(t.id) && t.tenant_id == currentUser.tenant_id).ToListAsync();
            if (!entities.Any())
            {
                return (false, "[202]" + _stringLocalizer["not_exists_entity"]);
            }
            else if (entities.Any(t => t.actual_qty > 0))
            {
                return (false, "[202]" + $"{_stringLocalizer["ASN_Status_Is_Putaway"]}");
            }
            else if (entities.Any(t => t.sorted_qty < 1))
            {
                return (false, "[202]" + $"{_stringLocalizer["ASN_Status_Is_Not_Sorting"]}");
            }
            entities.ForEach(e =>
            {
                e.asn_status = 2;
                e.sorted_qty = 0;
                e.more_qty = 0;
                e.shortage_qty = 0;
                e.last_update_time = DateTime.Now;
            });
            var qty = await _dBContext.SaveChangesAsync();
            if (qty > 0)
            {
                var Asnsorts = _dBContext.GetDbSet<AsnsortEntity>();
                await Asnsorts.Where(t => idList.Contains(t.asn_id)).ExecuteDeleteAsync();
                return (true, _stringLocalizer["save_success"]);
            }
            else
            {
                return (false, _stringLocalizer["save_failed"]);
            }
        }

        /// <summary>
        /// print series number
        /// </summary>
        /// <param name="input">selected asn id</param>
        /// <returns></returns>
        public async Task<List<AsnPrintSeriesNumberViewModel>> GetAsnPrintSeriesNumberAsync(List<int> input, CurrentUser currentUser)
        {
            var Spus = _dBContext.GetDbSet<SpuEntity>().AsNoTracking();
            var Skus = _dBContext.GetDbSet<SkuEntity>().AsNoTracking();
            var Asns = _dBContext.GetDbSet<AsnEntity>().AsNoTracking();
            var Asnmasters = _dBContext.GetDbSet<AsnmasterEntity>().AsNoTracking();
            var sorts = _dBContext.GetDbSet<AsnsortEntity>().AsNoTracking();

            var query = from m in Asnmasters
                        join a in Asns on m.id equals a.asnmaster_id
                        join p in Spus.AsNoTracking() on a.spu_id equals p.id
                        join k in Skus.AsNoTracking() on a.sku_id equals k.id
                        join s in sorts on a.id equals s.asn_id
                        where input.Contains(a.id) && a.tenant_id == currentUser.tenant_id
                        select new AsnPrintSeriesNumberViewModel
                        {
                            asn_id = a.id,
                            asnmaster_id = m.id,
                            asn_no = m.asn_no,
                            sku_id = a.sku_id,
                            sku_code = k.sku_code,
                            sku_name = k.sku_name,
                            spu_code = p.spu_code,
                            spu_name = p.spu_name,
                            series_number = s.series_number
                        };
            var data = await query.OrderBy(t => t.asn_id).ToListAsync();
            data ??= new List<AsnPrintSeriesNumberViewModel>();
            return data;
        }
    }
}
