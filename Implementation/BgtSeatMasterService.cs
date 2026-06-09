using ClosedXML.Excel;
using HRMSAPI.Data;
using HRMSAPI.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Roomsy.DTOS.GenericsResponses;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using SaveOptions = System.Xml.Linq.SaveOptions;

namespace HRMSAPI.Implementation
{
    public class BgtSeatMasterService : BaseService, IBgtSeatMasterService
    {
        private readonly IConfiguration _configuration;
        private readonly HRMSContext _context;
        private readonly ILogger<BgtSeatMasterService> _logger;

        public BgtSeatMasterService(HRMSContext context, IConfiguration configuration, ILogger<BgtSeatMasterService> logger) : base(context)
        {
            _configuration = configuration;
            _context = context;
            _logger = logger;
        }

        public async Task<FetchAndResponse> UploadBgtSeatMasterExcelAsync(IFormFile file)
{
    var expectedHeaders = new[] { "LOC CODE", "DEPARTMENT", "DESIGNATION", "SALARY BGT", "ORG CHART", "REPORTING MANAGER DESG", "ACTIVE" };
    if (file == null || file.Length == 0)
        return BuildFetchErrorResponse("No file uploaded", HttpStatusCode.BadRequest);

    using var stream = file.OpenReadStream();
    using var workbook = new XLWorkbook(stream);
    var worksheet = workbook.Worksheet(1);

    // Validate headers (exact order and names)
    for (int i = 0; i < expectedHeaders.Length; i++)
    {
        var cellValue = worksheet.Cell(1, i + 1).GetValue<string>().Trim();
        if (!string.Equals(cellValue, expectedHeaders[i], StringComparison.OrdinalIgnoreCase))
            return BuildFetchErrorResponse($"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'", HttpStatusCode.BadRequest);
    }

    var rows = worksheet.RowsUsed().Skip(1).ToList();
    if (rows.Count == 0)
        return BuildFetchErrorResponse("No data rows found", HttpStatusCode.BadRequest);

    // Collect unique values for validation
    var locCodes = new HashSet<string>(rows.Select(r => r.Cell(1).GetValue<string>()?.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)), StringComparer.OrdinalIgnoreCase);
    var deptNames = new HashSet<string>(rows.Select(r => r.Cell(2).GetValue<string>()?.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)), StringComparer.OrdinalIgnoreCase);
    var desigNames = new HashSet<string>(rows.Select(r => r.Cell(3).GetValue<string>()?.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)), StringComparer.OrdinalIgnoreCase);
    var repMgrDesigNames = new HashSet<string>(rows.Select(r => r.Cell(6).GetValue<string>()?.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)), StringComparer.OrdinalIgnoreCase);

    // Validate Active/InActive values (only these two if provided)
    var activeValues = new HashSet<string>(rows.Select(r => r.Cell(7).GetValue<string>()?.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)), StringComparer.OrdinalIgnoreCase);
    foreach (var val in activeValues)
    {
        if (!string.Equals(val, "Active", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(val, "InActive", StringComparison.OrdinalIgnoreCase))
        {
            return BuildFetchErrorResponse("only Active /InActive is allowed", HttpStatusCode.BadRequest);
        }
    }

    // Load reference data (existence checks)
    var locations = await _context.tblLocations.AsNoTracking().AsQueryable()
        .Where(l => locCodes.Contains(l.STCode))
        .ToListAsync();

    var departments = await _context.tblDepartments.AsNoTracking().AsQueryable()
        .Where(d => deptNames.Contains(d.DepartmentName))
        .ToListAsync();

    var designations = await _context.tblDesignations.AsNoTracking().AsQueryable()
        .Where(d => desigNames.Contains(d.DesignationName) || repMgrDesigNames.Contains(d.DesignationName))
        .ToListAsync();

    var existingLocCodes = new HashSet<string>(locations.Select(l => l.STCode), StringComparer.OrdinalIgnoreCase);
    var missingLocCodes = locCodes.Where(c => !existingLocCodes.Contains(c)).ToList();
    if (missingLocCodes.Count > 0)
        return BuildFetchErrorResponse($"These LOC CODEs do not exist: {string.Join(", ", missingLocCodes)}", HttpStatusCode.BadRequest);

    var deptByName = departments
        .GroupBy(d => d.DepartmentName, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    var missingDept = deptNames.Where(n => !deptByName.ContainsKey(n)).ToList();
    if (missingDept.Count > 0)
        return BuildFetchErrorResponse($"These departments do not exist: {string.Join(", ", missingDept)}", HttpStatusCode.BadRequest);

    var desigByName = designations
        .GroupBy(d => d.DesignationName, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    var missingDesig = desigNames.Where(n => !desigByName.ContainsKey(n)).ToList();
    if (missingDesig.Count > 0)
        return BuildFetchErrorResponse($"These designations do not exist: {string.Join(", ", missingDesig)}", HttpStatusCode.BadRequest);
    var missingRepMgrDesig = repMgrDesigNames.Where(n => !string.IsNullOrWhiteSpace(n)).Where(n => !desigByName.ContainsKey(n)).ToList();
    if (missingRepMgrDesig.Count > 0)
        return BuildFetchErrorResponse($"These reporting manager designations do not exist: {string.Join(", ", missingRepMgrDesig)}", HttpStatusCode.BadRequest);

            // ===== Load active sub-department hierarchy for validation (raw SQL; no EF entity) =====
            // Key: "{DepartmentId}|{ParentId(0=root)}|{DepthLevel}|{lower(name)}" -> (Id, canonical Name)
            var subByKey = new Dictionary<string, (int Id, string Name)>(StringComparer.OrdinalIgnoreCase);
            {
                var subConnStr = _configuration.GetConnectionString("DefaultConnection");
                await using var sconn = new SqlConnection(subConnStr);
                await sconn.OpenAsync();
                await using var scmd = new SqlCommand(
                    @"SELECT SubDepartmentId, SubDepartmentName, DepartmentId, ISNULL(ParentSubDepartmentId,0) AS ParentId, DepthLevel
                      FROM dbo.tblSubDepartment WHERE ISNULL(isDeleted,0)=0 AND ISNULL(isActive,1)=1", sconn);
                await using var srdr = await scmd.ExecuteReaderAsync();
                while (await srdr.ReadAsync())
                {
                    var sid = srdr.GetInt32(0);
                    var snm = (srdr["SubDepartmentName"] as string ?? "").Trim();
                    var sdept = srdr.GetInt32(2);
                    var sparent = Convert.ToInt32(srdr["ParentId"]);
                    var slvl = Convert.ToInt32(srdr["DepthLevel"]);
                    subByKey[$"{sdept}|{sparent}|{slvl}|{snm.ToLowerInvariant()}"] = (sid, snm);
                }
            }
            var subErrors = new List<string>();

            // ===== Build XML for the stored procedure (IDs only; names come from DB) =====
            var xRows = new XElement("rows");

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];

                var locRaw = row.Cell(1).GetValue<string>()?.Trim();
                if (string.IsNullOrWhiteSpace(locRaw))
                    return BuildFetchErrorResponse(
                        $"Row {i + 2}: LOC CODE is required.",
                        HttpStatusCode.BadRequest);
                var loc = (locRaw ?? string.Empty).ToUpperInvariant(); // <-- force UPPER like HA10

                var deptName = row.Cell(2).GetValue<string>()?.Trim();
                var desigName = row.Cell(3).GetValue<string>()?.Trim();
                var salaryStr = row.Cell(4).GetValue<string>()?.Trim();
                var orgChart = row.Cell(5).GetValue<string>()?.Trim();
                var repMgrDesigName = row.Cell(6).GetValue<string>()?.Trim();
                var activeStr = row.Cell(7).GetValue<string>()?.Trim();
                if (string.IsNullOrWhiteSpace(deptName) || string.IsNullOrWhiteSpace(desigName))
                    return BuildFetchErrorResponse(
                        $"Row {i + 2}: Department and Designation are required.",
                        HttpStatusCode.BadRequest);
                var dept = deptByName[deptName];
                var desig = desigByName[desigName];

                // rep-manager optional
                int repMgrDesigId = 0;
                if (!string.IsNullOrWhiteSpace(repMgrDesigName) && desigByName.TryGetValue(repMgrDesigName, out var repDesig))
                    repMgrDesigId = repDesig.DesignationId;

                // salary → invariant if parseable; else empty (proc TRY_CONVERTs)
                string salaryOut = string.Empty;
                if (decimal.TryParse(salaryStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var sal))
                    salaryOut = sal.ToString(CultureInfo.InvariantCulture);

                // active → "Active"/"InActive" or empty
                string activeOut = string.Empty;
                if (!string.IsNullOrWhiteSpace(activeStr))
                    activeOut = activeStr.Equals("Active", StringComparison.OrdinalIgnoreCase) ? "Active"
                              : activeStr.Equals("InActive", StringComparison.OrdinalIgnoreCase) ? "InActive"
                              : string.Empty;

                // Sub-department chain (optional). Blank => skip. If any value is provided it MUST
                // match the hierarchy under this department/parent chain, else the row is rejected.
                var sub1 = row.Cell(8).GetValue<string>()?.Trim();
                var sub2 = row.Cell(9).GetValue<string>()?.Trim();
                var sub3 = row.Cell(10).GetValue<string>()?.Trim();
                string sub1Out = string.Empty, sub2Out = string.Empty, sub3Out = string.Empty;
                if (!string.IsNullOrWhiteSpace(sub1) || !string.IsNullOrWhiteSpace(sub2) || !string.IsNullOrWhiteSpace(sub3))
                {
                    if (string.IsNullOrWhiteSpace(sub1))
                        subErrors.Add($"Row {i + 2}: Sub Dept 1 is required when Sub Dept 2/3 are provided.");
                    else if (!string.IsNullOrWhiteSpace(sub3) && string.IsNullOrWhiteSpace(sub2))
                        subErrors.Add($"Row {i + 2}: Sub Dept 2 is required when Sub Dept 3 is provided.");
                    else if (!subByKey.TryGetValue($"{dept.DepartmentId}|0|1|{sub1.ToLowerInvariant()}", out var n1))
                        subErrors.Add($"Row {i + 2}: Sub Dept 1 '{sub1}' not found under department '{deptName}'.");
                    else
                    {
                        sub1Out = n1.Name;
                        if (!string.IsNullOrWhiteSpace(sub2))
                        {
                            if (!subByKey.TryGetValue($"{dept.DepartmentId}|{n1.Id}|2|{sub2.ToLowerInvariant()}", out var n2))
                                subErrors.Add($"Row {i + 2}: Sub Dept 2 '{sub2}' not found under '{sub1}' (department '{deptName}').");
                            else
                            {
                                sub2Out = n2.Name;
                                if (!string.IsNullOrWhiteSpace(sub3))
                                {
                                    if (!subByKey.TryGetValue($"{dept.DepartmentId}|{n2.Id}|3|{sub3.ToLowerInvariant()}", out var n3))
                                        subErrors.Add($"Row {i + 2}: Sub Dept 3 '{sub3}' not found under '{sub2}' (department '{deptName}').");
                                    else
                                        sub3Out = n3.Name;
                                }
                            }
                        }
                    }
                }

                xRows.Add(new XElement("row",
                    new XAttribute("idx", i + 1),                // <— parse on SQL side
                    new XAttribute("loc", loc ?? string.Empty),
                    new XAttribute("dept_id", dept.DepartmentId),
                    new XAttribute("desig_id", desig.DesignationId),
                    new XAttribute("rep_mgr_desig_id", repMgrDesigId),  // 0 => NULL in SQL
                    new XAttribute("salary", salaryOut),
                    new XAttribute("org", orgChart ?? string.Empty),
                    new XAttribute("active", activeOut),
                    new XAttribute("sub1", sub1Out),
                    new XAttribute("sub2", sub2Out),
                    new XAttribute("sub3", sub3Out)
                ));
            }

            // Hierarchy validation failures: reject the whole upload with row-level details.
            if (subErrors.Count > 0)
                return BuildFetchErrorResponse(
                    "Sub-department hierarchy not matched:\n" + string.Join("\n", subErrors),
                    HttpStatusCode.BadRequest);

            // Call proc and optionally read (SeatMasterNo, idx)
            using (var conn = _context.Database.GetDbConnection()) {
                try
                {
                    await conn.OpenAsync();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "dbo.sp_BGTSeatMaster_InsertOnly_FromXml";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add(new SqlParameter("@RowsXml", SqlDbType.Xml)
                    {
                        Value = xRows.ToString(SaveOptions.DisableFormatting)
                    });

                    // (Optional) collect generated numbers
                    var created = new List<(int idx, string seatNo)>();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string seatNo = reader.GetString(0);   // inserted.SEAT_MASTER_NO from OUTPUT
                            int idx = reader.GetInt32(1);          // ir.idx from OUTPUT
                            created.Add((idx, seatNo));
                        }
                    }

                    // You can return `created` in your FetchAndResponse if you want
                    return BuildFetchSuccessResponse("BGTSEATMaster uploaded successfully", created);
                }
                catch (Exception ex)
                {
                    return BuildFetchErrorResponse($"Error uploading BGTSEATMaster: {ex.Message}", HttpStatusCode.BadRequest);
                }
                finally {
                    if (conn.State == ConnectionState.Open)
                        await conn.CloseAsync(); // <-- close, don't dispose
                }
            }
        }

        public async Task<FetchAndResponse> GetAllBgtSeatMasterAsync(bool isExcel = false)
        {
            try
            {
                var data = await _context.GetProcedures().proc_BGTSEATMASTERAsync();
                if (data == null || data.Count<1) {
                    return BuildFetchErrorResponse("No Data Found",HttpStatusCode.NotFound);
                }
                if (!isExcel)
                {
                    return BuildFetchSuccessResponse("Fetched all BGTSEATMaster records successfully", data);
                }

                using var workbook = new ClosedXML.Excel.XLWorkbook();
                var worksheet = workbook.Worksheets.Add("BGTSeatMaster");

                // Headers
                worksheet.Cell(1, 1).Value = "STCode";
                worksheet.Cell(1, 2).Value = "DepartmentId";
                worksheet.Cell(1, 3).Value = "DepartmentName";
                worksheet.Cell(1, 4).Value = "SubDepartment1";
                worksheet.Cell(1, 5).Value = "SubDepartment2";
                worksheet.Cell(1, 6).Value = "SubDepartment3";
                worksheet.Cell(1, 7).Value = "DesignationId";
                worksheet.Cell(1, 8).Value = "DesignationName";
                worksheet.Cell(1, 9).Value = "SeatOrStatus";
                worksheet.Cell(1, 10).Value = "SALARY_BGT";
                worksheet.Cell(1, 11).Value = "ActualSalary";
                worksheet.Cell(1, 12).Value = "Ecode";
                worksheet.Cell(1, 13).Value = "FullName";
                worksheet.Cell(1, 14).Value = "ReportEcode";
                worksheet.Cell(1, 15).Value = "ReportFullName";
                worksheet.Cell(1, 16).Value = "BGTReportingDesig";
                worksheet.Cell(1, 17).Value = "ActualReportingDesig";
                worksheet.Cell(1, 18).Value = "ACTIVE";

                for (int i = 0; i < data.Count; i++)
                {
                    var r = data[i];
                    worksheet.Cell(i + 2, 1).Value = r.STCode;
                    worksheet.Cell(i + 2, 2).Value = r.DepartmentId;
                    worksheet.Cell(i + 2, 3).Value = r.DepartmentName;
                    worksheet.Cell(i + 2, 4).Value = r.SubDepartment1;
                    worksheet.Cell(i + 2, 5).Value = r.SubDepartment2;
                    worksheet.Cell(i + 2, 6).Value = r.SubDepartment3;
                    worksheet.Cell(i + 2, 7).Value = r.DesignationId;
                    worksheet.Cell(i + 2, 8).Value = r.DesignationName;
                    worksheet.Cell(i + 2, 9).Value = r.SeatOrStatus;
                    worksheet.Cell(i + 2, 10).Value = r.SALARY_BGT;
                    worksheet.Cell(i + 2, 11).Value = r.ActualSalary;
                    worksheet.Cell(i + 2, 12).Value = r.Ecode;
                    worksheet.Cell(i + 2, 13).Value = r.FullName;
                    worksheet.Cell(i + 2, 14).Value = r.ReportEcode;
                    worksheet.Cell(i + 2, 15).Value = r.ReportFullName;
                    worksheet.Cell(i + 2, 16).Value = r.BGTReportingDesig;
                    worksheet.Cell(i + 2, 17).Value = r.ActualReportingDesig;
                    worksheet.Cell(i + 2, 18).Value = r.ACTIVE==true?"Active":"Inactive";
                }

                worksheet.Columns().AdjustToContents();

                using var stream = new System.IO.MemoryStream();
                workbook.SaveAs(stream);
                stream.Position = 0;

                return new FetchAndResponse
                {
                    Status = true,
                    Message = "Excel generated successfully",
                    Code = HttpStatusCode.OK,
                    Data = stream.ToArray()
                };
            }
            catch (Exception ex) {
                return BuildFetchErrorResponse(ex.Message,HttpStatusCode.BadRequest);
            }
        }
		public async Task<ExecuteAndReponse> DeleteSeatsBySeriesAsync(string locCode, int deptSno, int desgSno, int deleteCount)
		{
			if (string.IsNullOrWhiteSpace(locCode))
				return BuildExecuteErrorResponse("LOC_CODE is required", HttpStatusCode.BadRequest);
			if (deleteCount < 1)
				return BuildExecuteErrorResponse("DeleteCount must be a positive integer.", HttpStatusCode.BadRequest);

			using (var conn = _context.Database.GetDbConnection())
			{
				try
				{
					await conn.OpenAsync();
					using var cmd = conn.CreateCommand();
					cmd.CommandText = "dbo.usp_DeleteSeatsBySeries";
					cmd.CommandType = CommandType.StoredProcedure;

					var p1 = new SqlParameter("@LOC_CODE", SqlDbType.NVarChar, 50) { Value = locCode };
					var p2 = new SqlParameter("@DEPT_SNO", SqlDbType.Int) { Value = deptSno };
					var p3 = new SqlParameter("@DESG_SNO", SqlDbType.Int) { Value = desgSno };
					var p4 = new SqlParameter("@DeleteCount", SqlDbType.Int) { Value = deleteCount };

					cmd.Parameters.Add(p1);
					cmd.Parameters.Add(p2);
					cmd.Parameters.Add(p3);
					cmd.Parameters.Add(p4);

					await cmd.ExecuteNonQueryAsync();
					return BuildExecuteSuccessResponse($"Deleted {deleteCount} seat(s) for {locCode}/{deptSno}/{desgSno} successfully");
				}
				catch (SqlException ex)
				{
					// Preserve message from RAISERROR in proc when appropriate
					return BuildExecuteErrorResponse(ex.Message, HttpStatusCode.BadRequest);
				}
				catch (Exception ex)
				{
					return BuildExecuteErrorResponse($"Error deleting seats: {ex.Message}", HttpStatusCode.InternalServerError);
				}
				finally
				{
					if (conn.State == ConnectionState.Open)
						await conn.CloseAsync();
				}
			}
		}


    }
}
