using HRMSAPI.Models;

namespace HRMSAPI.Interfaces;

public interface IMedicalCardService
{
    Task<IReadOnlyList<Models.MedicalCardDto>> GetByEmployeeIdAsync(long employeeId);
    Task<IReadOnlyList<Models.MedicalCardDto>> GetByEcodeAsync(string ecode);
    Task<MedicalCardReparseResult> ReparseAllAsync(string updatedBy, bool dryRun = false);
    Task<MedicalCardReparseResult> ReparseForEcodeAsync(string ecode, string updatedBy);
    Task<bool> UpdateSumAssuredAsync(int cardId, decimal? sumAssured, string updatedBy);
    Task<(bool success, string message, string url)> UploadAndAttachAsync(string ecode, Microsoft.AspNetCore.Http.IFormFile file, string updatedBy);
}

public class MedicalCardReparseResult
{
    public int EmployeesProcessed { get; set; }
    public int CardsInserted { get; set; }
    public int CardsSkipped { get; set; }
    public List<string> Errors { get; set; } = new();
}
