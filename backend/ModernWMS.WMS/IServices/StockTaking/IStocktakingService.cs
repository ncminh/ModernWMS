/*
 * date：2022-12-30
 * developer：AMo
 */
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.Core.Services;
 using ModernWMS.WMS.Entities.Models;
 using ModernWMS.WMS.Entities.ViewModels;
 
 namespace ModernWMS.WMS.IServices
 {
     /// <summary>
     /// Interface of StockTakingService
     /// </summary>
     public interface IStockTakingService : IBaseService<StockTakingEntity>
     {
         #region Api
         /// <summary>
         /// page search
         /// </summary>
         /// <param name="pageSearch">args</param>
         /// <param name="currentUser">current user</param>
         /// <returns></returns>
         Task<(List<StockTakingViewModel> data, int totals)> PageAsync(PageSearch pageSearch, CurrentUser currentUser);
          
         /// <summary>
         /// Get a record by id
         /// </summary>
         /// <param name="id">primary key</param>
         /// <param name="currentUser">current user</param>
         /// <returns></returns>
         Task<StockTakingViewModel> GetAsync(int id, CurrentUser currentUser);
        /// <summary>
        /// add a new record
        /// </summary>
        /// <param name="viewModel">viewmodel</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        Task<(int id, string msg)> AddAsync(StockTakingBasicViewModel viewModel, CurrentUser currentUser);

        /// <summary>
        /// update  counted_qty
        /// </summary>
        /// <param name="viewModel">args</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        Task<(bool flag, string msg)> PutAsync(StockTakingConfirmViewModel viewModel, CurrentUser currentUser);

        /// <summary>
        /// confrim a record and change stock and add to stockadjust
        /// </summary>
        /// <param name="id">id</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        Task<(bool flag, string msg)> ConfirmAsync(int id, CurrentUser currentUser);
 
         /// <summary>
         /// delete a record
         /// </summary>
         /// <param name="id">id</param>
         /// <param name="currentUser">current user</param>
         /// <returns></returns>
         Task<(bool flag, string msg)> DeleteAsync(int id, CurrentUser currentUser);
         #endregion
     }
 }
 
