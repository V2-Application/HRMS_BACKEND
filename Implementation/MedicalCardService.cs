using System.Globalization;
using System.Text.RegularExpressions;
using HRMSAPI.Data;
using HRMSAPI.Interfaces;
using HRMSAPI.Models;
using Microsoft.EntityFrameworkCore;
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

                // Preserve any user-entered SumAssured: keyed by (EmployeeId, CardOrder).
                var existing = await _context.tblEmployee_MedicalCards
                    .Where(c => c.EmployeeId == emp.EmployeeId)
                    .ToListAsync();
                var sumByOrder = existing.ToDictionary(c => c.CardOrder, c => c.SumAssured);

                _context.tblEmployee_MedicalCards.RemoveRange(existing);

                foreach (var p in parsedCards)
                {
                    p.EmployeeId = emp.EmployeeId;
                    p.CreatedBy = updatedBy;
                    p.CreatedOn = DateTime.UtcNow;
                    if (sumByOrder.TryGetValue(p.CardOrder, out var prevSum))
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
            order++;
            var raw = page.Text ?? string.Empty;
            // Header section is everything before "TERMS AND CONDITIONS" (which always follows).
            var headerEnd = raw.IndexOf("TERMS AND CONDITIONS", StringComparison.OrdinalIgnoreCase);
            var header = headerEnd > 0 ? raw.Substring(0, headerEnd) : raw;

            var card = new tblEmployee_MedicalCard
            {
                Ecode = ecode,
                CardOrder = order,
                SourcePdfUrl = sourceUrl,
                RawText = raw.Length > 4000 ? raw.Substring(0, 4000) : raw,
            };

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

            // Trim oversize values defensively (DB caps).
            card.UhidNo       = TrimTo(card.UhidNo,       50);
            card.HolderName   = TrimTo(card.HolderName,   200);
            card.PolicyNo     = TrimTo(card.PolicyNo,     100);
            card.Organisation = TrimTo(card.Organisation, 200);
            card.Insurer      = TrimTo(card.Insurer,      200);
            card.Tpa          = TrimTo(card.Tpa,          200);

            cards.Add(card);
        }
        return cards;
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

    private static DateOnly? TryParseDmy(string s)
        => DateOnly.TryParseExact(s, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
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
