using HRMSAPI.DTO;
using HRMSAPI.Models;

namespace HRMSAPI.Interfaces;

/// <summary>
/// Bulk download of applicant resumes as ZIP part files.
/// Read-only: nothing here writes to the database or to disk outside the OS temp folder.
/// </summary>
public interface IApplicantResumeService
{
    /// <summary>
    /// Works out what a download would contain WITHOUT reading any file, so the UI can
    /// show the size before a multi-GB transfer is started. Also returns the part plan.
    /// </summary>
    Task<ResumeZipPlan> BuildPlanAsync(
        JwtLoginDetailDto loginDetail,
        int statusId,
        string searchTerm,
        DateTime? fromDate,
        DateTime? toDate,
        bool allDates,
        long partSizeBytes,
        CancellationToken ct = default);

    /// <summary>
    /// Writes one part of the plan to <paramref name="destinationPath"/> as a ZIP.
    /// Returns the manifest of what actually went in, including anything skipped.
    /// </summary>
    Task<ResumeZipPartResult> WritePartAsync(
        JwtLoginDetailDto loginDetail,
        int statusId,
        string searchTerm,
        DateTime? fromDate,
        DateTime? toDate,
        bool allDates,
        long partSizeBytes,
        int partNumber,
        string destinationPath,
        CancellationToken ct = default);
}
