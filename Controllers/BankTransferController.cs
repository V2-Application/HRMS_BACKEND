using HRMSAPI.Extension;
using HRMSAPI.Implementation;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace HRMSAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [RequirePageAccess("/given-to-bank")]
    public class BankTransferController : ControllerBase
    {
        private readonly IBankTransferService _bankTransferService;

        public BankTransferController(IBankTransferService payrollRepository)
        {
            _bankTransferService = payrollRepository;
        }
        [HttpPost("upload-bank-transfer")]
        public async Task<IActionResult> UploadBankTransfer(IFormFile file)
        {
            string createdBy = "system";
            var result = await _bankTransferService.UploadBankTransferDataAsync(file);
            return result.Success ? Ok(result.Message) : BadRequest(result.Message);
        }
        [HttpGet]
        public async Task<IActionResult> GetBankTransferList(
     [FromQuery] string? searchTerm = null,
     [FromQuery] string? ecode = null,
     [FromQuery] int page = 1,
     [FromQuery] int pageSize = 10)
        {
            try
            {
                var (records, totalRecords) = await _bankTransferService.GetBankTransferList(searchTerm, ecode, page, pageSize);
                return Ok(new { Records = records, TotalRecords = totalRecords });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving bank transfer records: {ex.Message}");
                return StatusCode(500, new { Message = "An error occurred while retrieving bank transfer records." });
            }
        }
    }
}