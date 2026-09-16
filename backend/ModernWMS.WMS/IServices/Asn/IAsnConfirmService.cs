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
    /// Interface of AsnConfirmService.
    /// Split out of IAsnService's "Flow Api" region (AsnService pre-split): the
    /// confirm-delivery / unload pair of status transitions.
    /// </summary>
    public interface IAsnConfirmService : IBaseService<AsnEntity>
    {
        /// <summary>
        /// Confirm Delivery
        /// change the asn_status from 0 to 1
        /// </summary>
        /// <param name="viewModels">args</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        Task<(bool flag, string msg)> ConfirmAsync(List<AsnConfirmInputViewModel> viewModels, CurrentUser currentUser);

        /// <summary>
        /// Cancel confirm, change asn_status 1 to 0
        /// </summary>
        /// <param name="idList">id list</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        Task<(bool flag, string msg)> ConfirmCancelAsync(List<int> idList, CurrentUser currentUser);

        /// <summary>
        /// Unload
        /// change the asn_status from 1 to 2
        /// </summary>
        /// <param name="viewModels">args</param>
        /// <param name="user">user</param>
        /// <returns></returns>
        Task<(bool flag, string msg)> UnloadAsync(List<AsnUnloadInputViewModel> viewModels, CurrentUser user);

        /// <summary>
        /// Cancel unload
        /// change the asn_status from 2 to 1
        /// </summary>
        /// <param name="idList">id list</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        Task<(bool flag, string msg)> UnloadCancelAsync(List<int> idList, CurrentUser currentUser);
    }
}
