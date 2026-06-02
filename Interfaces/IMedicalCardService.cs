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
    Task<MedicalCardBulkUploadResult> BulkUploadAsync(IEnumerable<Microsoft.AspNetCore.Http.IFormFile> files, string updatedBy, bool skipReparse = false);
}

public class MedicalCardReparseResult
{
    public int EmployeesProcessed { get; set; }
    public int CardsInserted { get; set; }
    public int CardsSkipped { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class MedicalCardBulkUploadResult
{
    public int TotalFiles { get; set; }
    public int SavedCount { get; set; }
    public int SkippedCount { get; set; }
    public int CardsParsed { get; set; }
    // Per-file outcome, in input order, so the UI can show a row-by-row report.
    public List<MedicalCardBulkUploadItem> Items { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class MedicalCardBulkUploadItem
{
    public string FileName { get; set; }
    public string Ecode { get; set; }
    public bool Saved { get; set; }
    public string Url { get; set; }
    public string Error { get; set; }
}
