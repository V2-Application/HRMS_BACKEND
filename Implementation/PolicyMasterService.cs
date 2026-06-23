using ClosedXML.Excel;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.Data.SqlClient;
using Roomsy.DTOS.GenericsResponses;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace HRMSAPI.Implementation
{
    // PTax (PTPolicyMaster) + LWF (LWFPolicyMaster) master upload/export.
    // Upload = UPSERT by natural key (LWF: State+Frequency; PTax: State+SlabMin+SlabMax+Gender).
    // Existing rows matching the key are updated; new keys are inserted. Nothing is deleted.
    public class PolicyMasterService : BaseService, IPolicyMasterService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<PolicyMasterService> _logger;

        public PolicyMasterService(HRMSContext context, IConfiguration configuration, ILogger<PolicyMasterService> logger) : base(context)
        {
            _configuration = configuration;
            _logger = logger;
        }

        private string ConnStr => _configuration.GetConnectionString("DefaultConnection");

        private static decimal? ParseDec(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return decimal.TryParse(s.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : (decimal?)null;
        }

        // ============================ PTax (PTPolicyMaster) ============================
        public async Task<FetchAndResponse> UploadPtaxExcelAsync(IFormFile file)
        {
            var expected = new[] { "State", "Slab Min", "Slab Max", "PT Rate", "Frequency", "Gender" };
            if (file == null || file.Length == 0)
                return BuildFetchErrorResponse("No file uploaded", HttpStatusCode.BadRequest);

            using var stream = file.OpenReadStream();
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheet(1);

            for (int i = 0; i < expected.Length; i++)
            {
                var h = ws.Cell(1, i + 1).GetValue<string>().Trim();
                if (!string.Equals(h, expected[i], StringComparison.OrdinalIgnoreCase))
                    return BuildFetchErrorResponse($"Header mismatch at column {i + 1}: expected '{expected[i]}', found '{h}'", HttpStatusCode.BadRequest);
            }

            var rows = ws.RowsUsed().Skip(1).ToList();
            if (rows.Count == 0)
                return BuildFetchErrorResponse("No data rows found", HttpStatusCode.BadRequest);

            int updated = 0;
            var skipped = new List<string>();
            await using var conn = new SqlConnection(ConnStr);
            await conn.OpenAsync();
            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
            try
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var r = rows[i];
                    var state = r.Cell(1).GetValue<string>()?.Trim();
                    if (string.IsNullOrWhiteSpace(state))
                        return Rollback(tx, $"Row {i + 2}: State is required.");

                    var slabMin = ParseDec(r.Cell(2).GetValue<string>());
                    var slabMax = ParseDec(r.Cell(3).GetValue<string>());
                    var ptRate = ParseDec(r.Cell(4).GetValue<string>());
                    var freq = r.Cell(5).GetValue<string>()?.Trim();
                    var gender = r.Cell(6).GetValue<string>()?.Trim();

                    // UPDATE-ONLY: never insert. Match existing row by State + Slab range + Gender.
                    await using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = @"
UPDATE dbo.PTPolicyMaster SET PtRate=@rate, Frequency=@freq
 WHERE State=@state
   AND ISNULL(SlabMin,-999999999)=ISNULL(@min,-999999999)
   AND ISNULL(SlabMax,-999999999)=ISNULL(@max,-999999999)
   AND ISNULL(Gender,'')=ISNULL(@gender,'');
SELECT @@ROWCOUNT;";
                        cmd.Parameters.Add(new SqlParameter("@state", SqlDbType.NVarChar, 200) { Value = state });
                        cmd.Parameters.Add(new SqlParameter("@min", SqlDbType.Decimal) { Value = (object)slabMin ?? DBNull.Value });
                        cmd.Parameters.Add(new SqlParameter("@max", SqlDbType.Decimal) { Value = (object)slabMax ?? DBNull.Value });
                        cmd.Parameters.Add(new SqlParameter("@rate", SqlDbType.Decimal) { Value = (object)ptRate ?? DBNull.Value });
                        cmd.Parameters.Add(new SqlParameter("@freq", SqlDbType.NVarChar, 100) { Value = (object)freq ?? DBNull.Value });
                        cmd.Parameters.Add(new SqlParameter("@gender", SqlDbType.VarChar, 50) { Value = (object)gender ?? DBNull.Value });
                        var affected = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        if (affected > 0) updated += affected;
                        else skipped.Add($"Row {i + 2}: no matching row for State='{state}', Slab {slabMin}-{slabMax}, Gender='{gender}'");
                    }
                }

                await tx.CommitAsync();
                var msg = skipped.Count > 0
                    ? $"PTax policy: {updated} row(s) updated, {skipped.Count} skipped (no match — uploads only update existing rows)."
                    : $"PTax policy: {updated} row(s) updated.";
                return BuildFetchSuccessResponse(msg, new { updated, skipped });
            }
            catch (Exception ex)
            {
                try { await tx.RollbackAsync(); } catch { }
                return BuildFetchErrorResponse($"Error uploading PTax policy: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }

        public async Task<FetchAndResponse> GetAllPtaxAsync(bool isExcel = false)
        {
            try
            {
                var list = new List<object>();
                var dt = new DataTable();
                await using (var conn = new SqlConnection(ConnStr))
                {
                    await conn.OpenAsync();
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT Id, State, SlabMin, SlabMax, PtRate, Frequency, Gender FROM dbo.PTPolicyMaster ORDER BY State, SlabMin, Gender";
                    if (!isExcel)
                    {
                        await using var rdr = await cmd.ExecuteReaderAsync();
                        while (await rdr.ReadAsync())
                        {
                            list.Add(new
                            {
                                id = rdr["Id"],
                                state = rdr["State"] as string,
                                slabMin = rdr["SlabMin"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(rdr["SlabMin"]),
                                slabMax = rdr["SlabMax"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(rdr["SlabMax"]),
                                ptRate = rdr["PtRate"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(rdr["PtRate"]),
                                frequency = rdr["Frequency"] as string,
                                gender = rdr["Gender"] as string,
                            });
                        }
                        return BuildFetchSuccessResponse("Fetched PTax policy", list);
                    }
                    using var da = new SqlDataAdapter(cmd);
                    da.Fill(dt);
                }

                using var book = new XLWorkbook();
                var sheet = book.Worksheets.Add("PTPolicyMaster");
                var headers = new[] { "State", "Slab Min", "Slab Max", "PT Rate", "Frequency", "Gender" };
                for (int i = 0; i < headers.Length; i++) { var c = sheet.Cell(1, i + 1); c.Value = headers[i]; c.Style.Font.Bold = true; }
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    var row = dt.Rows[i];
                    sheet.Cell(i + 2, 1).Value = row["State"]?.ToString();
                    sheet.Cell(i + 2, 2).Value = row["SlabMin"] == DBNull.Value ? "" : row["SlabMin"].ToString();
                    sheet.Cell(i + 2, 3).Value = row["SlabMax"] == DBNull.Value ? "" : row["SlabMax"].ToString();
                    sheet.Cell(i + 2, 4).Value = row["PtRate"] == DBNull.Value ? "" : row["PtRate"].ToString();
                    sheet.Cell(i + 2, 5).Value = row["Frequency"]?.ToString();
                    sheet.Cell(i + 2, 6).Value = row["Gender"]?.ToString();
                }
                sheet.Columns().AdjustToContents();
                using var ms = new MemoryStream();
                book.SaveAs(ms);
                return new FetchAndResponse { Status = true, Message = "Excel generated", Code = HttpStatusCode.OK, Data = ms.ToArray() };
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ============================ LWF (LWFPolicyMaster) ============================
        public async Task<FetchAndResponse> UploadLwfExcelAsync(IFormFile file)
        {
            var expected = new[] { "State", "Frequency", "Employee", "Employee Max", "Employer", "Employer Max" };
            if (file == null || file.Length == 0)
                return BuildFetchErrorResponse("No file uploaded", HttpStatusCode.BadRequest);

            using var stream = file.OpenReadStream();
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheet(1);

            for (int i = 0; i < expected.Length; i++)
            {
                var h = ws.Cell(1, i + 1).GetValue<string>().Trim();
                if (!string.Equals(h, expected[i], StringComparison.OrdinalIgnoreCase))
                    return BuildFetchErrorResponse($"Header mismatch at column {i + 1}: expected '{expected[i]}', found '{h}'", HttpStatusCode.BadRequest);
            }

            var rows = ws.RowsUsed().Skip(1).ToList();
            if (rows.Count == 0)
                return BuildFetchErrorResponse("No data rows found", HttpStatusCode.BadRequest);

            int updated = 0;
            var skipped = new List<string>();
            await using var conn = new SqlConnection(ConnStr);
            await conn.OpenAsync();
            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
            try
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var r = rows[i];
                    var state = r.Cell(1).GetValue<string>()?.Trim();
                    if (string.IsNullOrWhiteSpace(state))
                        return Rollback(tx, $"Row {i + 2}: State is required.");

                    var freq = r.Cell(2).GetValue<string>()?.Trim();
                    if (string.IsNullOrWhiteSpace(freq))
                        return Rollback(tx, $"Row {i + 2}: Frequency is required.");
                    var emp = ParseDec(r.Cell(3).GetValue<string>());
                    var empMax = ParseDec(r.Cell(4).GetValue<string>());
                    var empr = ParseDec(r.Cell(5).GetValue<string>());
                    var emprMax = ParseDec(r.Cell(6).GetValue<string>());

                    // UPDATE-ONLY: never insert. Match existing row by State + Frequency.
                    await using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = @"
UPDATE dbo.LWFPolicyMaster SET Employee=@emp, EmployeeMax=@empMax, Employeer=@empr, EmployeerMax=@emprMax
 WHERE State=@state AND ISNULL(Frequency,'')=ISNULL(@freq,'');
SELECT @@ROWCOUNT;";
                        cmd.Parameters.Add(new SqlParameter("@state", SqlDbType.NVarChar, 200) { Value = state });
                        cmd.Parameters.Add(new SqlParameter("@freq", SqlDbType.NVarChar, 200) { Value = (object)freq ?? DBNull.Value });
                        cmd.Parameters.Add(new SqlParameter("@emp", SqlDbType.Decimal) { Value = (object)emp ?? DBNull.Value });
                        cmd.Parameters.Add(new SqlParameter("@empMax", SqlDbType.Decimal) { Value = (object)empMax ?? DBNull.Value });
                        cmd.Parameters.Add(new SqlParameter("@empr", SqlDbType.Decimal) { Value = (object)empr ?? DBNull.Value });
                        cmd.Parameters.Add(new SqlParameter("@emprMax", SqlDbType.Decimal) { Value = (object)emprMax ?? DBNull.Value });
                        var affected = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        if (affected > 0) updated += affected;
                        else skipped.Add($"Row {i + 2}: no matching row for State='{state}', Frequency='{freq}'");
                    }
                }

                await tx.CommitAsync();
                var msg = skipped.Count > 0
                    ? $"LWF policy: {updated} row(s) updated, {skipped.Count} skipped (no match — uploads only update existing rows)."
                    : $"LWF policy: {updated} row(s) updated.";
                return BuildFetchSuccessResponse(msg, new { updated, skipped });
            }
            catch (Exception ex)
            {
                try { await tx.RollbackAsync(); } catch { }
                return BuildFetchErrorResponse($"Error uploading LWF policy: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }

        public async Task<FetchAndResponse> GetAllLwfAsync(bool isExcel = false)
        {
            try
            {
                var list = new List<object>();
                var dt = new DataTable();
                await using (var conn = new SqlConnection(ConnStr))
                {
                    await conn.OpenAsync();
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT Id, State, Frequency, Employee, EmployeeMax, Employeer, EmployeerMax FROM dbo.LWFPolicyMaster ORDER BY State, Frequency";
                    if (!isExcel)
                    {
                        await using var rdr = await cmd.ExecuteReaderAsync();
                        while (await rdr.ReadAsync())
                        {
                            list.Add(new
                            {
                                id = rdr["Id"],
                                state = rdr["State"] as string,
                                frequency = rdr["Frequency"] as string,
                                employee = rdr["Employee"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(rdr["Employee"]),
                                employeeMax = rdr["EmployeeMax"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(rdr["EmployeeMax"]),
                                employer = rdr["Employeer"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(rdr["Employeer"]),
                                employerMax = rdr["EmployeerMax"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(rdr["EmployeerMax"]),
                            });
                        }
                        return BuildFetchSuccessResponse("Fetched LWF policy", list);
                    }
                    using var da = new SqlDataAdapter(cmd);
                    da.Fill(dt);
                }

                using var book = new XLWorkbook();
                var sheet = book.Worksheets.Add("LWFPolicyMaster");
                var headers = new[] { "State", "Frequency", "Employee", "Employee Max", "Employer", "Employer Max" };
                for (int i = 0; i < headers.Length; i++) { var c = sheet.Cell(1, i + 1); c.Value = headers[i]; c.Style.Font.Bold = true; }
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    var row = dt.Rows[i];
                    sheet.Cell(i + 2, 1).Value = row["State"]?.ToString();
                    sheet.Cell(i + 2, 2).Value = row["Frequency"]?.ToString();
                    sheet.Cell(i + 2, 3).Value = row["Employee"] == DBNull.Value ? "" : row["Employee"].ToString();
                    sheet.Cell(i + 2, 4).Value = row["EmployeeMax"] == DBNull.Value ? "" : row["EmployeeMax"].ToString();
                    sheet.Cell(i + 2, 5).Value = row["Employeer"] == DBNull.Value ? "" : row["Employeer"].ToString();
                    sheet.Cell(i + 2, 6).Value = row["EmployeerMax"] == DBNull.Value ? "" : row["EmployeerMax"].ToString();
                }
                sheet.Columns().AdjustToContents();
                using var ms = new MemoryStream();
                book.SaveAs(ms);
                return new FetchAndResponse { Status = true, Message = "Excel generated", Code = HttpStatusCode.OK, Data = ms.ToArray() };
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ============================ Single-row UPDATE (from UI) ============================
        public async Task<ExecuteAndReponse> UpdatePtaxAsync(PtaxUpdateDto dto)
        {
            if (dto == null || dto.Id <= 0)
                return BuildExecuteErrorResponse("Valid row Id is required.", HttpStatusCode.BadRequest);
            if (string.IsNullOrWhiteSpace(dto.State))
                return BuildExecuteErrorResponse("State is required.", HttpStatusCode.BadRequest);
            if (dto.SlabMin == null)
                return BuildExecuteErrorResponse("Slab Min is required.", HttpStatusCode.BadRequest);
            if (dto.SlabMax == null)
                return BuildExecuteErrorResponse("Slab Max is required.", HttpStatusCode.BadRequest);
            if (dto.PtRate == null)
                return BuildExecuteErrorResponse("PT Rate is required.", HttpStatusCode.BadRequest);
            if (string.IsNullOrWhiteSpace(dto.Frequency))
                return BuildExecuteErrorResponse("Frequency is required.", HttpStatusCode.BadRequest);
            try
            {
                await using var conn = new SqlConnection(ConnStr);
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"UPDATE dbo.PTPolicyMaster
                    SET State=@state, SlabMin=@min, SlabMax=@max, PtRate=@rate, Frequency=@freq, Gender=@gender
                    WHERE Id=@id";
                cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = dto.Id });
                cmd.Parameters.Add(new SqlParameter("@state", SqlDbType.NVarChar, 200) { Value = dto.State.Trim() });
                cmd.Parameters.Add(new SqlParameter("@min", SqlDbType.Decimal) { Value = (object)dto.SlabMin ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@max", SqlDbType.Decimal) { Value = (object)dto.SlabMax ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@rate", SqlDbType.Decimal) { Value = (object)dto.PtRate ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@freq", SqlDbType.NVarChar, 100) { Value = dto.Frequency.Trim() });
                cmd.Parameters.Add(new SqlParameter("@gender", SqlDbType.VarChar, 50) { Value = (object)(string.IsNullOrWhiteSpace(dto.Gender) ? null : dto.Gender.Trim()) ?? DBNull.Value });
                var n = await cmd.ExecuteNonQueryAsync();
                if (n == 0) return BuildExecuteErrorResponse($"No PTax row found with Id {dto.Id}.", HttpStatusCode.NotFound);
                return BuildExecuteSuccessResponse("PTax row updated successfully.");
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse($"Error updating PTax row: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }

        public async Task<ExecuteAndReponse> UpdateLwfAsync(LwfUpdateDto dto)
        {
            if (dto == null || dto.Id <= 0)
                return BuildExecuteErrorResponse("Valid row Id is required.", HttpStatusCode.BadRequest);
            if (string.IsNullOrWhiteSpace(dto.State))
                return BuildExecuteErrorResponse("State is required.", HttpStatusCode.BadRequest);
            if (string.IsNullOrWhiteSpace(dto.Frequency))
                return BuildExecuteErrorResponse("Frequency is required.", HttpStatusCode.BadRequest);
            try
            {
                await using var conn = new SqlConnection(ConnStr);
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"UPDATE dbo.LWFPolicyMaster
                    SET State=@state, Frequency=@freq, Employee=@emp, EmployeeMax=@empMax, Employeer=@empr, EmployeerMax=@emprMax
                    WHERE Id=@id";
                cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = dto.Id });
                cmd.Parameters.Add(new SqlParameter("@state", SqlDbType.NVarChar, 200) { Value = dto.State.Trim() });
                cmd.Parameters.Add(new SqlParameter("@freq", SqlDbType.NVarChar, 200) { Value = dto.Frequency.Trim() });
                cmd.Parameters.Add(new SqlParameter("@emp", SqlDbType.Decimal) { Value = (object)dto.Employee ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@empMax", SqlDbType.Decimal) { Value = (object)dto.EmployeeMax ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@empr", SqlDbType.Decimal) { Value = (object)dto.Employer ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@emprMax", SqlDbType.Decimal) { Value = (object)dto.EmployerMax ?? DBNull.Value });
                var n = await cmd.ExecuteNonQueryAsync();
                if (n == 0) return BuildExecuteErrorResponse($"No LWF row found with Id {dto.Id}.", HttpStatusCode.NotFound);
                return BuildExecuteSuccessResponse("LWF row updated successfully.");
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse($"Error updating LWF row: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }

        // ============================ Single-row CREATE (from UI "+ Add") ============================
        public async Task<ExecuteAndReponse> CreatePtaxAsync(PtaxUpdateDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.State))
                return BuildExecuteErrorResponse("State is required.", HttpStatusCode.BadRequest);
            if (dto.SlabMin == null)
                return BuildExecuteErrorResponse("Slab Min is required.", HttpStatusCode.BadRequest);
            if (dto.SlabMax == null)
                return BuildExecuteErrorResponse("Slab Max is required.", HttpStatusCode.BadRequest);
            if (dto.PtRate == null)
                return BuildExecuteErrorResponse("PT Rate is required.", HttpStatusCode.BadRequest);
            if (string.IsNullOrWhiteSpace(dto.Frequency))
                return BuildExecuteErrorResponse("Frequency is required.", HttpStatusCode.BadRequest);
            try
            {
                await using var conn = new SqlConnection(ConnStr);
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO dbo.PTPolicyMaster (State, SlabMin, SlabMax, PtRate, Frequency, Gender)
                                    VALUES (@state, @min, @max, @rate, @freq, @gender);";
                cmd.Parameters.Add(new SqlParameter("@state", SqlDbType.NVarChar, 200) { Value = dto.State.Trim() });
                cmd.Parameters.Add(new SqlParameter("@min", SqlDbType.Decimal) { Value = (object)dto.SlabMin ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@max", SqlDbType.Decimal) { Value = (object)dto.SlabMax ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@rate", SqlDbType.Decimal) { Value = (object)dto.PtRate ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@freq", SqlDbType.NVarChar, 100) { Value = dto.Frequency.Trim() });
                cmd.Parameters.Add(new SqlParameter("@gender", SqlDbType.VarChar, 50) { Value = (object)(string.IsNullOrWhiteSpace(dto.Gender) ? null : dto.Gender.Trim()) ?? DBNull.Value });
                await cmd.ExecuteNonQueryAsync();
                return BuildExecuteSuccessResponse("PTax slab added successfully.");
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse($"Error adding PTax slab: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }

        public async Task<ExecuteAndReponse> CreateLwfAsync(LwfUpdateDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.State))
                return BuildExecuteErrorResponse("State is required.", HttpStatusCode.BadRequest);
            if (string.IsNullOrWhiteSpace(dto.Frequency))
                return BuildExecuteErrorResponse("Frequency is required.", HttpStatusCode.BadRequest);
            try
            {
                await using var conn = new SqlConnection(ConnStr);
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO dbo.LWFPolicyMaster (State, Frequency, Employee, EmployeeMax, Employeer, EmployeerMax)
                                    VALUES (@state, @freq, @emp, @empMax, @empr, @emprMax);";
                cmd.Parameters.Add(new SqlParameter("@state", SqlDbType.NVarChar, 200) { Value = dto.State.Trim() });
                cmd.Parameters.Add(new SqlParameter("@freq", SqlDbType.NVarChar, 200) { Value = dto.Frequency.Trim() });
                cmd.Parameters.Add(new SqlParameter("@emp", SqlDbType.Decimal) { Value = (object)dto.Employee ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@empMax", SqlDbType.Decimal) { Value = (object)dto.EmployeeMax ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@empr", SqlDbType.Decimal) { Value = (object)dto.Employer ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@emprMax", SqlDbType.Decimal) { Value = (object)dto.EmployerMax ?? DBNull.Value });
                await cmd.ExecuteNonQueryAsync();
                return BuildExecuteSuccessResponse("LWF entry added successfully.");
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse($"Error adding LWF entry: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }

        private FetchAndResponse Rollback(SqlTransaction tx, string message)
        {
            try { tx.Rollback(); } catch { }
            return BuildFetchErrorResponse(message, HttpStatusCode.BadRequest);
        }
    }
}
