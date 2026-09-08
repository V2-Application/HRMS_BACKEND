using System.Globalization;
using System.Text.RegularExpressions;
using HRMSAPI.Data;
using HRMSAPI.Interfaces;
using HRMSAPI.Models;
using Microsoft.EntityFrameworkCore;
using SharpCompress.Archives;
using UglyToad.PdfPig;

namespace HRMSAPI.Implementation;

public class MedicalCardService : IMedicalCardService
{
    private readonly HRMSContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<MedicalCardService> _log;

    public MedicalCardService(HRMSContext context, IWebHostEnvironment env, ILogger<MedicalCardService> log)
    {
        _context = context;
        _env = env;
        _log = log;
    }

    public async Task<IReadOnlyList<MedicalCardDto>> GetByEmployeeIdAsync(long employeeId)
    {
        var rows = await _context.tblEmployee_MedicalCards
            .AsNoTracking()
            .Where(c => c.EmployeeId == employeeId)
            .OrderBy(c => c.CardOrder)
            .ToListAsync();
        return rows.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<MedicalCardDto>> GetByEcodeAsync(string ecode)
    {
        var rows = await _context.tblEmployee_MedicalCards
            .AsNoTracking()
            .Where(c => c.Ecode == ecode)
            .OrderBy(c => c.CardOrder)
            .ToListAsync();
        return rows.Select(MapToDto).ToList();
    }

    public async Task<bool> UpdateSumAssuredAsync(int cardId, decimal? sumAssured, string updatedBy)
    {
        var card = await _context.tblEmployee_MedicalCards.FirstOrDefaultAsync(c => c.Id == cardId);
        if (card == null) return false;
        card.SumAssured = sumAssured;
        card.UpdatedBy = updatedBy;
        card.UpdatedOn = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<(bool success, string message, string url)> UploadAndAttachAsync(string ecode, Microsoft.AspNetCore.Http.IFormFile file, string updatedBy)
    {
        if (string.IsNullOrWhiteSpace(ecode)) return (false, "Ecode is required.", null);
        if (file == null || file.Length == 0) return (false, "No file uploaded.", null);

        var emp = await _context.tblEmployees.FirstOrDefaultAsync(e => e.Ecode == ecode);
        if (emp == null) return (false, $"Employee not found for ecode: {ecode}", null);

        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var folder = Path.Combine(webRoot, "MedicalCard", ecode);
        Directory.CreateDirectory(folder);

        var safeName = Path.GetFileName(file.FileName);
        var fileName = $"{DateTime.Now:yyyyMMddHHmmss}_{safeName}";
        var diskPath = Path.Combine(folder, fileName);
        using (var fs = new FileStream(diskPath, FileMode.Create))
            await file.CopyToAsync(fs);

        var relativeUrl = $"MedicalCard/{ecode}/{fileName}";
        emp.MedicalCardUrl = relativeUrl;
        emp.LastUpdatedBy = updatedBy;
        emp.UpdatedBy = updatedBy;   // keep both audit columns in sync
        emp.UpdatedOn = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Best-effort: re-parse so the cards table is populated for this ecode.
        try { await ReparseForEcodeAsync(ecode, updatedBy); }
        catch (Exception ex) { _log.LogWarning(ex, "Reparse after upload failed for {Ecode}", ecode); }

        return (true, "Uploaded", relativeUrl);
    }

    public Task<MedicalCardReparseResult> ReparseForEcodeAsync(string ecode, string updatedBy)
        => ReparseInternalAsync(updatedBy, ecodeFilter: ecode, dryRun: false);

    public Task<MedicalCardReparseResult> ReparseAllAsync(string updatedBy, bool dryRun = false)
        => ReparseInternalAsync(updatedBy, ecodeFilter: null, dryRun: dryRun);

    // Bulk upload. Accepts loose PDFs, ZIPs containing PDFs, or a mix.
    // Each file's name (without extension) is treated as the employee Ecode
    // (e.g. V00362.pdf -> V00362). Files mapped to an unknown ecode are
    // reported as skipped instead of failing the whole batch. After all
    // saves succeed the parser is invoked once per touched ecode.
    public async Task<MedicalCardBulkUploadResult> BulkUploadAsync(IEnumerable<Microsoft.AspNetCore.Http.IFormFile> files, string updatedBy, bool skipReparse = false)
    {
        var result = new MedicalCardBulkUploadResult();
        if (files == null) { result.Errors.Add("No files provided."); return result; }

        var incoming = files.Where(f => f != null && f.Length > 0).ToList();
        if (incoming.Count == 0) { result.Errors.Add("No non-empty files in the upload."); return result; }

        // Flatten: PDFs pass through; archives are expanded to their .pdf entries.
        // Archive entries are streamed to per-entry temp files up-front (while the
        // archive is still open) because the IFormFile request stream is not
        // seekable — opening entries lazily later throws
        // InvalidOperationException("inner stream position changed").
        var entries = new List<(string fileName, Func<Stream> openStream)>();
        var tempFilesToDelete = new List<string>();
        try
        {
            foreach (var f in incoming)
            {
                var name = f.FileName ?? "(unnamed)";

                // Anything that is not a PDF gets spooled to a temp file and
                // sniffed. Extension alone is not trustworthy: the insurer's
                // archives arrive from a download portal with mangled names like
                // "Ms V2 Retails Limited.7z_1787849793866 (1).7z", and a plain
                // ".zip" check would have to be repeated for every new format.
                // Magic bytes settle it, and SharpCompress reads zip / 7z / rar /
                // tar / gz through one interface.
                if (name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    var captured = f;
                    entries.Add((name, () => captured.OpenReadStream()));
                    continue;
                }

                var archiveTempPath = Path.Combine(Path.GetTempPath(), $"mc-arch-{Guid.NewGuid():N}.bin");
                tempFilesToDelete.Add(archiveTempPath);
                try
                {
                    using (var fs = new FileStream(archiveTempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        await f.CopyToAsync(fs);

                    var kind = DetectFileKind(archiveTempPath);
                    if (kind == UploadKind.Pdf)
                    {
                        // A PDF that arrived without a .pdf extension.
                        var captured = archiveTempPath;
                        entries.Add((name, () => new FileStream(captured, FileMode.Open, FileAccess.Read, FileShare.Read)));
                        continue;
                    }
                    if (kind == UploadKind.Unknown)
                    {
                        result.Errors.Add($"{name}: not a PDF and not a readable archive (zip / 7z / rar / tar / gz).");
                        continue;
                    }

                    var pdfCount = 0;
                    using (var archive = ArchiveFactory.Open(new FileInfo(archiveTempPath)))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (entry.IsDirectory || entry.Size <= 0) continue;
                            // Key is the entry's own basename — archives from the
                            // insurer nest everything under a folder
                            // ("Ms V2 Retails Limited\V00362_family.pdf").
                            var entryName = Path.GetFileName((entry.Key ?? string.Empty).Replace('\\', '/'));
                            if (string.IsNullOrEmpty(entryName) ||
                                !entryName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) continue;

                            var entryTemp = Path.Combine(Path.GetTempPath(), $"mc-pdf-{Guid.NewGuid():N}.pdf");
                            tempFilesToDelete.Add(entryTemp);
                            using (var es = entry.OpenEntryStream())
                            using (var ts = new FileStream(entryTemp, FileMode.Create, FileAccess.Write, FileShare.None))
                                await es.CopyToAsync(ts);

                            var captured = entryTemp;
                            entries.Add((entryName, () => new FileStream(captured, FileMode.Open, FileAccess.Read, FileShare.Read)));
                            pdfCount++;
                        }
                    }

                    if (pdfCount == 0)
                        result.Errors.Add($"{name}: {kind} archive contains no .pdf entries.");
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"{name}: failed to read archive - {ex.Message}");
                    _log.LogError(ex, "Failed to read archive {File}", name);
                }
            }

            result.TotalFiles = entries.Count;
            if (entries.Count == 0)
            {
                if (result.Errors.Count == 0)
                    result.Errors.Add("No usable files (PDFs or ZIPs containing PDFs) in the upload.");
                return result;
            }

            // Pre-load valid ecodes once so we don't query the DB per file. Each
            // filename yields SEVERAL candidate ecodes (see EcodeCandidates), so
            // ask for all of them in one go and let the per-file loop pick.
            var requestedEcodes = entries
                .SelectMany(e => EcodeCandidates(e.fileName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Chunked: a single IN (...) of 1,800+ literals is where SQL Server's
            // 2,100-parameter limit starts to bite on a full-company import.
            var validSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var chunk in requestedEcodes.Chunk(1000))
            {
                var found = await _context.tblEmployees
                    .Where(e => chunk.Contains(e.Ecode))
                    .Select(e => e.Ecode)
                    .ToListAsync();
                foreach (var ec in found) validSet.Add(ec);
            }

            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var touchedEcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (fileName, openStream) in entries)
            {
                var item = new MedicalCardBulkUploadItem { FileName = fileName };
                try
                {
                    var candidates = EcodeCandidates(fileName);
                    if (candidates.Count == 0)
                    {
                        item.Error = "Filename contains no ecode-shaped token.";
                        result.SkippedCount++;
                        result.Items.Add(item);
                        continue;
                    }

                    var ecode = candidates.FirstOrDefault(c => validSet.Contains(c));
                    if (ecode == null)
                    {
                        // Name the candidates tried, otherwise "not found" gives
                        // whoever is importing nothing to act on.
                        item.Ecode = candidates[0];
                        item.Error = $"No employee matches any ecode in this filename (tried: {string.Join(", ", candidates)}).";
                        result.SkippedCount++;
                        result.Items.Add(item);
                        continue;
                    }

                    // Use the DB's own casing, not the filename's — MedicalCardUrl
                    // becomes a folder path and the cards table keys off Ecode.
                    ecode = validSet.TryGetValue(ecode, out var canonical) ? canonical : ecode;
                    item.Ecode = ecode;

                    var folder = Path.Combine(webRoot, "MedicalCard", ecode);
                    Directory.CreateDirectory(folder);
                    var safeName = Path.GetFileName(fileName);
                    var newName = $"{DateTime.Now:yyyyMMddHHmmssfff}_{safeName}";
                    var diskPath = Path.Combine(folder, newName);
                    using (var fs = new FileStream(diskPath, FileMode.Create))
                    using (var src = openStream())
                    {
                        await src.CopyToAsync(fs);
                    }

                    var relativeUrl = $"MedicalCard/{ecode}/{newName}";
                    // ExecuteUpdateAsync issues a single UPDATE without loading
                    // the row into the change tracker — critical at 3k+ files.
                    await _context.tblEmployees
                        .Where(e => e.Ecode == ecode)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(e => e.MedicalCardUrl, relativeUrl)
                            .SetProperty(e => e.LastUpdatedBy, updatedBy));

                    item.Saved = true;
                    item.Url = relativeUrl;
                    touchedEcodes.Add(ecode);
                    result.SavedCount++;
                }
                catch (Exception ex)
                {
                    item.Error = $"{ex.GetType().Name}: {ex.Message}";
                    result.Errors.Add($"{fileName}: {item.Error}");
                    _log.LogError(ex, "Bulk upload failed for file {File}", fileName);
                }
                result.Items.Add(item);
            }

            // Re-parse each touched ecode so the cards table is up-to-date.
            // At scale (2k+) the parser phase dominates wall-time, so callers
            // can pass skipReparse=true and trigger Re-parse all afterwards.
            if (!skipReparse)
            {
                foreach (var ec in touchedEcodes)
                {
                    try
                    {
                        var r = await ReparseForEcodeAsync(ec, updatedBy);
                        result.CardsParsed += r.CardsInserted;
                        if (r.Errors.Count > 0) result.Errors.AddRange(r.Errors.Select(e => $"{ec}: {e}"));
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"{ec}: reparse failed - {ex.Message}");
                        _log.LogWarning(ex, "Bulk reparse failed for {Ecode}", ec);
                    }
                }
            }

            return result;
        }
        finally
        {
            foreach (var p in tempFilesToDelete)
            {
                try { if (File.Exists(p)) File.Delete(p); } catch { /* best-effort */ }
            }
        }
    }

    private enum UploadKind { Unknown, Pdf, Zip, SevenZip, Rar, Tar, GZip }

    // Identify an upload by its leading bytes. The insurer's portal renames its
    // downloads ("... .7z_1787849793866 (1).7z"), so trusting the extension is how
    // a whole batch silently gets rejected.
    private static UploadKind DetectFileKind(string path)
    {
        Span<byte> head = stackalloc byte[8];
        int read;
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            read = fs.Read(head);

        if (read >= 4 && head[0] == 0x25 && head[1] == 0x50 && head[2] == 0x44 && head[3] == 0x46) return UploadKind.Pdf;   // %PDF
        if (read >= 4 && head[0] == 0x50 && head[1] == 0x4B) return UploadKind.Zip;                                          // PK
        if (read >= 6 && head[0] == 0x37 && head[1] == 0x7A && head[2] == 0xBC &&
            head[3] == 0xAF && head[4] == 0x27 && head[5] == 0x1C) return UploadKind.SevenZip;                               // 7z
        if (read >= 4 && head[0] == 0x52 && head[1] == 0x61 && head[2] == 0x72 && head[3] == 0x21) return UploadKind.Rar;     // Rar!
        if (read >= 2 && head[0] == 0x1F && head[1] == 0x8B) return UploadKind.GZip;

        // TAR has no leading magic — "ustar" sits at offset 257.
        try
        {
            using var fs2 = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (fs2.Length > 262)
            {
                fs2.Position = 257;
                var ustar = new byte[5];
                if (fs2.Read(ustar, 0, 5) == 5 &&
                    System.Text.Encoding.ASCII.GetString(ustar) == "ustar") return UploadKind.Tar;
            }
        }
        catch { /* fall through to Unknown */ }

        return UploadKind.Unknown;
    }

    // A medical-card PDF is matched to an employee by the ecode in its filename.
    // The insurer does not hold that format still: cards used to arrive as
    // "V00362.pdf" and the August 2026 batch arrives as "V00362_family.pdf", which
    // made every one of 1,813 files fail as "ecode not found". So instead of
    // assuming one shape, return every ecode-shaped token the name could yield,
    // most-specific first, and let the caller keep whichever one a real employee
    // has. Adding a suffix on the insurer's side no longer breaks the import.
    //
    // Ecode-shaped = letters/digits only, containing at least one digit
    // (V00362, V2S006, E0277). That drops descriptive words like "family" and
    // "self" without needing a list of them.
    internal static List<string> EcodeCandidates(string fileName)
    {
        var candidates = new List<string>();
        var baseName = Path.GetFileNameWithoutExtension(fileName ?? string.Empty)?.Trim();
        if (string.IsNullOrEmpty(baseName)) return candidates;

        void Add(string s)
        {
            s = s?.Trim();
            if (string.IsNullOrEmpty(s)) return;
            if (!Regex.IsMatch(s, @"^[A-Za-z0-9]+$")) return;
            if (!s.Any(char.IsDigit)) return;
            if (!candidates.Contains(s, StringComparer.OrdinalIgnoreCase)) candidates.Add(s);
        }

        // 1. The whole name — keeps plain "V00362.pdf" working exactly as before,
        //    and wins over any token if an ecode ever contains a separator.
        Add(baseName);

        // 2. Each separator-delimited token, left to right. "V00362_family" -> V00362.
        //    Left-to-right so "V00362_family" and a hypothetical "family_V00362"
        //    both resolve.
        foreach (var token in baseName.Split(new[] { '_', '-', ' ', '.', '(', ')', '+', '#' },
                                             StringSplitOptions.RemoveEmptyEntries))
            Add(token);

        // 3. Last resort: any embedded ecode-looking run, for names with no
        //    separator at all ("V00362family").
        foreach (Match m in Regex.Matches(baseName, @"[A-Za-z]+\d+[A-Za-z0-9]*"))
            Add(m.Value);

        return candidates;
    }

    private async Task<MedicalCardReparseResult> ReparseInternalAsync(string updatedBy, string ecodeFilter, bool dryRun)
    {
        var result = new MedicalCardReparseResult();

        var employees = await _context.tblEmployees
            .Where(e => e.MedicalCardUrl != null && (ecodeFilter == null || e.Ecode == ecodeFilter))
            .Select(e => new { e.EmployeeId, e.Ecode, e.MedicalCardUrl })
            .ToListAsync();

        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        foreach (var emp in employees)
        {
            result.EmployeesProcessed++;
            try
            {
                var pdfPath = Path.Combine(webRoot, emp.MedicalCardUrl.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(pdfPath))
                {
                    result.Errors.Add($"{emp.Ecode}: PDF not found at {pdfPath}");
                    continue;
                }

                var parsedCards = ParsePdf(pdfPath, emp.Ecode, emp.MedicalCardUrl);
                if (parsedCards.Count == 0)
                {
                    result.Errors.Add($"{emp.Ecode}: no cards parsed");
                    continue;
                }

                if (dryRun)
                {
                    result.CardsSkipped += parsedCards.Count;
                    continue;
                }

                // Preserve any user-entered SumAssured. Keyed by the card's own
                // member/UHID number first: CardOrder used to be the page number
                // and is now a per-member counter, so on the first re-parse of a
                // Volo card matching by order alone would move a hand-entered sum
                // onto a different family member. Order stays as the fallback for
                // legacy cards, which have no member id.
                var existing = await _context.tblEmployee_MedicalCards
                    .Where(c => c.EmployeeId == emp.EmployeeId)
                    .ToListAsync();
                var sumByUhid = existing
                    .Where(c => !string.IsNullOrWhiteSpace(c.UhidNo) && c.SumAssured != null)
                    .GroupBy(c => c.UhidNo.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().SumAssured, StringComparer.OrdinalIgnoreCase);
                var sumByOrder = existing.ToDictionary(c => c.CardOrder, c => c.SumAssured);

                _context.tblEmployee_MedicalCards.RemoveRange(existing);

                foreach (var p in parsedCards)
                {
                    p.EmployeeId = emp.EmployeeId;
                    p.CreatedBy = updatedBy;
                    p.CreatedOn = DateTime.UtcNow;
                    if (!string.IsNullOrWhiteSpace(p.UhidNo) && sumByUhid.TryGetValue(p.UhidNo.Trim(), out var sumForMember))
                        p.SumAssured = sumForMember;
                    else if (sumByOrder.TryGetValue(p.CardOrder, out var prevSum))
                        p.SumAssured = prevSum;
                    _context.tblEmployee_MedicalCards.Add(p);
                }
                await _context.SaveChangesAsync();
                result.CardsInserted += parsedCards.Count;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{emp.Ecode}: {ex.GetType().Name}: {ex.Message}");
                _log.LogError(ex, "Failed to parse medical card for {Ecode}", emp.Ecode);
            }
        }

        return result;
    }

    private List<tblEmployee_MedicalCard> ParsePdf(string pdfPath, string ecode, string sourceUrl)
    {
        var cards = new List<tblEmployee_MedicalCard>();
        using var doc = PdfDocument.Open(pdfPath);
        int order = 0;
        foreach (var page in doc.GetPages())
        {
            var raw = page.Text ?? string.Empty;

            // TWO card layouts are in circulation, so the layout is detected per
            // page rather than assumed:
            //
            //   LEGACY  United India Insurance / FHPL TPA. Labelled fields
            //           ("UHID No", "Plan Period", ...), exactly ONE card per page.
            //           Still the only format for every card uploaded before
            //           Aug 2026, so Re-parse all must keep understanding it.
            //
            //   VOLO    Aditya Birla Health Insurance / Volo Health TPA, from the
            //           Aug-2026 batch onwards. Different labels AND several
            //           members per page, so a page is no longer one card.
            //
            // CardOrder therefore counts CARDS across the whole PDF, not pages.
            var pageCards = raw.IndexOf("Member ID:", StringComparison.OrdinalIgnoreCase) >= 0
                ? ParseVoloPage(raw)
                : new List<tblEmployee_MedicalCard> { ParseLegacyPage(raw) };

            foreach (var card in pageCards)
            {
                order++;
                card.Ecode = ecode;
                card.CardOrder = order;
                card.SourcePdfUrl = sourceUrl;
                card.RawText = TrimTo(card.RawText ?? raw, 4000);

                // Trim oversize values defensively (DB caps).
                card.UhidNo       = TrimTo(card.UhidNo,       50);
                card.HolderName   = TrimTo(card.HolderName,   200);
                card.PolicyNo     = TrimTo(card.PolicyNo,     100);
                card.Organisation = TrimTo(card.Organisation, 200);
                card.Insurer      = TrimTo(card.Insurer,      200);
                card.Tpa          = TrimTo(card.Tpa,          200);

                cards.Add(card);
            }
        }
        return cards;
    }

    private static tblEmployee_MedicalCard ParseLegacyPage(string raw)
    {
        // Header section is everything before "TERMS AND CONDITIONS" (which always follows).
        var headerEnd = raw.IndexOf("TERMS AND CONDITIONS", StringComparison.OrdinalIgnoreCase);
        var header = headerEnd > 0 ? raw.Substring(0, headerEnd) : raw;

        var card = new tblEmployee_MedicalCard { RawText = raw };

        // Field markers appear in fixed order; each value runs until the next marker.
        // Markers: "UHID No", "Name", "Age", "EmployeeID", "Plan Period", "Policy No", "Organisation"
        card.UhidNo       = Between(header, "UHID No",      "Name");
        card.HolderName   = Between(header, "Name",         "Age");
        var ageGender     = Between(header, "Age",          "EmployeeID");
        var planPeriod    = Between(header, "Plan Period",  "Policy No");
        card.PolicyNo     = Between(header, "Policy No",    "Organisation");
        card.Organisation = Between(header, "Organisation", null);

        (card.Age, card.Gender) = ParseAgeGender(ageGender);
        (card.PlanValidFrom, card.PlanValidTo) = ParsePlanPeriod(planPeriod);

        card.Insurer = DeriveInsurer(card.UhidNo);
        card.Tpa     = DeriveTpa(raw);

        return card;
    }

    // One Volo e-card, as PdfPig flattens it (no separators between fields):
    //
    //   Pappu KumarMember ID: VOLO06480101Gender: Male D.O.B.: 15-01-1984
    //   Relation: EMPLOYEE  Validity: 02-08-2026 - 01-08-2027
    //   Employer: Ms V2 Retails LimitedEmployee ID: V00362
    //   Insurer: Aditya Birla Health Insurance Co. LimitedTPA: Volo Health ...
    //   Customer support :...DISCLAIMER :...benefits details
    //
    // A page carries between one and three of these back to back. The holder's
    // name has no label of its own — it is whatever sits in front of "Member ID:" —
    // so blocks are cut at that anchor and the name taken from the preceding gap.
    private static readonly Regex VoloBlock = new(
        @"Member\sID:\s*(?<mid>[A-Za-z0-9\-\/]+)\s*" +
        @"Gender:\s*(?<gender>[A-Za-z]+)\s*" +
        @"D\.O\.B\.:\s*(?<dob>\d{2}-\d{2}-\d{4})\s*" +
        @"Relation:\s*(?<relation>[A-Za-z \-]+?)\s+" +
        @"Validity:\s*(?<from>\d{2}-\d{2}-\d{4})\s*-\s*(?<to>\d{2}-\d{2}-\d{4})\s*" +
        @"Employer:\s*(?<employer>.*?)" +
        @"Employee\sID:\s*(?<empid>[A-Za-z0-9]+)\s*" +
        @"Insurer:\s*(?<insurer>.*?)" +
        @"TPA:\s*(?<tpa>.*?)" +
        @"(?=Customer\ssupport|DISCLAIMER|$)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static List<tblEmployee_MedicalCard> ParseVoloPage(string raw)
    {
        var cards = new List<tblEmployee_MedicalCard>();
        var matches = VoloBlock.Matches(raw);
        int previousBlockEnd = 0;

        foreach (Match m in matches)
        {
            // Text between the end of the last block and this "Member ID:" is the
            // holder's name, preceded by the previous card's disclaimer paragraph.
            var gap = raw.Substring(previousBlockEnd, m.Index - previousBlockEnd);
            var cut = gap.LastIndexOf("benefits details", StringComparison.OrdinalIgnoreCase);
            if (cut >= 0) gap = gap.Substring(cut + "benefits details".Length);
            var name = gap.Trim();

            var from = TryParseDate(m.Groups["from"].Value, "dd-MM-yyyy");
            var to   = TryParseDate(m.Groups["to"].Value,   "dd-MM-yyyy");
            var dob  = TryParseDate(m.Groups["dob"].Value,  "dd-MM-yyyy");

            cards.Add(new tblEmployee_MedicalCard
            {
                // Volo's "Member ID" is this format's per-person identifier, so it
                // goes where the UHID used to — that is what the portal shows.
                UhidNo        = m.Groups["mid"].Value.Trim(),
                HolderName    = string.IsNullOrWhiteSpace(name) ? null : name,
                Gender        = NormalizeGender(m.Groups["gender"].Value),
                // The Volo card prints a date of birth, not an age. Age is derived
                // against the plan start (not today) so a re-parse next year does
                // not silently change what the card says.
                Age           = AgeAt(dob, from),
                PlanValidFrom = from,
                PlanValidTo   = to,
                // Volo cards carry no policy number at all — left null rather than
                // filled with something that is not on the card.
                PolicyNo      = null,
                Organisation  = NullIfBlank(m.Groups["employer"].Value),
                Insurer       = NullIfBlank(m.Groups["insurer"].Value),
                Tpa           = NullIfBlank(m.Groups["tpa"].Value),
                RawText       = m.Value,
            });

            previousBlockEnd = m.Index + m.Length;
        }

        return cards;
    }

    private static string NullIfBlank(string s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string NormalizeGender(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        // Legacy cards stored "M"/"F"; keep that so the column stays consistent.
        return s.Trim().ToUpperInvariant() switch
        {
            "MALE" or "M" => "M",
            "FEMALE" or "F" => "F",
            _ => s.Trim().ToUpperInvariant()
        };
    }

    private static int? AgeAt(DateOnly? dob, DateOnly? asOf)
    {
        if (dob == null) return null;
        var on = asOf ?? DateOnly.FromDateTime(DateTime.Today);
        var age = on.Year - dob.Value.Year;
        if (on < dob.Value.AddYears(age)) age--;
        return age >= 0 && age < 130 ? age : null;
    }

    private static string Between(string src, string startMarker, string endMarker)
    {
        if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(startMarker)) return null;
        var s = src.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        if (s < 0) return null;
        s += startMarker.Length;
        int e = endMarker != null
            ? src.IndexOf(endMarker, s, StringComparison.OrdinalIgnoreCase)
            : src.Length;
        if (e < 0) e = src.Length;
        return src.Substring(s, e - s).Trim();
    }

    private static (int? age, string gender) ParseAgeGender(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return (null, null);
        // e.g. "41 Years(M)"
        var m = Regex.Match(s, @"(\d+)\s*Years?\s*\(?([MF])\)?", RegexOptions.IgnoreCase);
        if (!m.Success) return (null, null);
        return (int.Parse(m.Groups[1].Value), m.Groups[2].Value.ToUpperInvariant());
    }

    private static (DateOnly? from, DateOnly? to) ParsePlanPeriod(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return (null, null);
        // e.g. "02/08/2025 To 01/08/2026"
        var m = Regex.Match(s, @"(\d{2}/\d{2}/\d{4})\s*To\s*(\d{2}/\d{2}/\d{4})", RegexOptions.IgnoreCase);
        if (!m.Success) return (null, null);
        DateOnly? f = TryParseDmy(m.Groups[1].Value);
        DateOnly? t = TryParseDmy(m.Groups[2].Value);
        return (f, t);
    }

    private static DateOnly? TryParseDmy(string s) => TryParseDate(s, "dd/MM/yyyy");

    private static DateOnly? TryParseDate(string s, string format)
        => DateOnly.TryParseExact(s, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
           ? d : (DateOnly?)null;

    private static string DeriveInsurer(string uhid)
    {
        if (string.IsNullOrEmpty(uhid)) return null;
        // UHID prefix encodes the insurer. Currently observed: "UIIC" => United India Insurance Co.
        var prefix = new string(uhid.TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();
        return prefix switch
        {
            "UIIC" => "United India Insurance Co. Ltd.",
            _ => null
        };
    }

    private static string DeriveTpa(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        if (raw.IndexOf("fhpl", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Family Health Plan Insurance TPA Limited (FHPL)";
        return null;
    }

    private static string TrimTo(string s, int max)
        => string.IsNullOrEmpty(s) ? s : (s.Length > max ? s.Substring(0, max) : s);

    private static MedicalCardDto MapToDto(tblEmployee_MedicalCard c) => new()
    {
        id            = c.Id,
        employeeId    = c.EmployeeId,
        ecode         = c.Ecode,
        cardOrder     = c.CardOrder,
        uhidNo        = c.UhidNo,
        holderName    = c.HolderName,
        age           = c.Age,
        gender        = c.Gender,
        planValidFrom = c.PlanValidFrom,
        planValidTo   = c.PlanValidTo,
        policyNo      = c.PolicyNo,
        organisation  = c.Organisation,
        insurer       = c.Insurer,
        tpa           = c.Tpa,
        sumAssured    = c.SumAssured,
        sourcePdfUrl  = c.SourcePdfUrl,
    };
}
