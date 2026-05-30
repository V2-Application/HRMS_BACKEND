using System.Security.Claims;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMSAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class MedicalCardController : ControllerBase
{
    private readonly IMedicalCardService _svc;
    private readonly ILogger<MedicalCardController> _log;

    public MedicalCardController(IMedicalCardService svc, ILogger<MedicalCardController> log)
    {
        _svc = svc;
        _log = log;
    }

    // GET api/MedicalCard/by-employee/{employeeId}
    [HttpGet("by-employee/{employeeId:long}")]
    public async Task<IActionResult> GetByEmployeeId(long employeeId)
    {
        var data = await _svc.GetByEmployeeIdAsync(employeeId);
        return Ok(new { status = true, data });
    }

    // GET api/MedicalCard/by-ecode/{ecode}
    [HttpGet("by-ecode/{ecode}")]
    public async Task<IActionResult> GetByEcode(string ecode)
    {
        var data = await _svc.GetByEcodeAsync(ecode);
        return Ok(new { status = true, data });
    }

    // PATCH api/MedicalCard/{cardId}/sum-assured
    public class SumAssuredPatch { public decimal? sumAssured { get; set; } }

    [HttpPatch("{cardId:int}/sum-assured")]
    public async Task<IActionResult> UpdateSumAssured(int cardId, [FromBody] SumAssuredPatch body)
    {
        var user = User?.Identity?.Name ?? "System";
        var ok = await _svc.UpdateSumAssuredAsync(cardId, body?.sumAssured, user);
        if (!ok) return NotFound(new { status = false, message = "Card not found" });
        return Ok(new { status = true });
    }

    // POST api/MedicalCard/reparse-all  — admin one-shot.
    // Re-parses every employee with MedicalCardUrl set; preserves SumAssured per CardOrder.
    [HttpPost("reparse-all")]
    public async Task<IActionResult> ReparseAll([FromQuery] bool dryRun = false)
    {
        var user = User?.Identity?.Name ?? "System";
        var result = await _svc.ReparseAllAsync(user, dryRun);
        return Ok(new { status = true, result });
    }

    // POST api/MedicalCard/reparse/{ecode}
    [HttpPost("reparse/{ecode}")]
    public async Task<IActionResult> ReparseOne(string ecode)
    {
        var user = User?.Identity?.Name ?? "System";
        var result = await _svc.ReparseForEcodeAsync(ecode, user);
        return Ok(new { status = true, result });
    }

    // POST api/MedicalCard/upload/{ecode}  — uploads a PDF, sets
    // tblEmployee.MedicalCardUrl, and reparses cards for the ecode.
    [HttpPost("upload/{ecode}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadPdf(string ecode, IFormFile file)
    {
        var user = User?.Identity?.Name ?? "System";
        var r = await _svc.UploadAndAttachAsync(ecode, file, user);
        if (!r.success) return BadRequest(new { status = false, message = r.message });
        return Ok(new { status = true, url = r.url });
    }
}
