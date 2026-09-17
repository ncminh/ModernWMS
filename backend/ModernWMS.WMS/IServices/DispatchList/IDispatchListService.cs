/*
 * date：2022-12-27
 * developer：NoNo
 */
 using ModernWMS.Core.Services;
 using ModernWMS.Core.Models;
 using ModernWMS.Core.JWT;
 using ModernWMS.WMS.Entities.Models;
 using ModernWMS.WMS.Entities.ViewModels;
namespace ModernWMS.WMS.IServices
{
    /// <summary>
    /// Interface of DispatchListService.
    /// Basic CRUD/read for dispatchlist records; the confirm/pick sub-flow lives in
    /// IDispatchConfirmService and the package/weight/delivery/sign sub-flow lives in
    /// IDispatchDeliveryService.
    /// </summary>
     public interface IDispatchListService : IBaseService<DispatchListEntity>
     {
         #region Api
         /// <summary>
         /// page search
         /// </summary>
         /// <param name="pageSearch">args</param>
         /// <param name="currentUser">current user</param>
         /// <returns></returns>
         Task<(List<DispatchListViewModel> data, int totals)> PageAsync(PageSearch pageSearch, CurrentUser currentUser);

        /// <summary>
        /// advanced dispatch order page search
        /// </summary>
        /// <param name="pageSearch">args</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        Task<(List<PreDispatchListViewModel> data, int totals)> AdvancedDispatchlistPageAsync(PageSearch pageSearch, CurrentUser currentUser);

         /// <summary>
         /// add a new record
         /// </summary>
         /// <param name="viewModel">viewmodel</param>
         /// <param name="currentUser">current user</param>
         /// <returns></returns>
         Task<(bool flag, string msg)> AddAsync(List<DispatchListAddViewModel> viewModel, CurrentUser currentUser);

        /// <summary>
        /// get dispatchlist by dispatch_no
        /// </summary>
        /// <param name="dispatch_no"></param>
        /// <param name="currentUser"></param>
        /// <returns></returns>
        Task<List<DispatchListViewModel>> GetByDispatchlistNo(string dispatch_no, CurrentUser currentUser);

        /// <summary>
        /// delete a record
        /// </summary>
        /// <param name="dispatch_no">dispatch_no</param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        Task<(bool flag, string msg)> DeleteAsync(string dispatch_no, CurrentUser currentUser);

        /// <summary>
        /// update dispatchlist with same dispatch_no
        /// </summary>
        /// <param name="viewModels"></param>
        /// <param name="currentUser"></param>
        /// <returns></returns>
        Task<(bool flag, string msg)> UpdateAsycn(List<DispatchListViewModel> viewModels, CurrentUser currentUser);

        /// <summary>
        /// Excel Import
        /// </summary>
        /// <param name="viewModels">viewModels</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        Task<(bool flag, string msg)> Import(List<DispatchListImportViewModel> viewModels, CurrentUser currentUser);
         #endregion
     }
 }

