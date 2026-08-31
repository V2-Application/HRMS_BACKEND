using ClosedXML.Excel;
using HRMSAPI.Extension;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Roomsy.DTOS.GenericsResponses;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HRMSAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LwfPolicyController : ControllerBase
    {
        private readonly IPolicyMasterService _service;
        public LwfPolicyController(IPolicyMasterService service)
        {
            _service = service;
        }

        // LWF policy is restricted to the IT SuperAdmin role only.
        private bool IsItSuperAdmin()
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);
            return string.Equals((userClaims?.role ?? string.Empty).Trim(), "IT SuperAdmin", System.StringComparison.OrdinalIgnoreCase);
        }

        private IActionResult ForbidItSuperAdmin() =>
            StatusCode(StatusCodes.Status403Forbidden, new ApiExecuteAndReponse { Status = false, Message = "Only IT SuperAdmin can access LWF policy." });

        [HttpGet("DownloadTemplate")]
        public IActionResult DownloadTemplate()
        {
            if (!IsItSuperAdmin()) return ForbidItSuperAdmin();
            // "Calc Type" says how Employee/Employer must be read: Flat = rupee amount,
            // Percent = percentage of gross capped by the Max column.
            var headers = new[] { "State", "Frequency", "Employee", "Employee Max", "Employer", "Employer Max", "Calc Type" };
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("LWFPolicyMaster");
            for (int i = 0; i < headers.Length; i++)
            {
                var c = ws.Cell(1, i + 1);
                c.Value = headers[i];
                c.Style.Font.Bold = true;
            }
            // sample row - a flat amount
            ws.Cell(2, 1).Value = "Maharashtra";
            ws.Cell(2, 2).Value = "Half Yearly";
            ws.Cell(2, 3).Value = "25";
            ws.Cell(2, 4).Value = "25";
            ws.Cell(2, 5).Value = "75";
            ws.Cell(2, 6).Value = "75";
            ws.Cell(2, 7).Value = "Flat";
            // sample row - a percentage of gross, capped (this is how Haryana works)
            ws.Cell(3, 1).Value = "Haryana";
            ws.Cell(3, 2).Value = "Monthly";
            ws.Cell(3, 3).Value = "0.2";
            ws.Cell(3, 4).Value = "35";
            ws.Cell(3, 5).Value = "0.4";
            ws.Cell(3, 6).Value = "70";
            ws.Cell(3, 7).Value = "Percent";
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "LWF_Policy_UploadTemplate.xlsx");
        }

        [HttpPost("UploadExcel")]
        public async Task<IActionResult> UploadExcel([FromForm] IFormFile file)
        {
            if (!IsItSuperAdmin()) return ForbidItSuperAdmin();
            var result = await _service.UploadLwfExcelAsync(file);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse { Status = result.Status, Message = result.Message });
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll([FromQuery] bool isExcel = false)
        {
            if (!IsItSuperAdmin()) return ForbidItSuperAdmin();
            var result = await _service.GetAllLwfAsync(isExcel);
            if (isExcel && result.Status == true && result.Data is byte[] bytes)
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "LWF_Policy.xlsx");
            return StatusCode((int)result.Code, new ApiFetchAndResponse { Status = result.Status, Message = result.Message, Data = result.Data });
        }

        // Update a single LWF line item by Id.
        [HttpPost("Update")]
        public async Task<IActionResult> Update([FromBody] HRMSAPI.DTO.LwfUpdateDto dto)
        {
            if (!IsItSuperAdmin()) return ForbidItSuperAdmin();
            var result = await _service.UpdateLwfAsync(dto);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse { Status = result.Status, Message = result.Message });
        }

        // Add a new LWF line item.
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] HRMSAPI.DTO.LwfUpdateDto dto)
        {
            if (!IsItSuperAdmin()) return ForbidItSuperAdmin();
            var result = await _service.CreateLwfAsync(dto);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse { Status = result.Status, Message = result.Message });
        }
    }
}
