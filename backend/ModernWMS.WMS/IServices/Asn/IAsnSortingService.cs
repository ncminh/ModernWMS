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
    /// Interface of AsnSortingService.
    /// Split out of IAsnService's "Flow Api" region (AsnService pre-split): the
    /// sorting sub-flow, plus the print-series-number read that also depends on
    /// AsnsortEntity.
    /// </summary>
    public interface IAsnSortingService : IBaseService<AsnsortEntity>
    {
        /// <summary>
        /// sorting， add a new asnsort record and update asn sorted_qty
        /// </summary>
        /// <param name="viewModels">args</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        Task<(bool flag, string msg)> SortingAsync(List<AsnsortInputViewModel> viewModels, CurrentUser currentUser);

        /// <summary>
        /// get asnsorts list by asn_id
        /// </summary>
        /// <param name="asn_id">asn id</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        Task<List<AsnsortViewModel>> GetAsnsortsAsync(int asn_id, CurrentUser currentUser);

        /// <summary>
        /// update or delete asnsorts data
        /// </summary>
        /// <param name="entities">data</param>
        /// <param name="user">CurrentUser</param>
        /// <returns></returns>
        Task<(bool flag, string msg)> ModifyAsnsortsAsync(List<AsnsortEntity> entities, CurrentUser user);

        /// <summary>
        /// Sorted
        /// change the asn_status from 2 to 3
        /// </summary>
        /// <param name="idList">id list</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        Task<(bool flag, string msg)> SortedAsync(List<int> idList, CurrentUser currentUser);

        /// <summary>
        /// Cancel sorted
        /// change the asn_status from 3 to 2
        /// </summary>
        /// <param name="idList">id list</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        Task<(bool flag, string msg)> SortedCancelAsync(List<int> idList, CurrentUser currentUser);

        /// <summary>
        /// print series number
        /// </summary>
        /// <param name="input">selected asn id</param>
        /// <param name="currentUser">currentUser</param>
        /// <returns></returns>
        Task<List<AsnPrintSeriesNumberViewModel>> GetAsnPrintSeriesNumberAsync(List<int> input, CurrentUser currentUser);
    }
}
