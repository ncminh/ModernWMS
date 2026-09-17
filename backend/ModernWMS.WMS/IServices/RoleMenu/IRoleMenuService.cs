using ModernWMS.Core.JWT;
using ModernWMS.Core.Services;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;

namespace ModernWMS.WMS.IServices
{
    /// <summary>
    /// Interface of RoleMenuService
    /// </summary>
    public interface IRoleMenuService : IBaseService<RoleMenuEntity>
    {
        #region Api
        /// <summary>
        /// Get all records
        /// </summary>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        Task<List<RoleMenuListViewModel>> GetAllAsync(CurrentUser currentUser);

        /// <summary>
        /// Get a record by id
        /// </summary>
        /// <param name="userrole_id">userrole id</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        Task<RoleMenuBothViewModel> GetAsync(int userrole_id, CurrentUser currentUser);

        /// <summary>
        /// add a new record
        /// </summary>
        /// <param name="viewModel">args</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        Task<(int id, string msg)> AddAsync(RoleMenuBothViewModel viewModel, CurrentUser currentUser);

        /// <summary>
        /// Get all menus
        /// </summary>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        Task<List<MenuViewModel>> GetAllMenusAsync(CurrentUser currentUser);

        /// <summary>
        /// Get menu's authority by user role id
        /// </summary>
        /// <param name="userrole_id">user role id</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        Task<List<MenuViewModel>> GetMenusByRoleId(int userrole_id, CurrentUser currentUser);

        /// <summary>
        /// update a record
        /// </summary>
        /// <param name="viewModel">args</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        Task<(bool flag, string msg)> UpdateAsync(RoleMenuBothViewModel viewModel, CurrentUser currentUser);

        /// <summary>
        /// delete a record
        /// </summary>
        /// <param name="userrole_id">userrole id</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        Task<(bool flag, string msg)> DeleteAsync(int userrole_id, CurrentUser currentUser);
        #endregion
    }
}
