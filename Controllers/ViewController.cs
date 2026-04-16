using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using HRMSAPI.Interfaces;
using Roomsy.DTOS.GenericsResponses;
using Microsoft.AspNetCore.Http;

namespace HRMSAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ViewController : ControllerBase
    {
        private readonly IViewService _viewService;
        public ViewController(IViewService viewService)
        {
            _viewService = viewService;
        }

        [HttpGet("ExportEmpAttendanceFormatToExcel")]
        public async Task<IActionResult> ExportEmpAttendanceFormatToExcel([FromQuery] string ecode = null)
        {
            var bytes = await _viewService.ExportEmpAttendanceFormatToExcelAsync(ecode);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "EmpAttendanceFormat.xlsx");
        }

        [HttpGet("ExportBgtSalaryStructWithEmpDetailsToExcel")]
        public async Task<IActionResult> ExportBgtSalaryStructWithEmpDetailsToExcel([FromQuery] string ecode = null)
        {
            var bytes = await _viewService.ExportBgtSalaryStructWithEmpDetailsToExcelAsync(ecode);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "BgtSalaryStructWithEmpDetails.xlsx");
        }

        [HttpGet("ExportLeaveMasterToExcel")]
        public async Task<IActionResult> ExportLeaveMasterToExcel([FromQuery] string ecode = null)
        {
            var bytes = await _viewService.ExportLeaveMasterToExcelAsync(ecode);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "LeaveMaster.xlsx");
        }

        [HttpGet("ExportPfMasterToExcel")]
        public async Task<IActionResult> ExportPfMasterToExcel([FromQuery] string ecode = null)
        {
            var bytes = await _viewService.ExportPfMasterToExcelAsync(ecode);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "PfMaster.xlsx");
        }

        [HttpGet("ExportEsicMasterToExcel")]
        public async Task<IActionResult> ExportEsicMasterToExcel([FromQuery] string ecode = null)
        {
            var bytes = await _viewService.ExportEsicMasterToExcelAsync(ecode);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "EsicMaster.xlsx");
        }

        [HttpGet("ExportTotalDeductionToExcel")]
        public async Task<IActionResult> ExportTotalDeductionToExcel([FromQuery] string ecode = null)
        {
            var bytes = await _viewService.ExportTotalDeductionToExcelAsync(ecode);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "TotalDeduction.xlsx");
        }

        [HttpGet("GetTotalDeductionList")]
        public async Task<IActionResult> GetTotalDeductionList([FromQuery] string ecode = null, [FromQuery] bool asExcel = false, [FromQuery] int? page = null, [FromQuery] int? pageSize = null)
        {
            var result = await _viewService.GetTotalDeductionListAsync(ecode, asExcel, page, pageSize);
            if (asExcel && result.Status == true && result.Data is byte[] bytes)
            {
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "TotalDeduction.xlsx");
            }
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }

        [HttpGet("GetEsicMaster")]
        public async Task<IActionResult> GetEsicMaster([FromQuery] string ecode = null, [FromQuery] bool asExcel = false, [FromQuery] int? page = null, [FromQuery] int? pageSize = null)
        {
            var result = await _viewService.GetEsicMaster(ecode, asExcel, page, pageSize);
            if (asExcel && result.Status == true && result.Data is byte[] bytes)
            {
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "EsicMaster.xlsx");
            }
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }

        [HttpGet("GetLeaveMaster")]
        public async Task<IActionResult> GetLeaveMaster([FromQuery] string ecode = null, [FromQuery] bool asExcel = false, [FromQuery] int? page = null, [FromQuery] int? pageSize = null)
        {
            var result = await _viewService.GetLeaveMaster(ecode, asExcel, page, pageSize);
            if (asExcel && result.Status == true && result.Data is byte[] bytes)
            {
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "LeaveMaster.xlsx");
            }
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }

        [HttpGet("GetPfMaster")]
        public async Task<IActionResult> GetPfMaster([FromQuery] string ecode = null, [FromQuery] bool asExcel = false, [FromQuery] int? page = null, [FromQuery] int? pageSize = null)
        {
            var result = await _viewService.GetPfMaster(ecode, asExcel, page, pageSize);
            if (asExcel && result.Status == true && result.Data is byte[] bytes)
            {
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "PfMaster.xlsx");
            }
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }

        [HttpGet("GetBgtSalaryWithEmpDetails")]
        public async Task<IActionResult> GetBgtSalaryWithEmpDetails([FromQuery] string ecode = null, [FromQuery] bool asExcel = false, [FromQuery] int? page = null, [FromQuery] int? pageSize = null)
        {
            var result = await _viewService.GetBgtSalaryWithEmpDetails(ecode, asExcel, page, pageSize);
            if (asExcel && result.Status == true && result.Data is byte[] bytes)
            {
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "BgtSalaryStructWithEmpDetails.xlsx");
            }
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }

        [HttpGet("GetEmpAttendanceFormat")]
        public async Task<IActionResult> GetEmpAttendanceFormat([FromQuery] string ecode = null, [FromQuery] bool asExcel = false, [FromQuery] int? page = null, [FromQuery] int? pageSize = null)
        {
            var result = await _viewService.GetEmpAttendanceFormat(ecode, asExcel, page, pageSize);
            if (asExcel && result.Status == true && result.Data is byte[] bytes)
            {
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "EmpAttendanceFormat.xlsx");
            }
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }

        [HttpPost("UploadEmployeeDeductionsExcel")]
        public async Task<IActionResult> UploadEmployeeDeductionsExcel([FromForm] IFormFile file)
        {
            var result = await _viewService.UploadEmployeeDeductionsExcelAsync(file);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse
            {
                Status = result.Status,
                Message = result.Message
            });
        }

        [HttpGet("GetNetPaybleList")]
        public async Task<IActionResult> GetNetPaybleList([FromQuery] string ecode = null, [FromQuery] bool asExcel = false, [FromQuery] int? page = null, [FromQuery] int? pageSize = null)
        {
            var result = await _viewService.GetNetPaybleListAsync(ecode, asExcel, page, pageSize);
            if (asExcel && result.Status == true && result.Data is byte[] bytes)
            {
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "NetPayble.xlsx");
            }
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }

        [HttpGet("GetSalaryFormat")]
        public async Task<IActionResult> GetSalaryFormat([FromQuery] string ecode = null, [FromQuery] bool asExcel = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var result = await _viewService.GetSalaryFormatAsync(ecode, asExcel, page, pageSize);
            if (asExcel && result.Status == true && result.Data is byte[] bytes)
            {
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "SalaryFormat.xlsx");
            }
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }

        [HttpGet("GetPaybleDays")]
        public async Task<IActionResult> GetPaybleDays([FromQuery] string ecode = null, [FromQuery] bool asExcel = false, [FromQuery] int? page = null, [FromQuery] int? pageSize = null)
        {
            var result = await _viewService.GetPaybleDaysAsync(ecode, asExcel, page, pageSize);
            if (asExcel && result.Status == true && result.Data is byte[] bytes)
            {
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "PaybleDays.xlsx");
            }
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }
    }
} 