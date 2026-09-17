using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ModernWMS.Core.Controller;
using ModernWMS.Core.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.IServices;
using ModernWMS.WMS.Services;

namespace ModernWMS.WMS.Controllers
{
    /// <summary>
    /// goodsowner controller
    /// </summary>
    [Route("goodsowner")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "Base")]
    public class GoodsOwnerController : BaseController
    {
        #region Args

        /// <summary>
        /// goodsowner Service
        /// </summary>
        private readonly IGoodsOwnerService _goodsownerService;
        /// <summary>
        /// Localizer Service
        /// </summary>
        private readonly IStringLocalizer<ModernWMS.Core.MultiLanguage> _stringLocalizer;

        #endregion

        #region constructor
        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="goodsownerService">goodsowner Service</param>
        /// <param name="stringLocalizer">Localizer</param>
        public GoodsOwnerController(
            IGoodsOwnerService goodsownerService
          , IStringLocalizer<ModernWMS.Core.MultiLanguage> stringLocalizer
            )
        {
            this._goodsownerService = goodsownerService;
            this._stringLocalizer = stringLocalizer;
        }
        #endregion

        #region Api
        /// <summary>
        /// page search
        /// </summary>
        /// <param name="pageSearch">args</param>
        /// <returns></returns>
        [HttpPost("list")]
        public async Task<ResultModel<PageData<GoodsOwnerViewModel>>> PageAsync(PageSearch pageSearch)
        {
            var (data, totals) = await _goodsownerService.PageAsync(pageSearch, CurrentUser);

            return ResultModel<PageData<GoodsOwnerViewModel>>.Success(new PageData<GoodsOwnerViewModel>
            {
                Rows = data,
                Totals = totals
            });
        }
        /// <summary>
        /// Get all records
        /// </summary>
        /// <returns>args</returns>
        [HttpGet("all")]
        public async Task<ResultModel<List<GoodsOwnerViewModel>>> GetAllAsync()
        {
            var data = await _goodsownerService.GetAllAsync(CurrentUser);
            if (data.Any())
            {
                return ResultModel<List<GoodsOwnerViewModel>>.Success(data);
            }
            else
            {
                return ResultModel<List<GoodsOwnerViewModel>>.Success(new List<GoodsOwnerViewModel>());
            }
        }

        /// <summary>
        /// get a record by id
        /// </summary>
        /// <returns>args</returns>
        [HttpGet]
        public async Task<ResultModel<GoodsOwnerViewModel>> GetAsync(int id)
        {
            var data = await _goodsownerService.GetAsync(id, CurrentUser);
            if (data != null && data.id > 0)
            {
                return ResultModel<GoodsOwnerViewModel>.Success(data);
            }
            else
            {
                return ResultModel<GoodsOwnerViewModel>.Error(_stringLocalizer["not_exists_entity"]);
            }
        }
        /// <summary>
        /// add a new record
        /// </summary>
        /// <param name="viewModel">args</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<ResultModel<int>> AddAsync(GoodsOwnerViewModel viewModel)
        {
            var (id, msg) = await _goodsownerService.AddAsync(viewModel, CurrentUser);
            if (id > 0)
            {
                return ResultModel<int>.Success(id);
            }
            else
            {
                return ResultModel<int>.Error(msg);
            }
        }

        /// <summary>
        /// update a record
        /// </summary>
        /// <param name="viewModel">args</param>
        /// <returns></returns>
        [HttpPut]
        public async Task<ResultModel<bool>> UpdateAsync(GoodsOwnerViewModel viewModel)
        {
            var (flag, msg) = await _goodsownerService.UpdateAsync(viewModel, CurrentUser);
            if (flag)
            {
                return ResultModel<bool>.Success(flag);
            }
            else
            {
                return ResultModel<bool>.Error(msg, 400, flag);
            }
        }

        /// <summary>
        /// delete a record
        /// </summary>
        /// <param name="id">id</param>
        /// <returns></returns>
        [HttpDelete]
        public async Task<ResultModel<string>> DeleteAsync(int id)
        {
            var (flag, msg) = await _goodsownerService.DeleteAsync(id, CurrentUser);
            if (flag)
            {
                return ResultModel<string>.Success(msg);
            }
            else
            {
                return ResultModel<string>.Error(msg);
            }
        }
        #endregion

        #region Import
        /// <summary>
        /// import goodsowners by excel
        /// </summary>
        /// <param name="input">excel data</param>
        /// <returns></returns>
        [HttpPost("excel")]
        public async Task<ResultModel<List<GoodsOwnerImportViewModel>>> ExcelAsync(List<GoodsOwnerImportViewModel> input)
        {
            var (flag, errorData) = await _goodsownerService.ExcelAsync(input, CurrentUser);
            if (flag)
            {
                return ResultModel<List<GoodsOwnerImportViewModel>>.Success(errorData);
            }
            else
            {
                return ResultModel<List<GoodsOwnerImportViewModel>>.Error("", 400, errorData);
            }
        }
        #endregion
    }
}
