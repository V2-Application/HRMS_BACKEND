namespace HRMSAPI.Models;

/// <summary>One ZIP part in a resume download.</summary>
public class ResumeZipPart
{
    public int partNumber { get; set; }
    public int fileCount { get; set; }
    public long bytes { get; set; }
    /// <summary>Human-readable size, e.g. "487 MB" — saves the UI repeating the maths.</summary>
    public string sizeText { get; set; } = "";
    /// <summary>Suggested download name; the API sets the same value on Content-Disposition.</summary>
    public string fileName { get; set; } = "";
}

/// <summary>
/// What a resume download would contain. Produced from the database only — no file is
/// opened — so it stays fast enough to run every time the filters change.
/// </summary>
public class ResumeZipPlan
{
    /// <summary>Applicants matching the filters (whether or not they have a resume).</summary>
    public int applicantsInScope { get; set; }
    public int applicantsWithResume { get; set; }
    /// <summary>In scope but nothing uploaded — they cannot appear in the ZIP.</summary>
    public int applicantsWithoutResume { get; set; }
    public long totalBytes { get; set; }
    public string totalSizeText { get; set; } = "";
    public int partCount { get; set; }
    public long partSizeBytes { get; set; }
    public List<ResumeZipPart> parts { get; set; } = new();

    /// <summary>Echo of the window actually applied, so the UI can display what it asked for.</summary>
    public DateTime? fromDate { get; set; }
    public DateTime? toDate { get; set; }
    public bool allDates { get; set; }

    /// <summary>
    /// Sizes come from CanidateDocs.FileSize as recorded at upload time. A file that has
    /// since been moved or deleted still counts here and is reported as skipped by the
    /// download itself — flagged so a slightly smaller ZIP is not read as a bug.
    /// </summary>
    public string note { get; set; } = "";
}

/// <summary>Outcome of writing one part — what went in, and what did not.</summary>
public class ResumeZipPartResult
{
    public int partNumber { get; set; }
    public int partCount { get; set; }
    public int filesWritten { get; set; }
    public int filesMissingOnDisk { get; set; }
    public long bytesWritten { get; set; }
    public string fileName { get; set; } = "";
    public List<string> skipped { get; set; } = new();
}
