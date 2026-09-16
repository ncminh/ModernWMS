/*
 * date：2022-12-22
 * developer：AMo
 */
using ModernWMS.Core.JWT;
using ModernWMS.Core.Services;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;

namespace ModernWMS.WMS.IServices
{
    /// <summary>
    /// Interface of AsnPutawayService.
    /// Split out of IAsnService's "Flow Api" region (AsnService pre-split): the
    /// putaway sub-flow, the only part of the Asn workflow that writes StockEntity.
    /// </summary>
    public interface IAsnPutawayService : IBaseService<AsnEntity>
    {
        /// <summary>
        /// get pending putaway data by asn_id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        Task<List<AsnPendingPutawayViewModel>> GetPendingPutawayDataAsync(int id, CurrentUser currentUser);

        /// <summary>
        /// PutAway
        /// </summary>
        /// <param name="viewModels">args</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        Task<(bool flag, string msg)> PutAwayAsync(List<AsnPutAwayInputViewModel> viewModels, CurrentUser currentUser);
    }
}
