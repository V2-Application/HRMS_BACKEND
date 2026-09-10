using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using HRMSAPI.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HRMSAPI.Implementation;

/// <summary>
/// Bulk resume download for the Applicant List.
///
/// SCOPE comes from dbo.sp_GetApplicantListNew01 — the same proc the list and the Excel
/// export use — so a download contains exactly the applicants the user can see on screen,
/// including that proc's role-based visibility. Re-implementing the filter here would be
/// the easy way to quietly hand someone rows they are not allowed to see.
///
/// PARTS: applicant resumes total ~6.4 GB on prod. One 6 GB browser download is a single
/// point of failure, so the set is cut into ~500 MB parts that download independently.
/// Ordering is by candidate Id ASCENDING on purpose: new applicants get higher Ids, so
/// they only ever extend the LAST part and every earlier part keeps the same contents
/// between the estimate call and the downloads that follow it.
/// </summary>
public class ApplicantResumeService : IApplicantResumeService
{
    private readonly HRMSContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ApplicantResumeService> _log;

    public ApplicantResumeService(HRMSContext context, IWebHostEnvironment env, ILogger<ApplicantResumeService> log)
    {
        _context = context;
        _env = env;
        _log = log;
    }

    private sealed class ResumeItem
    {
        public long CandidateId { get; init; }
        public string ApplicantCode { get; init; } = "";
        public string Name { get; init; } = "";
        public DateTime? AppliedOn { get; init; }
        public string RelativePath { get; init; } = "";
        public long Bytes { get; init; }
    }

    public async Task<ResumeZipPlan> BuildPlanAsync(
        JwtLoginDetailDto loginDetail, int statusId, string searchTerm,
        DateTime? fromDate, DateTime? toDate, bool allDates, long partSizeBytes,
        CancellationToken ct = default)
    {
        if (partSizeBytes <= 0) partSizeBytes = 500L * 1024 * 1024;

        var (items, inScope, withoutResume) = await GatherAsync(
            loginDetail, statusId, searchTerm, fromDate, toDate, allDates, ct);

        var plan = new ResumeZipPlan
        {
            applicantsInScope = inScope,
            applicantsWithResume = items.Count,
            applicantsWithoutResume = withoutResume,
            totalBytes = items.Sum(i => i.Bytes),
            partSizeBytes = partSizeBytes,
            fromDate = fromDate,
            toDate = toDate,
            allDates = allDates,
            note = "Sizes are the values recorded when each file was uploaded. If a file has "
                 + "since been moved or removed from disk it still counts here, and the download "
                 + "reports it as skipped — so a part can arrive slightly smaller than shown."
        };
        plan.totalSizeText = FormatSize(plan.totalBytes);

        foreach (var part in Partition(items, partSizeBytes))
        {
            plan.parts.Add(new ResumeZipPart
            {
                partNumber = part.Index,
                fileCount = part.Items.Count,
                bytes = part.Items.Sum(i => i.Bytes),
                sizeText = FormatSize(part.Items.Sum(i => i.Bytes)),
                fileName = BuildFileName(part.Index, 0, fromDate, toDate, allDates)
            });
        }
        plan.partCount = plan.parts.Count;
        // File names embed "of N", which is only known once every part is counted.
        foreach (var p in plan.parts)
            p.fileName = BuildFileName(p.partNumber, plan.partCount, fromDate, toDate, allDates);

        return plan;
    }

    public async Task<ResumeZipPartResult> WritePartAsync(
        JwtLoginDetailDto loginDetail, int statusId, string searchTerm,
        DateTime? fromDate, DateTime? toDate, bool allDates, long partSizeBytes,
        int partNumber, string destinationPath, CancellationToken ct = default)
    {
        if (partSizeBytes <= 0) partSizeBytes = 500L * 1024 * 1024;

        var (items, _, _) = await GatherAsync(loginDetail, statusId, searchTerm, fromDate, toDate, allDates, ct);
        var parts = Partition(items, partSizeBytes);

        if (parts.Count == 0)
            throw new InvalidOperationException("No resumes match the selected filters.");
        if (partNumber < 1 || partNumber > parts.Count)
            // InvalidOperationException, not ArgumentOutOfRangeException: the latter appends
            // "(Parameter 'partNumber')" to the message, which then surfaces in the UI.
            throw new InvalidOperationException(
                $"Part {partNumber} does not exist — this selection has {parts.Count} part(s).");

        var chosen = parts[partNumber - 1];
        var result = new ResumeZipPartResult
        {
            partNumber = partNumber,
            partCount = parts.Count,
            fileName = BuildFileName(partNumber, parts.Count, fromDate, toDate, allDates)
        };

        var root = ResolveDocumentRoot();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var manifest = new StringBuilder();
        manifest.AppendLine("ApplicantCode,CandidateId,Name,AppliedOn,FileInZip,Status");

        await using (var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None,
                                             81920, useAsync: true))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false))
        {
            // Reads run AHEAD of the zip writer, several at a time.
            //
            // Building a part is dominated by per-file latency, not bandwidth: measured
            // against the deployed file share it costs ~151 ms per file but only reaches
            // ~1.95 MB/s, so 1,393 files spend ~210 s of a ~308 s build simply waiting on
            // round trips. Overlapping those waits measured 8.6 MB/s — 4.4x — and it
            // helps on local disk too, just less dramatically.
            //
            // ZipArchive is NOT safe for concurrent entry writes, so only the READS are
            // parallel; entries are still written one at a time, in the original order,
            // which keeps part contents byte-for-byte identical to the sequential version.
            var pending = new Queue<Task<PrefetchedFile>>();
            var queued = 0;
            long queuedBytes = 0;

            // The window is bounded by BOTH count and bytes: resumes average ~360 KB but
            // the odd .mp4 turns up, and 8 large files in flight would spike memory.
            const int maxAhead = 8;
            const long maxAheadBytes = 64L * 1024 * 1024;

            void Fill()
            {
                while (queued < chosen.Items.Count && pending.Count < maxAhead && queuedBytes < maxAheadBytes)
                {
                    var item = chosen.Items[queued++];
                    queuedBytes += item.Bytes;
                    pending.Enqueue(ReadAheadAsync(item, root, ct));
                }
            }

            Fill();
            while (pending.Count > 0)
            {
                ct.ThrowIfCancellationRequested();

                var file = await pending.Dequeue();
                queuedBytes -= file.Item.Bytes;
                if (queuedBytes < 0) queuedBytes = 0;
                Fill();

                if (file.Error != null)
                {
                    // Recorded in the DB but unreadable on disk. Report it rather than
                    // failing the whole part — one bad row must not cost the other few
                    // hundred files.
                    result.filesMissingOnDisk++;
                    result.skipped.Add($"{Describe(file.Item)} — {file.Error}");
                    manifest.AppendLine(ManifestRow(file.Item, "", file.Missing ? "MISSING ON DISK" : "ERROR"));
                    continue;
                }

                var entryName = UniqueEntryName(file.Item, usedNames);
                try
                {
                    // NoCompression is deliberate: resumes are pdf/jpg/docx, already
                    // compressed, so deflating them burns CPU for ~no size saving.
                    var entry = zip.CreateEntry(entryName, CompressionLevel.NoCompression);
                    await using var dst = entry.Open();
                    await dst.WriteAsync(file.Data, 0, file.Data.Length, ct);

                    result.filesWritten++;
                    result.bytesWritten += file.Data.Length;
                    manifest.AppendLine(ManifestRow(file.Item, entryName, "INCLUDED"));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    result.filesMissingOnDisk++;
                    result.skipped.Add($"{Describe(file.Item)} — {ex.GetType().Name}: {ex.Message}");
                    manifest.AppendLine(ManifestRow(file.Item, entryName, "ERROR"));
                    _log.LogWarning(ex, "Resume zip: failed to add {Entry}", entryName);
                }
            }

            // Always ship the manifest, so a short ZIP can be explained without
            // going back to the database.
            var manifestEntry = zip.CreateEntry("_manifest.csv", CompressionLevel.Optimal);
            await using var ms = manifestEntry.Open();
            var bytes = Encoding.UTF8.GetBytes(manifest.ToString());
            await ms.WriteAsync(bytes, 0, bytes.Length, ct);
        }

        return result;
    }

    /// <summary>
    /// Applicants in scope (from the list proc) joined to their newest resume.
    /// Returns the resume items plus the in-scope and no-resume counts.
    /// </summary>
    private async Task<(List<ResumeItem> Items, int InScope, int WithoutResume)> GatherAsync(
        JwtLoginDetailDto loginDetail, int statusId, string searchTerm,
        DateTime? fromDate, DateTime? toDate, bool allDates, CancellationToken ct)
    {
        searchTerm = searchTerm?.Trim();

        int? roleId = await (from e in _context.tblEmployees
                             join er in _context.tblEmployeeRoles on e.EmployeeId equals er.EmployeeId
                             join r in _context.tblRoles on er.RoleId equals r.RoleId
                             where e.EmployeeId.ToString() == loginDetail.EmployeeId
                             select r.RoleId).FirstOrDefaultAsync(ct);

        int? employeeId = loginDetail.EmployeeId != null ? Convert.ToInt32(loginDetail.EmployeeId) : null;

        var connStr = _context.Database.GetConnectionString();

        // Whole days, inclusive at both ends: "01 Sep to 03 Sep" must include everything
        // stamped on the 3rd, not stop at 00:00 that morning.
        var from = allDates ? (DateTime?)null : fromDate?.Date;
        var to = allDates ? (DateTime?)null : toDate?.Date.AddDays(1).AddTicks(-1);

        var inScope = 0;
        var withoutResume = 0;
        var scoped = new List<(long Id, string Code, string Name, DateTime? Applied, string ResumeLink)>();

        await using (var conn = new SqlConnection(connStr))
        {
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "dbo.sp_GetApplicantListNew01";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandTimeout = 600;
            cmd.Parameters.Add(new SqlParameter("@PageNumber", SqlDbType.Int) { Value = 1 });
            cmd.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = int.MaxValue });
            cmd.Parameters.Add(new SqlParameter("@StatusId", SqlDbType.Int) { Value = statusId });
            cmd.Parameters.Add(new SqlParameter("@SearchTerm", SqlDbType.NVarChar, 200)
            { Value = string.IsNullOrWhiteSpace(searchTerm) ? DBNull.Value : searchTerm });
            cmd.Parameters.Add(new SqlParameter("@RoleId", SqlDbType.Int) { Value = (object?)roleId ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@EmployeeId", SqlDbType.Int) { Value = (object?)employeeId ?? DBNull.Value });

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            // First result set is the counts header; the applicant rows are the second.
            if (!await reader.NextResultAsync(ct))
                return (new List<ResumeItem>(), 0, 0);

            while (await reader.ReadAsync(ct))
            {
                var applied = Get<DateTime?>(reader, "DateOfApply") ?? Get<DateTime?>(reader, "CreatedOn");
                if (!allDates)
                {
                    if (applied == null) continue;                 // cannot place it in the window
                    if (from != null && applied < from) continue;
                    if (to != null && applied > to) continue;
                }

                inScope++;

                var link = Get<string>(reader, "ResumeLink") ?? "";
                var id = Convert.ToInt64(reader["ID"]);
                var code = Get<string>(reader, "ApplicantCode");
                if (string.IsNullOrWhiteSpace(code)) code = Get<string>(reader, "APPLICANT CODE");

                var name = string.Join(" ", new[]
                {
                    Get<string>(reader, "FirstName"), Get<string>(reader, "MiddleName"), Get<string>(reader, "LastName")
                }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();

                if (string.IsNullOrWhiteSpace(link)) { withoutResume++; continue; }

                scoped.Add((id, code ?? "", name, applied, link));
            }
        }

        // Recorded upload sizes, so the plan can be produced without touching the disk.
        var sizes = new Dictionary<long, long>();
        await using (var conn = new SqlConnection(connStr))
        {
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 600;
            cmd.CommandText = @"
;WITH latest AS (
    SELECT CId, TRY_CAST(FileSize AS bigint) AS Bytes,
           ROW_NUMBER() OVER (PARTITION BY CId ORDER BY CreatedOn DESC, Id DESC) AS rn
    FROM dbo.CanidateDocs WITH (NOLOCK)
    WHERE FileType = 'Resume' AND ISNULL(IsDeleted, 0) = 0
)
SELECT CId, ISNULL(Bytes, 0) AS Bytes FROM latest WHERE rn = 1;";
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                sizes[Convert.ToInt64(reader["CId"])] = Convert.ToInt64(reader["Bytes"]);
        }

        var items = scoped
            .OrderBy(s => s.Id)          // ascending: see the class comment on part stability
            .Select(s => new ResumeItem
            {
                CandidateId = s.Id,
                ApplicantCode = s.Code,
                Name = s.Name,
                AppliedOn = s.Applied,
                RelativePath = s.ResumeLink,
                Bytes = sizes.TryGetValue(s.Id, out var b) ? b : 0
            })
            .ToList();

        return (items, inScope, withoutResume);
    }

    /// <summary>One file read ahead of the zip writer. Error/Missing replace throwing,
    /// so a bad file is reported in the manifest instead of killing the part.</summary>
    private sealed class PrefetchedFile
    {
        public ResumeItem Item { get; init; } = null!;
        public byte[] Data { get; init; } = Array.Empty<byte>();
        public string? Error { get; init; }
        public bool Missing { get; init; }
    }

    private static async Task<PrefetchedFile> ReadAheadAsync(ResumeItem item, string root, CancellationToken ct)
    {
        var diskPath = Path.Combine(root, item.RelativePath.Replace('/', Path.DirectorySeparatorChar)
                                                           .Replace('\\', Path.DirectorySeparatorChar));
        try
        {
            // Existence is not checked separately: over SMB that is a second round trip
            // per file, and the open below already tells us via FileNotFoundException.
            await using var src = new FileStream(diskPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                                                 81920, useAsync: true);
            using var ms = new MemoryStream(src.Length > 0 && src.Length < int.MaxValue ? (int)src.Length : 0);
            await src.CopyToAsync(ms, ct);
            return new PrefetchedFile { Item = item, Data = ms.ToArray() };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            var missing = ex is FileNotFoundException or DirectoryNotFoundException;
            return new PrefetchedFile
            {
                Item = item,
                Error = missing ? "file not found on disk" : $"{ex.GetType().Name}: {ex.Message}",
                Missing = missing
            };
        }
    }

    private sealed class Part
    {
        public int Index { get; init; }
        public List<ResumeItem> Items { get; } = new();
    }

    private static List<Part> Partition(List<ResumeItem> items, long partSizeBytes)
    {
        var parts = new List<Part>();
        if (items.Count == 0) return parts;

        var current = new Part { Index = 1 };
        long running = 0;
        foreach (var item in items)
        {
            // Start a new part once this file would push it past the limit — but never
            // emit an empty part, so a single file larger than the limit becomes its own.
            if (current.Items.Count > 0 && running + item.Bytes > partSizeBytes)
            {
                parts.Add(current);
                current = new Part { Index = parts.Count + 1 };
                running = 0;
            }
            current.Items.Add(item);
            running += item.Bytes;
        }
        if (current.Items.Count > 0) parts.Add(current);
        return parts;
    }

    /// <summary>
    /// Physical root for candidate documents. CandidateService saves under a bare
    /// "wwwroot" relative path, so the same folder is reached via WebRootPath here —
    /// which also means an instance started with --webroot pointing elsewhere (e.g. at
    /// the deployed server's share) reads that location instead.
    /// </summary>
    private string ResolveDocumentRoot()
        => _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

    private static string UniqueEntryName(ResumeItem item, HashSet<string> used)
    {
        var ext = Path.GetExtension(item.RelativePath);
        if (string.IsNullOrWhiteSpace(ext) || ext.Length > 12) ext = "";   // guards the odd malformed path

        // sp_GetApplicantListNew01 returns the literal "NA" (not NULL) when an applicant
        // has no code, which would name every single file "NA_<name>". Fall back to the
        // candidate id so each entry is actually identifiable and unique.
        var who = HasRealCode(item.ApplicantCode) ? item.ApplicantCode : item.CandidateId.ToString();
        var stem = Sanitize($"{who}_{item.Name}".Trim('_'));
        if (string.IsNullOrWhiteSpace(stem)) stem = item.CandidateId.ToString();

        var candidate = stem + ext;
        var n = 2;
        // Two applicants can share a code or a name; keep both files rather than
        // letting the second silently overwrite the first.
        while (!used.Add(candidate))
            candidate = $"{stem}_{n++}{ext}";
        return candidate;
    }

    /// <summary>"NA" / "N/A" / blank all mean "no applicant code" in the list proc's output.</summary>
    private static bool HasRealCode(string code)
        => !string.IsNullOrWhiteSpace(code)
           && !string.Equals(code.Trim(), "NA", StringComparison.OrdinalIgnoreCase)
           && !string.Equals(code.Trim(), "N/A", StringComparison.OrdinalIgnoreCase);

    private static string Sanitize(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            sb.Append(invalid.Contains(ch) || ch == ',' ? '_' : ch);
        return sb.ToString().Trim();
    }

    private static string Describe(ResumeItem i)
        => $"{(HasRealCode(i.ApplicantCode) ? i.ApplicantCode : i.CandidateId.ToString())} {i.Name}".Trim();

    private static string ManifestRow(ResumeItem i, string entryName, string status)
    {
        static string Q(string v) => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";
        return string.Join(",",
            Q(i.ApplicantCode), i.CandidateId, Q(i.Name),
            Q(i.AppliedOn?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? ""),
            Q(entryName), Q(status));
    }

    private static string BuildFileName(int part, int total, DateTime? from, DateTime? to, bool allDates)
    {
        var scope = allDates || from == null || to == null
            ? "all"
            : $"{from:yyyyMMdd}_to_{to:yyyyMMdd}";
        var of = total > 0 ? $"of{total:00}" : "";
        return $"Applicant_Resumes_{scope}_part{part:00}{of}.zip";
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "0 MB";
        double mb = bytes / 1024d / 1024d;
        return mb >= 1024 ? $"{mb / 1024d:0.00} GB" : $"{mb:0} MB";
    }

    private static T? Get<T>(IDataRecord r, string column)
    {
        int idx;
        try { idx = r.GetOrdinal(column); }
        catch (IndexOutOfRangeException) { return default; }
        if (r.IsDBNull(idx)) return default;
        var value = r.GetValue(idx);
        var target = typeof(T);
        var underlying = Nullable.GetUnderlyingType(target) ?? target;
        return (T)Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
    }
}
