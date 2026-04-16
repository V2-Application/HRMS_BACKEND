using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Roomsy.DTOS.GenericsResponses;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HRMSAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmpAttendanceViewSnapshotController : ControllerBase
    {
        private readonly IEmpAttendanceViewSnapshotService _service;

        public EmpAttendanceViewSnapshotController(IEmpAttendanceViewSnapshotService service)
        {
            _service = service;
        }

        [HttpGet("get-snapshots")]
        public async Task<IActionResult> GetSnapshots([FromQuery] string month = null, [FromQuery] int? status = null, [FromQuery] string ecode = null, [FromQuery] string batch = null)
        {
            // Validate month format if provided
            if (!string.IsNullOrWhiteSpace(month) && !Regex.IsMatch(month, @"^[A-Z][a-z]{2}-\d{2}$", RegexOptions.IgnoreCase))
            {
                return BadRequest(new FetchAndResponse()
                {
                    Status = false,
                    Message = "Month format must be 'MMM-YY' (e.g., Jul-25)"
                });
            }

            // Call the service
            var result = await _service.GetEmpAttendanceViewSnapshotsAsync(month, status, ecode, batch);

            if (result.Status)
                return Ok(result);

            return StatusCode(500, result);
        }

        [HttpPost("salary-process-to-given-to-bank-or-paid-by-cash"), Authorize]
        public async Task<IActionResult> SalaryProcessToGivenToBankOrPaidByCash([FromBody] UpdateSalaryStatusRequestDto request)
        {
            if (request.Status != 2 && request.Status != 3)
            {
                return BadRequest(new ExecuteAndReponse
                {
                    Status = false,
                    Message = "Only status 2 (Bank Transfer) and status 3 (Paid in Cash) are supported for this operation."
                });
            }
            
            var result = await _service.SalaryProcessToGivenToBankOrPaidByCash(request.ID, request.Status);
            
            if (result.Status)
                return Ok(result);
                
            return StatusCode(500, result);
        }

        [HttpGet("get-salary-status-list")]
        public async Task<IActionResult> GetSalaryStatusList([FromQuery] int status, [FromQuery] string month = null)
        {
            // Validate status
            if (status < 2 || status > 5)
            {
                return BadRequest(new FetchAndResponse()
                {
                    Status = false,
                    Message = "Status must be 2 (GivenToBank), 3 (PaidInCash), 4 (PaidByBank), or 5 (ReturnByBank)."
                });
            }

            // Validate month format if provided
            if (!string.IsNullOrWhiteSpace(month) && !Regex.IsMatch(month, @"^[A-Z][a-z]{2}-\d{2}$", RegexOptions.IgnoreCase))
            {
                return BadRequest(new FetchAndResponse()
                {
                    Status = false,
                    Message = "Month format must be 'MMM-YY' (e.g., Jul-25)"
                });
            }

            var result = await _service.GetSalaryStatusList(status, month);

            if (result.Status)
                return Ok(result);

            return StatusCode(500, result);
        }

        [HttpPost("given-to-bank-to-paid-by-bank-or-return-from-bank"), Authorize]
        public async Task<IActionResult> GivenToBankToPaidByBankOrReturnFromBank([FromBody] UpdateBankTransferStatusRequestDto request)
        {
            if (request.StatusId != 4 && request.StatusId != 5)
            {
                return BadRequest(new ExecuteAndReponse
                {
                    Status = false,
                    Message = "Only status 4 (Paid by Bank) and status 5 (Return by Bank) are supported for this operation."
                });
            }
            
            var result = await _service.GivenToBankToPaidByBankOrReturnFromBank(request.Id, request.StatusId, request.BatchId);
            
            if (result.Status)
                return Ok(result);
                
            return StatusCode(500, result);
        }

        [HttpPost("process-excel-upload"), Authorize]
        public async Task<IActionResult> ProcessExcelUpload([FromForm] ExcelUploadRequestDto request)
        {
            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest(new ExecuteAndReponse
                {
                    Status = false,
                    Message = "No file uploaded."
                });
            }

            var result = await _service.ProcessExcelUploadAsync(request.File);
            
            if (result.Status)
                return Ok(result);
                
            return StatusCode(500, result);
        }

        [HttpPost("process-given-to-bank-excel-upload"), Authorize]
        public async Task<IActionResult> ProcessGivenToBankExcelUpload([FromForm] GivenToBankExcelUploadRequestDto request)
        {
            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest(new ExecuteAndReponse
                {
                    Status = false,
                    Message = "No file uploaded."
                });
            }

            var result = await _service.ProcessGivenToBankExcelUploadAsync(request.File);
            
            if (result.Status)
                return Ok(result);
                
            return StatusCode(500, result);
        }

        [HttpGet("get-comprehensive-salary-status-list")]
        public async Task<IActionResult> GetComprehensiveSalaryStatusList(
            [FromQuery] string month = null,
            [FromQuery] string ecode = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50)
        {
            // Validate month format if provided
            if (!string.IsNullOrWhiteSpace(month) && !Regex.IsMatch(month, @"^[A-Z][a-z]{2}-\d{2}$", RegexOptions.IgnoreCase))
            {
                return BadRequest(new FetchAndResponse()
                {
                    Status = false,
                    Message = "Month format must be 'MMM-YY' (e.g., Jul-25)"
                });
            }

            // Validate pagination parameters
            if (pageNumber < 1)
            {
                return BadRequest(new FetchAndResponse()
                {
                    Status = false,
                    Message = "Page number must be greater than 0"
                });
            }

            if (pageSize < 1 || pageSize > 1000)
            {
                return BadRequest(new FetchAndResponse()
                {
                    Status = false,
                    Message = "Page size must be between 1 and 1000"
                });
            }

            var result = await _service.GetComprehensiveSalaryStatusList(month, ecode, pageNumber, pageSize);
            
            if (result.Status)
                return Ok(result);
                
            return StatusCode(500, result);
        }

        [HttpGet("eligible-employees")]
        public async Task<IActionResult> GetEligibleEmployees(
            [FromQuery] string stCode = "RH01",
            [FromQuery] string month = null)
        {
            if (!string.IsNullOrWhiteSpace(month) && !Regex.IsMatch(month, @"^[A-Z][a-z]{2}-\d{2}$", RegexOptions.IgnoreCase))
            {
                return BadRequest(new FetchAndResponse()
                {
                    Status = false,
                    Message = "Month format must be 'MMM-YY' (e.g., Oct-25)"
                });
            }

            var result = await _service.GetEmployeesMissingOrReturnedAsync(stCode, month);
            return StatusCode((int)result.Code, result);
        }

        [HttpGet("EmployeeSalarySnapShotByEcode")]
        public async Task<IActionResult> GetEligibleEmployeesFast(
            [FromQuery] string ecode = null,
            [FromQuery] string month = null)
        {
            if (!string.IsNullOrWhiteSpace(month) && !Regex.IsMatch(month, @"^[A-Z][a-z]{2}-\d{2}$", RegexOptions.IgnoreCase))
            {
                return BadRequest(new FetchAndResponse()
                {
                    Status = false,
                    Message = "Month format must be 'MMM-YY' (e.g., Oct-25)"
                });
            }

            var result = await _service.GetEligibleEmployeesFastAsync(ecode, month);
            return StatusCode((int)result.Code, result);
        }

        [HttpPost("update-status/{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateStatus([FromRoute] long id, [FromQuery] int status)
        {
            // Validate status - only allow 1 (approve) or -1 (reject)
            if (status != 1 && status != -1)
            {
                return BadRequest(new ExecuteAndReponse
                {
                    Status = false,
                    Message = $"Invalid status value: {status}. Only 1 (approve) or -1 (reject) are allowed."
                });
            }

            var result = await _service.UpdateStatusByIdAsync(id, status);

            if (result.Status)
                return Ok(result);

            // Check if it's a not found error (could return 404) or other error (500)
            if (result.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return NotFound(result);

            return StatusCode(500, result);
        }
    }
}

