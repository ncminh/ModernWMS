/*
 * date：2022-12-21
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
     /// Interface of GoodsLocationService
     /// </summary>
     public interface IGoodsLocationService : IBaseService<GoodsLocationEntity>
     {
        #region Api
        /// <summary>
        /// get goodslocation of the warehousearea by warehouse_id and warehousearea_id
        /// </summary>
        /// <param name="warehousearea_id">warehousearea's id</param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        Task<List<FormSelectItem>> GetGoodslocationByWarehouse_area_id(int warehousearea_id, CurrentUser currentUser);

        /// <summary>
        /// page search
        /// </summary>
        /// <param name="pageSearch">args</param>
        /// <param name="currentUser">current user</param>
        /// <returns></returns>
        Task<(List<GoodsLocationViewModel> data, int totals)> PageAsync(PageSearch pageSearch, CurrentUser currentUser);
         /// <summary>
         /// Get all records
         /// </summary>
         /// <returns></returns>
         Task<List<GoodsLocationViewModel>> GetAllAsync(CurrentUser currentUser);
         /// <summary>
         /// Get a record by id
         /// </summary>
         /// <param name="id">primary key</param>
         /// <param name="currentUser">current user</param>
         /// <returns></returns>
         Task<GoodsLocationViewModel> GetAsync(int id, CurrentUser currentUser);
         /// <summary>
         /// add a new record
         /// </summary>
         /// <param name="viewModel">viewmodel</param>
         /// <param name="currentUser">current user</param>
         /// <returns></returns>
         Task<(int id, string msg)> AddAsync(GoodsLocationViewModel viewModel, CurrentUser currentUser);
        /// <summary>
        /// update a record
        /// </summary>
        /// <param name="viewModel">viewmodel</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        Task<(bool flag, string msg)> UpdateAsync(GoodsLocationViewModel viewModel, CurrentUser currentUser);
 
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
 
