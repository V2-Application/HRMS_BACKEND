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
    public class PtaxPolicyController : ControllerBase
    {
        private readonly IPolicyMasterService _service;
        public PtaxPolicyController(IPolicyMasterService service)
        {
            _service = service;
        }

        // PTax policy is restricted to the IT SuperAdmin role only.
        private bool IsItSuperAdmin()
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);
            return string.Equals((userClaims?.role ?? string.Empty).Trim(), "IT SuperAdmin", System.StringComparison.OrdinalIgnoreCase);
        }

        private IActionResult ForbidItSuperAdmin() =>
            StatusCode(StatusCodes.Status403Forbidden, new ApiExecuteAndReponse { Status = false, Message = "Only IT SuperAdmin can access PTax policy." });

        [HttpGet("DownloadTemplate")]
        public IActionResult DownloadTemplate()
        {
            if (!IsItSuperAdmin()) return ForbidItSuperAdmin();
            var headers = new[] { "State", "Slab Min", "Slab Max", "PT Rate", "Frequency", "Gender" };
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("PTPolicyMaster");
            for (int i = 0; i < headers.Length; i++)
            {
                var c = ws.Cell(1, i + 1);
                c.Value = headers[i];
                c.Style.Font.Bold = true;
            }
            // sample row
            ws.Cell(2, 1).Value = "Maharashtra";
            ws.Cell(2, 2).Value = "0";
            ws.Cell(2, 3).Value = "7500";
            ws.Cell(2, 4).Value = "0";
            ws.Cell(2, 5).Value = "Monthly";
            ws.Cell(2, 6).Value = "Male";
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "PTax_Policy_UploadTemplate.xlsx");
        }

        [HttpPost("UploadExcel")]
        public async Task<IActionResult> UploadExcel([FromForm] IFormFile file)
        {
            if (!IsItSuperAdmin()) return ForbidItSuperAdmin();
            var result = await _service.UploadPtaxExcelAsync(file);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse { Status = result.Status, Message = result.Message });
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll([FromQuery] bool isExcel = false)
        {
            if (!IsItSuperAdmin()) return ForbidItSuperAdmin();
            var result = await _service.GetAllPtaxAsync(isExcel);
            if (isExcel && result.Status == true && result.Data is byte[] bytes)
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "PTax_Policy.xlsx");
            return StatusCode((int)result.Code, new ApiFetchAndResponse { Status = result.Status, Message = result.Message, Data = result.Data });
        }

        // Update a single PTax line item by Id.
        [HttpPost("Update")]
        public async Task<IActionResult> Update([FromBody] HRMSAPI.DTO.PtaxUpdateDto dto)
        {
            if (!IsItSuperAdmin()) return ForbidItSuperAdmin();
            var result = await _service.UpdatePtaxAsync(dto);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse { Status = result.Status, Message = result.Message });
        }

        // Add a new PTax slab/line item.
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] HRMSAPI.DTO.PtaxUpdateDto dto)
        {
            if (!IsItSuperAdmin()) return ForbidItSuperAdmin();
            var result = await _service.CreatePtaxAsync(dto);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse { Status = result.Status, Message = result.Message });
        }
    }
}
