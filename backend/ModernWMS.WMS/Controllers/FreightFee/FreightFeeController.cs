/*
 * date：2022-12-22
 * developer：NoNo
 */
 using Microsoft.AspNetCore.Mvc;
 using ModernWMS.Core.Controller;
 using ModernWMS.Core.Models;
 using ModernWMS.WMS.Entities.ViewModels;
 using ModernWMS.WMS.IServices;
 using Microsoft.Extensions.Localization;
namespace ModernWMS.WMS.Controllers
{
    /// <summary>
    /// freightfee controller
    /// </summary>
     [Route("freightfee")]
     [ApiController]
     [ApiExplorerSettings(GroupName = "Base")]
     public class FreightFeeController : BaseController
     {
         #region Args
 
         /// <summary>
         /// freightfee Service
         /// </summary>
         private readonly IFreightFeeService _freightfeeService;
 
         /// <summary>
         /// Localizer Service
         /// </summary>
         private readonly IStringLocalizer<ModernWMS.Core.MultiLanguage> _stringLocalizer;
         #endregion
 
         #region constructor
         /// <summary>
         /// constructor
         /// </summary>
         /// <param name="freightfeeService">freightfee Service</param>
        /// <param name="stringLocalizer">Localizer</param>
         public FreightFeeController(
             IFreightFeeService freightfeeService
           , IStringLocalizer<ModernWMS.Core.MultiLanguage> stringLocalizer
             )
         {
             this._freightfeeService = freightfeeService;
            this._stringLocalizer= stringLocalizer;
         }
         #endregion
 
         #region Api
         /// <summary>
         /// page search
         /// </summary>
         /// <param name="pageSearch">args</param>
         /// <returns></returns>
         [HttpPost("list")]
         public async Task<ResultModel<PageData<FreightFeeViewModel>>> PageAsync(PageSearch pageSearch)
         {
             var (data, totals) = await _freightfeeService.PageAsync(pageSearch, CurrentUser);
              
             return ResultModel<PageData<FreightFeeViewModel>>.Success(new PageData<FreightFeeViewModel>
             {
                 Rows = data,
                 Totals = totals
             });
         }
 
         /// <summary>
         /// get all records
         /// </summary>
         /// <returns>args</returns>
        [HttpGet("all")]
         public async Task<ResultModel<List<FreightFeeViewModel>>> GetAllAsync()
         {
             var data = await _freightfeeService.GetAllAsync(CurrentUser);
             if (data.Any())
             {
                 return ResultModel<List<FreightFeeViewModel>>.Success(data);
             }
             else
             {
                 return ResultModel<List<FreightFeeViewModel>>.Success(new List<FreightFeeViewModel>());
             }
         }
 
         /// <summary>
         /// get a record by id
         /// </summary>
         /// <returns>args</returns>
         [HttpGet]
         public async Task<ResultModel<FreightFeeViewModel>> GetAsync(int id)
         {
             var data = await _freightfeeService.GetAsync(id, CurrentUser);
             if (data!=null)
             {
                 return ResultModel<FreightFeeViewModel>.Success(data);
             }
             else
             {
                 return ResultModel<FreightFeeViewModel>.Error(_stringLocalizer["not_exists_entity"]);
             }
         }
         /// <summary>
         /// add a new record
         /// </summary>
         /// <param name="viewModel">args</param>
         /// <returns></returns>
         [HttpPost]
         public async Task<ResultModel<int>> AddAsync(FreightFeeViewModel viewModel)
         {
             var (id, msg) = await _freightfeeService.AddAsync(viewModel,CurrentUser);
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
         public async Task<ResultModel<bool>> UpdateAsync(FreightFeeViewModel viewModel)
         {
             var (flag, msg) = await _freightfeeService.UpdateAsync(viewModel, CurrentUser);
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
             var (flag, msg) = await _freightfeeService.DeleteAsync(id, CurrentUser);
             if (flag)
             {
                 return ResultModel<string>.Success(msg);
             }
             else
             {
                 return ResultModel<string>.Error(msg);
             }
         }

        /// <summary>
        /// import freight fee by excel
        /// </summary>
        /// <param name="excel_datas">excel datas</param>
        /// <returns></returns>
        [HttpPost("excel")]
        public async Task<ResultModel<string>> ExcelAsync(List<FreightFeeExcelmportViewModel> excel_datas)
        {
            var (flag, msg) = await _freightfeeService.ExcelAsync(excel_datas, CurrentUser);
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

    }
 }
 
