/*
 * date：2022-12-27
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
     /// stockmove controller
     /// </summary>
     [Route("stockmove")]
     [ApiController]
     [ApiExplorerSettings(GroupName = "WMS")]
     public class StockMoveController : BaseController
     {
         #region Args
 
         /// <summary>
         /// stockmove Service
         /// </summary>
         private readonly IStockMoveService _stockmoveService;
 
         /// <summary>
         /// Localizer Service
         /// </summary>
         private readonly IStringLocalizer<ModernWMS.Core.MultiLanguage> _stringLocalizer;
         #endregion
 
         #region constructor
         /// <summary>
         /// constructor
         /// </summary>
         /// <param name="stockmoveService">stockmove Service</param>
        /// <param name="stringLocalizer">Localizer</param>
         public StockMoveController(
             IStockMoveService stockmoveService
           , IStringLocalizer<ModernWMS.Core.MultiLanguage> stringLocalizer
             )
         {
             this._stockmoveService = stockmoveService;
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
         public async Task<ResultModel<PageData<StockMoveViewModel>>> PageAsync(PageSearch pageSearch)
         {
             var (data, totals) = await _stockmoveService.PageAsync(pageSearch, CurrentUser);
              
             return ResultModel<PageData<StockMoveViewModel>>.Success(new PageData<StockMoveViewModel>
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
         public async Task<ResultModel<List<StockMoveViewModel>>> GetAllAsync()
         {
             var data = await _stockmoveService.GetAllAsync(CurrentUser);
             if (data.Any())
             {
                 return ResultModel<List<StockMoveViewModel>>.Success(data);
             }
             else
             {
                 return ResultModel<List<StockMoveViewModel>>.Success(new List<StockMoveViewModel>());
             }
         }
 
         /// <summary>
         /// get a record by id
         /// </summary>
         /// <returns>args</returns>
         [HttpGet]
         public async Task<ResultModel<StockMoveViewModel>> GetAsync(int id)
         {
             var data = await _stockmoveService.GetAsync(id, CurrentUser);
             if (data!=null)
             {
                 return ResultModel<StockMoveViewModel>.Success(data);
             }
             else
             {
                 return ResultModel<StockMoveViewModel>.Error(_stringLocalizer["not_exists_entity"]);
             }
         }
         /// <summary>
         /// add a new record
         /// </summary>
         /// <param name="viewModel">args</param>
         /// <returns></returns>
         [HttpPost]
         public async Task<ResultModel<int>> AddAsync(StockMoveViewModel viewModel)
         {
             var (id, msg) = await _stockmoveService.AddAsync(viewModel,CurrentUser);
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
         /// confirm move
         /// </summary>
         /// <param name="id">id</param>
         /// <returns></returns>
         [HttpPut]
         public async Task<ResultModel<bool>> Confirm(int id)
         {
             var (flag, msg) = await _stockmoveService.Confirm(id,CurrentUser);
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
             var (flag, msg) = await _stockmoveService.DeleteAsync(id, CurrentUser);
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
 
