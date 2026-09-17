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
    /// dispatchlist controller
    /// </summary>
    [Route("dispatchlist")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "WMS")]
    public class DispatchListController : BaseController
    {
        #region Args

        /// <summary>
        /// dispatchlist Service
        /// </summary>
        private readonly IDispatchListService _dispatchlistService;

        /// <summary>
        /// dispatch confirm/pick Service
        /// </summary>
        private readonly IDispatchConfirmService _dispatchConfirmService;

        /// <summary>
        /// dispatch package/weight/delivery/sign Service
        /// </summary>
        private readonly IDispatchDeliveryService _dispatchDeliveryService;

        /// <summary>
        /// Localizer Service
        /// </summary>
        private readonly IStringLocalizer<ModernWMS.Core.MultiLanguage> _stringLocalizer;
        #endregion

        #region constructor
        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="dispatchlistService">dispatchlist Service</param>
        /// <param name="dispatchConfirmService">dispatch confirm/pick Service</param>
        /// <param name="dispatchDeliveryService">dispatch package/weight/delivery/sign Service</param>
        /// <param name="stringLocalizer">Localizer</param>
        public DispatchListController(
            IDispatchListService dispatchlistService
          , IDispatchConfirmService dispatchConfirmService
          , IDispatchDeliveryService dispatchDeliveryService
          , IStringLocalizer<ModernWMS.Core.MultiLanguage> stringLocalizer
            )
        {
            this._dispatchlistService = dispatchlistService;
            this._dispatchConfirmService = dispatchConfirmService;
            this._dispatchDeliveryService = dispatchDeliveryService;
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
        public async Task<ResultModel<PageData<DispatchListViewModel>>> PageAsync(PageSearch pageSearch)
        {
            var (data, totals) = await _dispatchlistService.PageAsync(pageSearch, CurrentUser);

            return ResultModel<PageData<DispatchListViewModel>>.Success(new PageData<DispatchListViewModel>
            {
                Rows = data,
                Totals = totals
            });
        }
        /// <summary>
        /// advanced dispatch order page search
        /// </summary>
        /// <param name="pageSearch">args</param>
        /// <returns></returns>
        [HttpPost("advanced-list")]
        public async Task<ResultModel<PageData<PreDispatchListViewModel>>> AdvancedDispatchlistPageAsync(PageSearch pageSearch)
        {
            var (data, totals) = await _dispatchlistService.AdvancedDispatchlistPageAsync(pageSearch, CurrentUser);

            return ResultModel<PageData<PreDispatchListViewModel>>.Success(new PageData<PreDispatchListViewModel>
            {
                Rows = data,
                Totals = totals
            });
        }
        /// <summary>
        /// add a new record
        /// </summary>
        /// <param name="viewModel">args</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<ResultModel<string>> AddAsync(List<DispatchListAddViewModel> viewModel)
        {
            var (flag, msg) = await _dispatchlistService.AddAsync(viewModel, CurrentUser);
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
        /// update dispatchlist with same dispatch_no
        /// </summary>
        /// <param name="viewModel">viewModel</param>
        /// <returns></returns>
        [HttpPut]
        public async Task<ResultModel<string>> UpdateAsycn(List<DispatchListViewModel> viewModel)
        {
            var (flag, msg) = await _dispatchlistService.UpdateAsycn(viewModel, CurrentUser);
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
        /// get dispatchlist by dispatch_no
        /// </summary>
        /// <param name="dispatch_no">dispatch_no</param>
        /// <returns></returns>
        [HttpGet("by-dispatch_no")]
        public async Task<ResultModel<List<DispatchListViewModel>>> GetByDispatchlistNo(string dispatch_no)
        {
            var datas = await _dispatchlistService.GetByDispatchlistNo(dispatch_no, CurrentUser);
            return ResultModel<List<DispatchListViewModel>>.Success(datas);
        }
        /// <summary>
        /// get Dispatchlist details with available stock
        /// </summary>
        /// <param name="dispatch_no">dispatch_no</param>
        /// <returns></returns>
        [HttpGet("confirm-check")]
        public async Task<ResultModel<List<DispatchListConfirmDetailViewModel>>> ConfirmOrderCheck(string dispatch_no)
        {
            var datas = await _dispatchConfirmService.ConfirmOrderCheck(dispatch_no, CurrentUser);
            return ResultModel<List<DispatchListConfirmDetailViewModel>>.Success(datas);
        }

        /// <summary>
        ///  get pick list by dispatch_id
        /// </summary>
        /// <param name="dispatch_id">dispatch_id</param>
        /// <returns></returns>
        [HttpGet("pick-list")]
        public async Task<ResultModel<List<DispatchPickListViewModel>>> GetPickListByDispatchID(int dispatch_id)
        {
            var datas = await _dispatchConfirmService.GetPickListByDispatchID(dispatch_id, CurrentUser);
            return ResultModel<List<DispatchPickListViewModel>>.Success(datas);
        }
        /// <summary>
        /// delete a record
        /// </summary>
        /// <param name="dispatch_no">dispatch_no</param>
        /// <returns></returns>
        [HttpDelete]
        public async Task<ResultModel<string>> DeleteAsync(string dispatch_no)
        {
            var (flag, msg) = await _dispatchlistService.DeleteAsync(dispatch_no, CurrentUser);
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
        ///  Confirm orders and create  dispatchpicklist
        /// </summary>
        /// <param name="viewModels">viewModels</param>
        /// <returns></returns>
        [HttpPost("confirm-order")]
        public async Task<ResultModel<string>> ConfirmOrder(List<DispatchListConfirmDetailViewModel> viewModels)
        {
            var (flag, msg) = await _dispatchConfirmService.ConfirmOrder(viewModels, CurrentUser);
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
        ///  confirm dispatchpicklist picked by dispatch_no
        /// </summary>
        /// <param name="dispatch_no">viewModels</param>
        /// <returns></returns>
        [HttpPut("confirm-pick-dispatchlistno")]
        public async Task<ResultModel<string>> ConfirmPickByDispatchNo(string dispatch_no)
        {
            var (flag, msg) = await _dispatchConfirmService.ConfirmPickByDispatchNo(dispatch_no, CurrentUser);
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
        ///  confirm pick detail
        /// </summary>
        /// <param name="picklist_id">dispatch list pick detail id</param>
        /// <returns></returns>
        [HttpPost("confirm-pick-detail")]
        public async Task<ResultModel<string>> ConfirmPickDetail([FromBody]List<int> picklist_id)
        {
            var (flag, msg) = await _dispatchConfirmService.ConfirmPickDetail(picklist_id, CurrentUser);
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
        ///  confirm pick detail
        /// </summary>
        /// <param name="picklist_id">dispatch list pick detail id</param>
        /// <returns></returns>
        [HttpPost("cancel-confirm-pick-detail")]
        public async Task<ResultModel<string>> CancelConfirmPickDetail([FromBody] List<int> picklist_id)
        {
            var (flag, msg) = await _dispatchConfirmService.CancelConfirmPickDetail(picklist_id, CurrentUser);
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
        ///  package dispatchpicklist
        /// </summary>
        /// <param name="viewModels">viewModels</param>
        /// <returns></returns>
        [HttpPost("package")]
        public async Task<ResultModel<string>> Package(List<DispatchListPackageViewModel> viewModels)
        {
            var (flag, msg) = await _dispatchDeliveryService.Package(viewModels, CurrentUser);
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
        ///  weight dispatchpicklist
        /// </summary>
        /// <param name="viewModels">viewModels</param>
        /// <returns></returns>
        [HttpPost("weight")]
        public async Task<ResultModel<string>> Weight(List<DispatchListWeightViewModel> viewModels)
        {
            var (flag, msg) = await _dispatchDeliveryService.Weight(viewModels, CurrentUser);
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
        ///  dispatchpicklist outbound delivery
        /// </summary>
        /// <param name="viewModels">viewModels</param>
        /// <returns></returns>
        [HttpPost("delivery")]
        public async Task<ResultModel<string>> Delivery(List<DispatchListDeliveryViewModel> viewModels)
        {
            var (flag, msg) = await _dispatchDeliveryService.Delivery(viewModels, CurrentUser);
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
        ///  set dispatchlist freightfee
        /// </summary>
        /// <param name="viewModels">viewModels</param>
        /// <returns></returns>
        [HttpPost("freightfee")]
        public async Task<ResultModel<string>> SetFreightfee(List<DispatchListFreightFeeViewModel> viewModels)
        {
            var (flag, msg) = await _dispatchDeliveryService.SetFreightfee(viewModels, CurrentUser);
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
        ///  sign for arrival
        /// </summary>
        /// <param name="viewModels">viewModels</param>
        /// <returns></returns>
        [HttpPost("sign")]
        public async Task<ResultModel<string>> SignForArrival(List<DispatchListSignViewModel> viewModels)
        {
            var (flag, msg) = await _dispatchDeliveryService.SignForArrival(viewModels, CurrentUser);
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
        /// cancel order opration
        /// </summary>
        /// <param name="viewModel">viewModel</param>
        /// <returns></returns>
        [HttpPost("cancel-order")]
        public async Task<ResultModel<string>> CancelOrderOpration(CancelOrderOprationViewModel viewModel)
        {
            var (flag, msg) = await _dispatchConfirmService.CancelOrderOpration(viewModel, CurrentUser);
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
        /// cancel dispatchlist detail opration
        /// </summary>
        /// <param name="id">id</param>
        /// <returns></returns>
        [HttpPut("cancel-order")]
        public async Task<ResultModel<string>> CancelDispatchlistDetailOpration(int id)
        {
            var (flag, msg) = await _dispatchConfirmService.CancelDispatchlistDetailOpration(id, CurrentUser);
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
        /// Excel Import
        /// </summary>
        /// <param name="viewModels">viewModels</param>
        /// <returns></returns>
        [HttpPost("import")]
        public async Task<ResultModel<string>> Import(List<DispatchListImportViewModel> viewModels)
        {
            var (flag, msg) = await _dispatchlistService.Import(viewModels, CurrentUser);
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
