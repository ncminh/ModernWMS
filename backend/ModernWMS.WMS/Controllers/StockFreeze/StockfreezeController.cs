/*
 * date：2022-12-26
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
     /// stockfreeze controller
     /// </summary>
     [Route("stockfreeze")]
     [ApiController]
     [ApiExplorerSettings(GroupName = "WMS")]
     public class StockFreezeController : BaseController
     {
         #region Args
 
         /// <summary>
         /// stockfreeze Service
         /// </summary>
         private readonly IStockFreezeService _stockfreezeService;
 
         /// <summary>
         /// Localizer Service
         /// </summary>
         private readonly IStringLocalizer<ModernWMS.Core.MultiLanguage> _stringLocalizer;
         #endregion
 
         #region constructor
         /// <summary>
         /// constructor
         /// </summary>
         /// <param name="stockfreezeService">stockfreeze Service</param>
        /// <param name="stringLocalizer">Localizer</param>
         public StockFreezeController(
             IStockFreezeService stockfreezeService
           , IStringLocalizer<ModernWMS.Core.MultiLanguage> stringLocalizer
             )
         {
             this._stockfreezeService = stockfreezeService;
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
         public async Task<ResultModel<PageData<StockFreezeViewModel>>> PageAsync(PageSearch pageSearch)
         {
             var (data, totals) = await _stockfreezeService.PageAsync(pageSearch, CurrentUser);
              
             return ResultModel<PageData<StockFreezeViewModel>>.Success(new PageData<StockFreezeViewModel>
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
         public async Task<ResultModel<List<StockFreezeViewModel>>> GetAllAsync()
         {
             var data = await _stockfreezeService.GetAllAsync(CurrentUser);
             if (data.Any())
             {
                 return ResultModel<List<StockFreezeViewModel>>.Success(data);
             }
             else
             {
                 return ResultModel<List<StockFreezeViewModel>>.Success(new List<StockFreezeViewModel>());
             }
         }
 
         /// <summary>
         /// get a record by id
         /// </summary>
         /// <returns>args</returns>
         [HttpGet]
         public async Task<ResultModel<StockFreezeViewModel>> GetAsync(int id)
         {
             var data = await _stockfreezeService.GetAsync(id, CurrentUser);
             if (data!=null)
             {
                 return ResultModel<StockFreezeViewModel>.Success(data);
             }
             else
             {
                 return ResultModel<StockFreezeViewModel>.Error(_stringLocalizer["not_exists_entity"]);
             }
         }
         /// <summary>
         /// add a new record
         /// </summary>
         /// <param name="viewModel">args</param>
         /// <returns></returns>
         [HttpPost]
         public async Task<ResultModel<int>> AddAsync(StockFreezeViewModel viewModel)
         {
             var (id, msg) = await _stockfreezeService.AddAsync(viewModel,CurrentUser);
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
         public async Task<ResultModel<bool>> UpdateAsync(StockFreezeViewModel viewModel)
         {
             var (flag, msg) = await _stockfreezeService.UpdateAsync(viewModel, CurrentUser);
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
             var (flag, msg) = await _stockfreezeService.DeleteAsync(id, CurrentUser);
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
 
