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
    ///  Asn confirm/unload Service (part of the "Flow Api" region, split out of AsnService)
    /// </summary>
    public class AsnConfirmService : BaseService<AsnEntity>, IAsnConfirmService
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
        ///AsnConfirm  constructor
        /// </summary>
        /// <param name="dBContext">The DBContext</param>
        /// <param name="stringLocalizer">Localizer</param>
        public AsnConfirmService(
            SqlDBContext dBContext
            , IStringLocalizer<Core.MultiLanguage> stringLocalizer
            )
        {
            this._dBContext = dBContext;
            this._stringLocalizer = stringLocalizer;
        }

        #endregion constructor

        /// <summary>
        /// Confirm Delivery
        /// change the asn_status from 0 to 1
        /// </summary>
        /// <param name="viewModels">args</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> ConfirmAsync(List<AsnConfirmInputViewModel> viewModels, CurrentUser currentUser)
        {
            var idList = viewModels.Where(t => t.id > 0).Select(t => t.id).ToList();
            var Asns = _dBContext.GetDbSet<AsnEntity>();
            var entities = await Asns.Where(t => idList.Contains(t.id) && t.tenant_id == currentUser.tenant_id).ToListAsync();
            var last_update_time = DateTime.Now;
            if (!entities.Any())
            {
                return (false, "[202]" + _stringLocalizer["not_exists_entity"]);
            }
            else if (entities.Any(t => t.asn_status > 0))
            {
                return (false, "[202]" + $"{_stringLocalizer["ASN_Status_Is_Not_Pre_Delivery"]}");
            }
            // get asnmaster data
            var asnmaster_id = entities.Select(t => t.asnmaster_id).FirstOrDefault();
            var Asnmaster = _dBContext.GetDbSet<AsnmasterEntity>();
            var asnmasterentity = await Asnmaster.FirstOrDefaultAsync(t => t.id.Equals(asnmaster_id));
            if (asnmasterentity == null)
            {
                return (false, "[202]" + _stringLocalizer["not_exists_entity"]);
            }
            // update asnmaster last_update_time
            asnmasterentity.last_update_time = last_update_time;

            entities.ForEach(t =>
            {
                var vm = viewModels.FirstOrDefault(v => v.id == t.id);
                if (vm != null)
                {
                    t.asn_status = 1;
                    t.arrival_time = vm.arrival_time;
                    t.last_update_time = last_update_time;
                }
            });
            var qty = await _dBContext.SaveChangesAsync();
            if (qty > 0)
            {
                return (true, _stringLocalizer["confirm_success"]);
            }
            else
            {
                return (false, _stringLocalizer["confirm_failed"]);
            }
        }

        /// <summary>
        /// Cancel confirm, change asn_status 1 to 0
        /// </summary>
        /// <param name="idList">id list</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> ConfirmCancelAsync(List<int> idList, CurrentUser currentUser)
        {
            var Asns = _dBContext.GetDbSet<AsnEntity>();
            var entities = await Asns.Where(t => idList.Contains(t.id) && t.tenant_id == currentUser.tenant_id).ToListAsync();
            var last_update_time = DateTime.Now;
            if (!entities.Any())
            {
                return (false, "[202]" + _stringLocalizer["not_exists_entity"]);
            }
            if (entities.Any(t => t.asn_status != (byte)1))
            {
                return (false, "[202]" + $"{_stringLocalizer["ASN_Status_Is_Not_Pre_Delivery"]}");
            }
            // get asnmaster data
            var asnmaster_id = entities.Select(t => t.asnmaster_id).FirstOrDefault();
            var Asnmaster = _dBContext.GetDbSet<AsnmasterEntity>();
            var asnmasterentity = await Asnmaster.FirstOrDefaultAsync(t => t.id.Equals(asnmaster_id));
            if (asnmasterentity == null)
            {
                return (false, "[202]" + _stringLocalizer["not_exists_entity"]);
            }
            // update asnmaster last_update_time
            asnmasterentity.last_update_time = last_update_time;

            entities.ForEach(e =>
            {
                e.asn_status = 0;
                e.arrival_time = Core.Utility.UtilConvert.MinDate;
                e.last_update_time = last_update_time;
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
        /// Unload
        /// change the asn_status from 1 to 2
        /// </summary>
        /// <param name="viewModels">args</param>
        /// <param name="user">user</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> UnloadAsync(List<AsnUnloadInputViewModel> viewModels, CurrentUser user)
        {
            var idList = viewModels.Where(t => t.id > 0).Select(t => t.id).ToList();
            var Asns = _dBContext.GetDbSet<AsnEntity>();
            var entities = await Asns.Where(t => idList.Contains(t.id) && t.tenant_id == user.tenant_id).ToListAsync();
            if (!entities.Any())
            {
                return (false, "[202]" + _stringLocalizer["not_exists_entity"]);
            }
            else if (entities.Any(t => t.asn_status > 1))
            {
                return (false, "[202]" + $"{_stringLocalizer["ASN_Status_Is_Not_Pre_Load"]}");
            }
            entities.ForEach(t =>
            {
                var vm = viewModels.FirstOrDefault(v => v.id == t.id);
                if (vm != null)
                {
                    t.asn_status = 2;
                    t.last_update_time = DateTime.Now;
                    t.unload_time = vm.unload_time;
                    t.unload_person_id = vm.unload_person_id == 0 ? user.user_id : vm.unload_person_id;
                    t.unload_person = vm.unload_person_id == 0 ? user.user_name : vm.unload_person;
                }
            });
            var qty = await _dBContext.SaveChangesAsync();
            if (qty > 0)
            {
                return (true, _stringLocalizer["confirm_success"]);
            }
            else
            {
                return (false, _stringLocalizer["confirm_failed"]);
            }
        }

        /// <summary>
        /// Cancel unload
        /// change the asn_status from 2 to 1
        /// </summary>
        /// <param name="idList">id list</param>
        /// <returns></returns>
        public async Task<(bool flag, string msg)> UnloadCancelAsync(List<int> idList, CurrentUser currentUser)
        {
            var Asns = _dBContext.GetDbSet<AsnEntity>();
            var entities = await Asns.Where(t => idList.Contains(t.id) && t.tenant_id == currentUser.tenant_id).ToListAsync();
            if (!entities.Any())
            {
                return (false, "[202]" + _stringLocalizer["not_exists_entity"]);
            }
            if (entities.Any(t => t.asn_status != (byte)2))
            {
                return (false, "[202]" + $"{_stringLocalizer["ASN_Status_Is_Not_Pre_Load"]}");
            }

            entities.ForEach(e =>
            {
                e.asn_status = 1;
                e.unload_time = Core.Utility.UtilConvert.MinDate;
                e.unload_person_id = 0;
                e.unload_person = string.Empty;
                e.last_update_time = DateTime.Now;
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
    }
}
