using System.Data;
using System.Security.Claims;
using System.Threading;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;

public sealed class IncentiveService : IIncentiveService
{
    private const string ProcName = "dbo.usp_Incentive";
    private readonly HRMSContext _context;

    public IncentiveService(HRMSContext context) => _context = context;

    #region Public API

    public async Task<IncentiveDto?> UpsertAsync(
        IncentiveUpsertForm form,
        ClaimsIdentity? identity,
        string uploadsRoot,
        CancellationToken ct = default)
    {
        // Default CreatedBy on create
        if (form.IncentiveId == null && string.IsNullOrWhiteSpace(form.CreatedBy))
            form.CreatedBy = identity?.FindFirst("ecode")?.Value ?? identity?.Name ?? "system";

        // Normalize month to first day
        if (form.Month.HasValue)
            form.Month = new DateTime(form.Month.Value.Year, form.Month.Value.Month, 1);

        // Build TVP (metadata only)
        var tvp = BuildAttachmentsTvp();
        if (form.Attachments is { Count: > 0 })
        {
            var root = EnsureUploadsFolder(uploadsRoot);
            foreach (var file in form.Attachments)
            {
                if (file == null || file.Length == 0) continue;

                var ext = SafeGetExtension(file.FileName);
                var guidName = $"{Guid.NewGuid():N}{ext}";
                var savedPath = Path.Combine(root, guidName);

                Directory.CreateDirectory(root);
                await using (var stream = new FileStream(savedPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true))
                    await file.CopyToAsync(stream, ct).ConfigureAwait(false);

                var publicPath = $"/uploads/{guidName}";
                tvp.Rows.Add(file.FileName ?? string.Empty, file.ContentType ?? string.Empty, file.Length, publicPath);
            }
        }

        await using var sqlConn = await OpenSqlAsync(ct).ConfigureAwait(false);
        await using var cmd = CreateSpCommand(sqlConn, "UPSERT");

        // Scalars
        AddIfNotNull(cmd, "@IncentiveId", SqlDbType.BigInt, form.IncentiveId);
        AddIfNotNull(cmd, "@Ecode", SqlDbType.VarChar, form.Ecode, 50);
        AddIfNotNull(cmd, "@Month", SqlDbType.Date, form.Month);
        AddIfNotNull(cmd, "@Amount", SqlDbType.Decimal, form.Amount);
        AddIfNotNull(cmd, "@Remarks", SqlDbType.NVarChar, form.Remarks);
        AddIfNotNull(cmd, "@CreatedBy", SqlDbType.VarChar, form.CreatedBy, 50);

        // Stage fields
        AddIfNotNull(cmd, "@CmdStatusId", SqlDbType.Int, form.CmdStatusId);
        AddIfNotNull(cmd, "@HrStatusId", SqlDbType.Int, form.HrStatusId);
        AddIfNotNull(cmd, "@CmdRemarks", SqlDbType.NVarChar, form.CmdRemarks);
        AddIfNotNull(cmd, "@HrRemarks", SqlDbType.NVarChar, form.HrRemarks);

        // TVP + Replace flag
        cmd.Parameters.Add(new SqlParameter("@Attachments", SqlDbType.Structured)
        {
            TypeName = "dbo.tt_IncentiveAttachment",
            Value = tvp
        });
        cmd.Parameters.Add(new SqlParameter("@ReplaceAttachments", SqlDbType.Bit)
        {
            Value = form.ReplaceAttachments ?? false
        });

        IncentiveDto? dto = null;
        await using (var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection, ct).ConfigureAwait(false))
        {
            if (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var o = new Ordinals(reader);
                dto = MapIncentive(reader, o, includeStages: true);
                dto.Attachments = new List<IncentiveAttachmentDto>();
            }

            // Attachments (2nd result set)
            if (dto != null && await reader.NextResultAsync(ct).ConfigureAwait(false))
            {
                var list = new List<IncentiveAttachmentDto>();
                var o2 = new AttachmentOrdinals(reader);
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                    list.Add(MapAttachment(reader, o2));
                dto.Attachments = list;
            }
        }

        return dto;
    }

    public async Task<IncentiveDto?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        await using var sqlConn = await OpenSqlAsync(ct).ConfigureAwait(false);
        await using var cmd = CreateSpCommand(sqlConn, "GET");
        cmd.Parameters.Add(new SqlParameter("@IncentiveId", SqlDbType.BigInt) { Value = id });

        IncentiveDto? dto = null;
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection, ct).ConfigureAwait(false);

        if (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var o = new Ordinals(reader);
            dto = MapIncentive(reader, o, includeStages: true);
            dto.Attachments = new List<IncentiveAttachmentDto>();
        }

        if (dto != null && await reader.NextResultAsync(ct).ConfigureAwait(false))
        {
            var o2 = new AttachmentOrdinals(reader);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                dto.Attachments!.Add(MapAttachment(reader, o2));
        }

        return dto;
    }

    public async Task<(List<IncentiveDto> Items, long TotalCount, int CurrentPageNumber)>
        ListAsync(int pageNumber, int pageSize, string? searchTerm, CancellationToken ct = default)
    {
        await using var sqlConn = await OpenSqlAsync(ct).ConfigureAwait(false);
        await using var cmd = CreateSpCommand(sqlConn, "LIST");

        cmd.Parameters.Add(new SqlParameter("@PageNumber", SqlDbType.Int) { Value = pageNumber });
        cmd.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = pageSize });

        var st = string.IsNullOrWhiteSpace(searchTerm) ? (object)DBNull.Value : searchTerm!;
        cmd.Parameters.Add(new SqlParameter("@SearchTerm", SqlDbType.NVarChar, 100) { Value = st });

        var totalCountParam = new SqlParameter("@TotalCount", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
        var currentPageParam = new SqlParameter("@CurrentPageNumber", SqlDbType.Int) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(totalCountParam);
        cmd.Parameters.Add(currentPageParam);

        var items = new List<IncentiveDto>();
        await using (var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection, ct).ConfigureAwait(false))
        {
            var first = true;
            Ordinals? o = null;

            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                if (first) { o = new Ordinals(reader); first = false; }
                items.Add(MapIncentive(reader, o!, includeStages: true));
            }
        }

        long totalCount = (totalCountParam.Value == DBNull.Value) ? 0 : Convert.ToInt64(totalCountParam.Value);
        int currentPage = (currentPageParam.Value == DBNull.Value) ? pageNumber : Convert.ToInt32(currentPageParam.Value);

        return (items, totalCount, currentPage);
    }

    // >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
    // NEW: Bulk create (NO attachments) - implements IIncentiveService
    // >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
    public async Task<(List<IncentiveDto> Inserted, List<BulkSkipRow> Skipped)> BulkCreateAsync(
        IEnumerable<IncentiveUpsertForm> forms,
        ClaimsIdentity? identity,
        CancellationToken ct = default)
    {
        // Build TVP matching dbo.tt_IncentiveBulk
        var items = new DataTable();
        items.Columns.Add("RowNo", typeof(int));
        items.Columns.Add("Ecode", typeof(string));
        items.Columns.Add("Month", typeof(DateTime));
        items.Columns.Add("Amount", typeof(decimal));
        items.Columns.Add("Remarks", typeof(string));
        items.Columns.Add("CreatedBy", typeof(string));
        items.Columns.Add("CmdStatusId", typeof(int));
        items.Columns.Add("HrStatusId", typeof(int));
        items.Columns.Add("CmdRemarks", typeof(string));
        items.Columns.Add("HrRemarks", typeof(string));

        int rowNo = 0;
        foreach (var f in forms)
        {
            rowNo++;

            var createdBy = !string.IsNullOrWhiteSpace(f.CreatedBy)
                ? f.CreatedBy
                : (identity?.FindFirst("ecode")?.Value ?? identity?.Name ?? "system");

            if (!f.Month.HasValue) throw new ArgumentException("Month is required for bulk create.");
            if (!f.Amount.HasValue) throw new ArgumentException("Amount is required for bulk create.");
            if (string.IsNullOrWhiteSpace(f.Ecode)) throw new ArgumentException("Ecode is required for bulk create.");

            var month = new DateTime(f.Month.Value.Year, f.Month.Value.Month, 1);

            items.Rows.Add(
                rowNo,
                f.Ecode!,
                month,
                f.Amount!.Value,
                (object?)f.Remarks ?? DBNull.Value,
                createdBy,
                (object?)f.CmdStatusId ?? DBNull.Value,
                (object?)f.HrStatusId ?? DBNull.Value,
                (object?)f.CmdRemarks ?? DBNull.Value,
                (object?)f.HrRemarks ?? DBNull.Value
            );
        }

        await using var sqlConn = await OpenSqlAsync(ct).ConfigureAwait(false);
        await using var cmd = sqlConn.CreateCommand();
        cmd.CommandText = "dbo.usp_Incentive_BulkCreate";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.Add(new SqlParameter("@Items", SqlDbType.Structured)
        {
            TypeName = "dbo.tt_IncentiveBulk",
            Value = items
        });

        var inserted = new List<IncentiveDto>();
        var skipped = new List<BulkSkipRow>();

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection, ct).ConfigureAwait(false);

        // Set #1: inserted rows
        var first = true;
        Ordinals? o = null;
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            if (first) { o = new Ordinals(reader); first = false; }
            inserted.Add(MapIncentive(reader, o!, includeStages: true));
        }

        // Set #2: skipped rows
        if (await reader.NextResultAsync(ct).ConfigureAwait(false))
        {
            int ordRowNo = reader.GetOrdinal("RowNo");
            int ordEcode = reader.GetOrdinal("Ecode");
            int ordMonth = reader.GetOrdinal("Month");
            int ordReason = reader.GetOrdinal("Reason");

            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                skipped.Add(new BulkSkipRow
                {
                    RowNo = reader.IsDBNull(ordRowNo) ? 0 : reader.GetInt32(ordRowNo),
                    Ecode = reader.IsDBNull(ordEcode) ? null : reader.GetString(ordEcode),
                    Month = reader.IsDBNull(ordMonth) ? (DateTime?)null : reader.GetDateTime(ordMonth),
                    Reason = reader.IsDBNull(ordReason) ? null : reader.GetString(ordReason)
                });
            }
        }

        return (inserted, skipped);
    }

    #endregion

    #region SQL helpers

    private async Task<SqlConnection> OpenSqlAsync(CancellationToken ct)
    {
        var dbConn = _context.Database.GetDbConnection();
        if (dbConn is not SqlConnection sqlConn)
            throw new InvalidOperationException("SQL Server connection required.");

        if (sqlConn.State != ConnectionState.Open)
            await sqlConn.OpenAsync(ct).ConfigureAwait(false);

        return sqlConn;
    }

    private static SqlCommand CreateSpCommand(SqlConnection conn, string action)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = ProcName;
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.Add(new SqlParameter("@Action", SqlDbType.NVarChar, 20) { Value = action });
        return cmd;
    }

    private static void AddIfNotNull(SqlCommand cmd, string name, SqlDbType type, object? val, int? size = null)
    {
        if (val == null) return;
        if (val is string s && string.IsNullOrWhiteSpace(s)) return;

        var p = new SqlParameter(name, type);
        if (type == SqlDbType.Decimal)
        {
            p.Precision = 12; // DECIMAL(12,2)
            p.Scale = 2;
        }
        if (size.HasValue) p.Size = size.Value;
        p.Value = val;
        cmd.Parameters.Add(p);
    }

    private static DataTable BuildAttachmentsTvp()
    {
        var tvp = new DataTable();
        tvp.Columns.Add("FileName", typeof(string));
        tvp.Columns.Add("FileType", typeof(string));
        tvp.Columns.Add("FileSizeBytes", typeof(long));
        tvp.Columns.Add("FilePath", typeof(string));
        return tvp;
    }

    private static string SafeGetExtension(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return string.Empty;
        var ext = Path.GetExtension(fileName);
        return string.IsNullOrWhiteSpace(ext) ? string.Empty : ext;
    }

    private static string EnsureUploadsFolder(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("uploadsRoot is required.", nameof(root));

        return Path.GetFullPath(root);
    }

    #endregion

    #region Mapping

    private sealed class Ordinals
    {
        public readonly int IncentiveId;
        public readonly int Ecode;
        public readonly int Month;
        public readonly int Amount;
        public readonly int Remarks;
        public readonly int CreatedBy;
        public readonly int StatusId;
        public readonly int OverallStatusName;
        public readonly int CmdStatusId;
        public readonly int CmdStatusName;
        public readonly int HrStatusId;
        public readonly int HrStatusName;
        public readonly int CmdRemarks;
        public readonly int HrRemarks;
        public readonly int CreatedAt;
        public readonly int UpdatedAt;

        public Ordinals(IDataRecord r)
        {
            IncentiveId = r.TryOrdinal("IncentiveId");
            Ecode = r.TryOrdinal("Ecode");
            Month = r.TryOrdinal("Month");
            Amount = r.TryOrdinal("Amount");
            Remarks = r.TryOrdinal("Remarks");
            CreatedBy = r.TryOrdinal("CreatedBy");
            StatusId = r.TryOrdinal("StatusId");
            OverallStatusName = r.TryOrdinal("OverallStatusName");
            CmdStatusId = r.TryOrdinal("CmdStatusId");
            CmdStatusName = r.TryOrdinal("CmdStatusName");
            HrStatusId = r.TryOrdinal("HrStatusId");
            HrStatusName = r.TryOrdinal("HrStatusName");
            CmdRemarks = r.TryOrdinal("CmdRemarks");
            HrRemarks = r.TryOrdinal("HrRemarks");
            CreatedAt = r.TryOrdinal("CreatedAt");
            UpdatedAt = r.TryOrdinal("UpdatedAt");
        }
    }

    private static IncentiveDto MapIncentive(IDataRecord r, Ordinals o, bool includeStages)
    {
        var dto = new IncentiveDto
        {
            IncentiveId = r.GetNullableInt64(o.IncentiveId),
            Ecode = r.GetNullableString(o.Ecode),
            Month = r.GetNullableDateTime(o.Month),
            Amount = r.GetNullableDecimal(o.Amount),
            Remarks = r.GetNullableString(o.Remarks),
            CreatedBy = r.GetNullableString(o.CreatedBy),

            StatusId = r.GetNullableInt32(o.StatusId),
            StatusName = r.GetNullableString(o.OverallStatusName),

            CreatedAt = r.GetNullableDateTime(o.CreatedAt),
            UpdatedAt = r.GetNullableDateTime(o.UpdatedAt)
        };

        if (includeStages)
        {
            dto.CmdStatusId = r.GetNullableInt32(o.CmdStatusId);
            dto.CmdStatusName = r.GetNullableString(o.CmdStatusName);
            dto.HrStatusId = r.GetNullableInt32(o.HrStatusId);
            dto.HrStatusName = r.GetNullableString(o.HrStatusName);
            dto.CmdRemarks = r.GetNullableString(o.CmdRemarks);
            dto.HrRemarks = r.GetNullableString(o.HrRemarks);
        }

        return dto;
    }

    private sealed class AttachmentOrdinals
    {
        public readonly int AttachmentId;
        public readonly int IncentiveId;
        public readonly int FileName;
        public readonly int FileType;
        public readonly int FileSizeBytes;
        public readonly int FilePath;
        public readonly int UploadedAt;

        public AttachmentOrdinals(IDataRecord r)
        {
            AttachmentId = r.TryOrdinal("AttachmentId");
            IncentiveId = r.TryOrdinal("IncentiveId");
            FileName = r.TryOrdinal("FileName");
            FileType = r.TryOrdinal("FileType");
            FileSizeBytes = r.TryOrdinal("FileSizeBytes");
            FilePath = r.TryOrdinal("FilePath");
            UploadedAt = r.TryOrdinal("UploadedAt");
        }
    }

    private static IncentiveAttachmentDto MapAttachment(IDataRecord r, AttachmentOrdinals o) =>
        new()
        {
            AttachmentId = r.GetNullableInt64(o.AttachmentId),
            IncentiveId = r.GetNullableInt64(o.IncentiveId),
            FileName = r.GetNullableString(o.FileName),
            FileType = r.GetNullableString(o.FileType),
            FileSizeBytes = r.GetNullableInt64(o.FileSizeBytes),
            FilePath = r.GetNullableString(o.FilePath),
            UploadedAt = r.GetNullableDateTime(o.UploadedAt)
        };

    #endregion
}

/* KEEP THIS SEPARATE + STATIC — no instance members here */
internal static class DataRecordExtensions
{
    public static int TryOrdinal(this IDataRecord r, string name)
    {
        try { return r.GetOrdinal(name); }
        catch (IndexOutOfRangeException) { return -1; } // column not present
    }

    public static bool IsDbNull(this IDataRecord r, int ordinal)
        => ordinal < 0 || r.IsDBNull(ordinal);

    public static string? GetNullableString(this IDataRecord r, int ordinal)
        => r.IsDbNull(ordinal) ? null : r.GetString(ordinal);

    public static DateTime? GetNullableDateTime(this IDataRecord r, int ordinal)
        => r.IsDbNull(ordinal) ? (DateTime?)null : r.GetDateTime(ordinal);

    public static long? GetNullableInt64(this IDataRecord r, int ordinal)
        => r.IsDbNull(ordinal) ? (long?)null : r.GetInt64(ordinal);

    public static int? GetNullableInt32(this IDataRecord r, int ordinal)
        => r.IsDbNull(ordinal) ? (int?)null : r.GetInt32(ordinal);

    public static decimal? GetNullableDecimal(this IDataRecord r, int ordinal)
        => r.IsDbNull(ordinal) ? (decimal?)null : r.GetDecimal(ordinal);
}
