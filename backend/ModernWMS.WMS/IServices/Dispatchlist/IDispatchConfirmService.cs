/*
 * date：2022-12-27
 * developer：NoNo
 */
using ModernWMS.Core.JWT;
using ModernWMS.Core.Services;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;

namespace ModernWMS.WMS.IServices
{
    /// <summary>
    /// Interface of DispatchConfirmService.
    /// Split out of IDispatchlistService (DispatchlistService pre-split): the
    /// confirm/pick sub-flow, from stock-availability check through pick confirmation
    /// and order cancellation.
    /// </summary>
    public interface IDispatchConfirmService : IBaseService<DispatchlistEntity>
    {
        /// <summary>
        /// Dispatchlist details with available stock
        /// </summary>
        /// <param name="dispatch_no">dispatch_no</param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        Task<List<DispatchlistConfirmDetailViewModel>> ConfirmOrderCheck(string dispatch_no, CurrentUser currentUser);

        /// <summary>
        ///  Confirm orders and create  dispatchpicklist
        /// </summary>
        /// <param name="viewModels">viewModels</param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        Task<(bool flag, string msg)> ConfirmOrder(List<DispatchlistConfirmDetailViewModel> viewModels, CurrentUser currentUser);

        /// <summary>
        /// confirm dispatchpicklist picked by dispatch_no
        /// </summary>
        /// <param name="dispatch_no">dispatch_no</param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        Task<(bool flag, string msg)> ConfirmPickByDispatchNo(string dispatch_no, CurrentUser currentUser);

        /// <summary>
        /// confirm pick detail
        /// </summary>
        /// <param name="picklist_id">dispatch list pick detail id</param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        Task<(bool flag, string msg)> ConfirmPickDetail(List<int> picklist_id, CurrentUser currentUser);

        /// <summary>
        /// cancel confirm pick detail
        /// </summary>
        /// <param name="picklist_id">dispatch list pick detail id</param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        Task<(bool flag, string msg)> CancelConfirmPickDetail(List<int> picklist_id, CurrentUser currentUser);

        /// <summary>
        /// get pick list by dispatch_id
        /// </summary>
        /// <param name="dispatch_id">dispatch_id</param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        Task<List<DispatchpicklistViewModel>> GetPickListByDispatchID(int dispatch_id, CurrentUser currentUser);

        /// <summary>
        ///  cancel order opration
        /// </summary>
        /// <param name="viewModel">viewmodel</param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        Task<(bool flag, string msg)> CancelOrderOpration(CancelOrderOprationViewModel viewModel, CurrentUser currentUser);

        /// <summary>
        /// cancel dispatchlist detail opration
        /// </summary>
        /// <param name="id">dispatchlist_id</param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        Task<(bool flag, string msg)> CancelDispatchlistDetailOpration(int id, CurrentUser currentUser);
    }
}
