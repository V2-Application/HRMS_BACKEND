using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Mvc;
using HRMSAPI.Services;
using System.Security.Claims;
using HRMSAPI.Extension;

namespace HRMSAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class FnfController : ControllerBase
    {
        private readonly IFnfService _service;
        public FnfController(IFnfService service) => _service = service;

        [HttpGet("FetchEmployeesForFNF")]
        public async Task<IActionResult> FetchEmployeesForFNF([FromQuery] string? ecode, [FromQuery] string? search, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var data = await _service.FetchEmployeesForFNF(ecode, search, fromDate, toDate, page, pageSize);
                return Ok(new { Status = true, Message = "Employees fetched", Data = data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Status = false, Message = "Fetch failed", Error = ex.Message });
            }
        }

        [HttpPost("save")]
        public async Task<IActionResult> SaveAll([FromBody] FnfSaveAllDto dto)
        {
            var res = await _service.SaveAllAsync(dto);
            return Ok(new { Status = true, Message = "FNF saved", Data = res });
        }
        [HttpGet("FNFDoneList")]
        public async Task<IActionResult> GetList([FromQuery] string? search, [FromQuery] DateTime? from,
                                                 [FromQuery] DateTime? to, [FromQuery] string? paymentStatus,
                                                 [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var data = await _service.GetAccountsListAsync(search, from, to, paymentStatus, page, pageSize);
            return Ok(new { Status = true, Message = "FnF accounts list", Data = data });
        }
        [HttpGet("FNFProcessedList")]
        public async Task<IActionResult> GetProcessedList([FromQuery] string? search, [FromQuery] DateTime? from,
                                                 [FromQuery] DateTime? to, [FromQuery] string? paymentStatus,
                                                 [FromQuery] int page = 1, [FromQuery] int pageSize = 20) {
            var data = await _service.GetProcessedListAsync(search, from, to, paymentStatus, page, pageSize);
            return Ok(new { Status = true, Message = "FnF Processed list", Data = data });
        }
        [HttpPost("bonus")]
        public async Task<IActionResult> CalcBonus([FromBody] BonusCalcRequestDto dto)
        {
            var (rows, totals) = await _service.CalculateBonusAsync(dto);
            return Ok(new { Status = true, Message = "Bonus calculated", Data = new { Rows = rows, Totals = totals } });
        }
        [HttpPost("leave-encashment")]
        public async Task<IActionResult> CalcLeaveEncashment([FromBody] LeaveEncashmentRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Ecode))
                return BadRequest(new { Status = false, Message = "ecode is required" });

            var data = await _service.CalculateLeaveEncashmentAsync(dto);
            return Ok(new { Status = true, Message = "Leave encashment", Data = data });
        }

        [HttpGet("gratuity")]
        public async Task<IActionResult> CalcGratuity([FromQuery] string? ecode, [FromQuery] long? employeeId)
        {
            var data = await _service.CalculateGratuityAsync(new GratuityRequestDto { Ecode = ecode, EmployeeId = employeeId });
            return Ok(new { Status = true, Message = "Gratuity", Data = data });
        }

        [HttpPost("bulk-upload")]
        public async Task<IActionResult> BulkUpload([FromBody] FnfBulkUploadRequestDto request)
        {
            try
            {
                // Get current user
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);
                request.User = userClaims?.EmployeeId ?? "System";
                
                var result = await _service.BulkUploadAsync(request);
                return Ok(new { 
                    Status = result.Success, 
                    Message = result.Message,
                    Data = new {
                        ProcessedCount = result.ProcessedCount,
                        TotalRecords = result.TotalRecords,
                        DuplicateEcodes = result.DuplicateEcodes,
                        AlreadyDoneEcodes = result.AlreadyDoneEcodes
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Status = false, Message = "Bulk upload error", Error = ex.Message });
            }
        }

        [HttpPost("bulk-upload-excel")]
        public async Task<IActionResult> BulkUploadFromExcel(IFormFile file)
        {
            try
            {
                // Get current user
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);
                
                var ok = await _service.BulkUploadFromExcelAsync(file, userClaims?.EmployeeId ?? "System");
                return Ok(new { Status = ok, Message = ok ? "Excel bulk upload processed" : "Excel bulk upload failed" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Status = false, Message = "Excel bulk upload error", Error = ex.Message });
            }
        }

        [HttpPost("bulk-upload-excel-processed")]
        public async Task<IActionResult> BulkUploadProcessedFromExcel(IFormFile file)
        {
            try
            {
                // Get current user
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);

                var response = await _service.BulkUploadProcessedFromExcelAsync(file, userClaims?.EmployeeId ?? "System");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Status = false, Message = "Excel bulk upload error", Error = ex.Message });
            }
        }

        [HttpPost("upload-completed-fnf-excel")]
        public async Task<IActionResult> UploadCompletedFNFExcel(IFormFile file)
        {
            try
            {
                // Get current user
                var identity = HttpContext.User.Identity as ClaimsIdentity;
                var userClaims = AuthenticUserDetails.GetCurrentUserDetails(identity);
                
                var result = await _service.UploadCompletedFNFExcelAsync(file, userClaims?.EmployeeId ?? "System");
                return Ok(new { 
                    Status = result.Success, 
                    Message = result.Message,
                    Data = new {
                        ProcessedCount = result.ProcessedCount,
                        TotalRecords = result.TotalRecords,
                        DuplicateEcodes = result.DuplicateEcodes,
                        AlreadyDoneEcodes = result.AlreadyDoneEcodes
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Status = false, Message = "Completed FNF Excel upload error", Error = ex.Message });
            }
        }

        [HttpPut("{fnfId}/payment-status")]
        public async Task<IActionResult> UpdatePaymentStatus(long fnfId, [FromBody] UpdateFNFPaymentStatusDto dto)
        {
            try
            {
                var allowedStatuses = new[] { "PENDING", "Transfered", "Rejected" };
                if (!allowedStatuses.Contains(dto.Status))
                {
                    return BadRequest(new { Status = false, Message = "Invalid status. Allowed values: PENDING, Transfered, Rejected." });
                }

                var rowsUpdated = await _service.UpdatePaymentStatusAsync(fnfId, dto.Status, dto.Remarks);
                
                if (rowsUpdated == 0)
                {
                    return NotFound(new { Status = false, Message = "No records updated. Check FNFId." });
                }

                return Ok(new { Status = true, Message = "Payment status and remarks updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Status = false, Message = "Update failed", Error = ex.Message });
            }
        }

        [HttpGet("export-excel")]
        public async Task<IActionResult> ExportToExcel([FromQuery] string? search, [FromQuery] DateTime? from,
                                                     [FromQuery] DateTime? to, [FromQuery] string? paymentStatus)
        {
            try
            {
                var excelData = await _service.ExportToExcelAsync(search, from, to, paymentStatus);
                
                var fileName = $"FNF_Accounts_List_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                
                Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");
                return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Status = false, Message = "Export failed", Error = ex.Message });
            }
        }
        [HttpGet("export-pending-excel")]
        public async Task<IActionResult> ExportPendingExcel()
        {
            try
            {
                var excelData = await _service.ExportPendingToExcelAsync();
                var fileName = $"FNF_Pending_List_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");
                return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Status = false, Message = "Failed to export data.", Error = ex.Message });
            }
        }

        [HttpPost("fnf-pending-to-processing")]
        public async Task<IActionResult> FnfPendingToProcessing([FromBody] long employeeId)
        {
            try
            {
                if (employeeId <= 0)
                {
                    return StatusCode(500, new { Status = false, Message = "Invalid Employee ID." });
                }

                var response = await _service.FnfPendingToProcessing(employeeId);
                return Ok(response);
            }
            catch(Exception ex) {
                return StatusCode(500, new { Error = ex.Message });
            }
        }
    }
}
