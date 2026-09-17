/*
 * date：2022-12-26
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
     /// Interface of StockfreezeService
     /// </summary>
     public interface IStockFreezeService : IBaseService<StockFreezeEntity>
     {
         #region Api
         /// <summary>
         /// page search
         /// </summary>
         /// <param name="pageSearch">args</param>
         /// <param name="currentUser">current user</param>
         /// <returns></returns>
         Task<(List<StockFreezeViewModel> data, int totals)> PageAsync(PageSearch pageSearch, CurrentUser currentUser);
         /// <summary>
         /// Get all records
         /// </summary>
         /// <returns></returns>
         Task<List<StockFreezeViewModel>> GetAllAsync(CurrentUser currentUser);
         /// <summary>
         /// Get a record by id
         /// </summary>
         /// <param name="id">primary key</param>
         /// <param name="currentUser">current user</param>
         /// <returns></returns>
         Task<StockFreezeViewModel> GetAsync(int id, CurrentUser currentUser);
         /// <summary>
         /// add a new record
         /// </summary>
         /// <param name="viewModel">viewmodel</param>
         /// <param name="currentUser">current user</param>
         /// <returns></returns>
         Task<(int id, string msg)> AddAsync(StockFreezeViewModel viewModel, CurrentUser currentUser);
         /// <summary>
         /// update a record
         /// </summary>
         /// <param name="viewModel">viewmodel</param>
         /// <param name="currentUser">current user</param>
         /// <returns></returns>
         Task<(bool flag, string msg)> UpdateAsync(StockFreezeViewModel viewModel, CurrentUser currentUser);

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
 
