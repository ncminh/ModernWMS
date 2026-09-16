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
    /// Interface of DispatchDeliveryService.
    /// Split out of IDispatchlistService (DispatchlistService pre-split): the
    /// package/weight/delivery/freight/sign sub-flow. Delivery is the only part of
    /// the Dispatchlist workflow that writes StockEntity.
    /// </summary>
    public interface IDispatchDeliveryService : IBaseService<DispatchlistEntity>
    {
        /// <summary>
        ///  package
        /// </summary>
        /// <param name="viewModels">viewModels</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        Task<(bool flag, string msg)> Package(List<DispatchlistPackageViewModel> viewModels, CurrentUser currentUser);

        /// <summary>
        ///  weight
        /// </summary>
        /// <param name="viewModels">viewModels</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        Task<(bool flag, string msg)> Weight(List<DispatchlistWeightViewModel> viewModels, CurrentUser currentUser);

        /// <summary>
        /// dispatchpicklist outbound delivery
        /// </summary>
        /// <param name="viewModels">viewModels</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        Task<(bool flag, string msg)> Delivery(List<DispatchlistDeliveryViewModel> viewModels, CurrentUser currentUser);

        /// <summary>
        ///  set dispatchlist freightfee
        /// </summary>
        /// <param name="viewModels"></param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        Task<(bool flag, string msg)> SetFreightfee(List<DispatchlistFreightfeeViewModel> viewModels, CurrentUser currentUser);

        /// <summary>
        /// sign for arrival
        /// </summary>
        /// <param name="viewModels">viewModels</param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        Task<(bool flag, string msg)> SignForArrival(List<DispatchlistSignViewModel> viewModels, CurrentUser currentUser);
    }
}
