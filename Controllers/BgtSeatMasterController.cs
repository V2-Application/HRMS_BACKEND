using ClosedXML.Excel;
using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Roomsy.DTOS.GenericsResponses;
using System.IO;
using System.Threading.Tasks;

namespace HRMSAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [RequirePageAccess("/bgt-seat-uploader")]
    public class BgtSeatMasterController : ControllerBase
    {

        private readonly IBgtSeatMasterService _service;
        public BgtSeatMasterController(IBgtSeatMasterService service)
        {
            _service = service;
        }

        // Ecode of the logged-in user (from JWT) — captured for upload/delete auditing.
        private string CurrentEcode()
        {
            var identity = HttpContext.User.Identity as System.Security.Claims.ClaimsIdentity;
            return AuthenticUserDetails.GetCurrentUserDetails(identity)?.EmployeeId ?? "System";
        }

        [HttpGet("DownloadTemplate")]
        public IActionResult DownloadTemplate()
        {
            // Upload template — must match the exact header order the uploader validates,
            // including the Sub-Department 1/2/3 columns (8/9/10).
            var headers = new[]
            {
                "LOC CODE", "DEPARTMENT", "DESIGNATION", "SALARY BGT", "ORG CHART",
                "REPORTING MANAGER DESG", "ACTIVE", "SUB DEPARTMENT 1", "SUB DEPARTMENT 2", "SUB DEPARTMENT 3"
            };
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("BgtSeatMaster");
            for (int i = 0; i < headers.Length; i++)
            {
                var c = ws.Cell(1, i + 1);
                c.Value = headers[i];
                c.Style.Font.Bold = true;
            }
            // sample row (Sub-Dept columns are optional per row — left mostly blank as a hint)
            ws.Cell(2, 1).Value = "HP11";
            ws.Cell(2, 2).Value = "RETAIL OPERATION";
            ws.Cell(2, 3).Value = "LOBM";
            ws.Cell(2, 7).Value = "Active";
            ws.Cell(2, 8).Value = "ZONE-3";
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "BgtSeatMaster_UploadTemplate.xlsx");
        }

        [HttpPost("UploadExcel")]
        public async Task<IActionResult> UploadExcel([FromForm] IFormFile file)
        {
            var result = await _service.UploadBgtSeatMasterExcelAsync(file, CurrentEcode());
            return StatusCode((int)result.Code, new ApiExecuteAndReponse
            {
                Status = result.Status,
                Message = result.Message
            });
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll([FromQuery] bool isExcel = false)
        {
            var result = await _service.GetAllBgtSeatMasterAsync(isExcel);
            if (isExcel && result.Status == true && result.Data is byte[] bytes)
            {
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "BgtSeatMaster.xlsx");
            }
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }

		[HttpPost("DeleteBySeries")]
		public async Task<IActionResult> DeleteBySeries([FromQuery] string locCode, [FromQuery] int deptSno, [FromQuery] int desgSno, [FromQuery] int deleteCount = 1)
		{
			var result = await _service.DeleteSeatsBySeriesAsync(locCode, deptSno, desgSno, deleteCount, CurrentEcode());
			return StatusCode((int)result.Code, new ApiExecuteAndReponse
			{
				Status = result.Status,
				Message = result.Message
			});
		}

		// Precise delete of specific seat entries - one (single row) or many (bulk).
		[HttpPost("DeleteSeats")]
		public async Task<IActionResult> DeleteSeats([FromBody] System.Collections.Generic.List<HRMSAPI.DTO.BgtSeatDeleteItem> seats)
		{
			var result = await _service.DeleteSeatsAsync(seats, CurrentEcode());
			return StatusCode((int)result.Code, new ApiExecuteAndReponse
			{
				Status = result.Status,
				Message = result.Message
			});
		}

		// Delete ALL budget seats for one or more stores (LOC_CODE list). Backs up the affected rows first.
		[HttpPost("DeleteByStore")]
		public async Task<IActionResult> DeleteByStore([FromBody] System.Collections.Generic.List<string> locCodes)
		{
			var result = await _service.DeleteSeatsByStoreAsync(locCodes, CurrentEcode());
			return StatusCode((int)result.Code, new ApiExecuteAndReponse
			{
				Status = result.Status,
				Message = result.Message
			});
		}

		// Delete EVERY budget seat (whole table). Requires confirm=DELETEALL. Backs up the full table first.
		[HttpPost("DeleteAll")]
		public async Task<IActionResult> DeleteAll([FromQuery] string confirm)
		{
			if (!string.Equals(confirm?.Replace(" ", ""), "DELETEALL", System.StringComparison.OrdinalIgnoreCase))
				return BadRequest(new ApiExecuteAndReponse
				{
					Status = false,
					Message = "Confirmation required. Pass confirm=DELETEALL to delete every budget seat."
				});

			var result = await _service.DeleteAllSeatsAsync(CurrentEcode());
			return StatusCode((int)result.Code, new ApiExecuteAndReponse
			{
				Status = result.Status,
				Message = result.Message
			});
		}

    }
} 