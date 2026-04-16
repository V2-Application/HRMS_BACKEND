using ClosedXML.Excel;
using HRMSAPI.Data;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Roomsy.DTOS.GenericsResponses;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace HRMSAPI.Implementation
{
    public class ApplicantUploadService : BaseService, IApplicantUploadService
    {
        private readonly ILogger<ApplicantUploadService> _logger;

        private static readonly (string Key, string[] Aliases, bool Required)[] TemplateColumns =
        {
            ("JobPosition", new[] { "Job Position", "JobPosition", "Job Location", "Location STCode", "Location" }, true),
            ("DepartmentName", new[] { "Department", "Dept", "Department Name" }, true),
            ("DesignationName", new[] { "Designation", "Designation Name", "DesignationNar" }, true),
            ("PreferredLocation", new[] { "Preferred Job Location", "Preferred Location", "PreferredLocation" }, true),
            ("StatusName", new[] { "Status", "Current Status", "Status Name" }, true),
            ("FullName", new[] { "Full Name", "Name" }, true),
            ("Phone", new[] { "Phone", "Mobile", "Contact No" }, true),
            ("DOB", new[] { "DOB", "Date of Birth" }, false),
            ("Email", new[] { "Email", "Email Address" }, false),
            ("TotalExperience", new[] { "Total Experience (Years)", "Total Experience" }, false),
            ("PreviousCompany", new[] { "Previous Company", "Last Company" }, false),
            ("PreviousDesignation", new[] { "Previous Designation", "Designation (Previous)" }, false),
            ("PreviousSalary", new[] { "Previous Salary", "Last Drawn Salary" }, false),
            ("SalaryExpectation", new[] { "Salary Expectation", "Expected Salary" }, false),
            ("Source", new[] { "Source" }, false),
            ("JoiningDate", new[] { "Date of Joining", "DOJ" }, false),
            ("Reference", new[] { "Reference" }, false),
            ("HigherQualification", new[] { "Higher Qualification", "Highest Qualification" }, false),
            ("PassingYear", new[] { "Passing Year", "Year Of Passing" }, false),
            ("Notes", new[] { "Cover Letter / Notes", "Notes" }, false),
            ("Interview1Date", new[] { "Interview-1 ACT", "Interview 1 ACT", "Interview1 ACT", "Interview1Date" }, false),
            ("Interview1Interviewers", new[] { "Interviewer-1", "Interviewer 1", "Interview1 Interviewer", "Interviewer1" }, false),
            ("Interview1Status", new[] { "Interviewer-1 Status", "Interviewer 1 Status", "Interview1 Status" }, false),
            ("Interview2Date", new[] { "Interview-2 ACT", "Interview 2 ACT", "Interview2 ACT", "Interview2Date" }, false),
            ("Interview2Interviewers", new[] { "Interviewer-2", "Interviewer 2", "Interview2 Interviewer", "Interviewer2" }, false),
            ("Interview2Status", new[] { "Interviewer-2 Status", "Interviewer 2 Status", "Interview2 Status" }, false),
            ("InterviewFinalRemark", new[] { "Interview Final Remark", "Final Interview Remark", "Final Remark" }, false)
        };

        private static readonly Dictionary<string, CandidateProcessStatus> AllowedStatusMap =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Pending"] = new CandidateProcessStatus { StatusId = 4, StatusName = "Pending" },
                ["OnHold"] = new CandidateProcessStatus { StatusId = 6, StatusName = "OnHold" },
                ["Not Interested"] = new CandidateProcessStatus { StatusId = 9, StatusName = "Not Interested" },
                ["Resume Shortlisted"] = new CandidateProcessStatus { StatusId = 12, StatusName = "Resume Shortlisted" },
                ["Rejected"] = new CandidateProcessStatus { StatusId = 2, StatusName = "Rejected" },
                ["Upcoming Interview"] = new CandidateProcessStatus { StatusId = 14, StatusName = "Upcoming Interview" },
                ["Schedule Pending"] = new CandidateProcessStatus { StatusId = 13, StatusName = "Schedule Pending" },
                ["Negotitation"] = new CandidateProcessStatus { StatusId = 11, StatusName = "Negotitation" },
                ["Complete"] = new CandidateProcessStatus { StatusId = 15, StatusName = "Complete" }
            };

        private static readonly string[] StatusesAllowedWithoutInterview =
        {
            "Pending",
            "Rejected",
            "Resume Shortlisted",
            "OnHold",
            "Not Interested"
        };

        private static readonly string[] AllowedInterviewerStatuses =
        {
            "Qualified",
            "Rejected",
            "Pending"
        };

        public ApplicantUploadService(HRMSContext context, ILogger<ApplicantUploadService> logger) : base(context)
        {
            _logger = logger;
        }

        public async Task<ExecuteAndReponse> UploadApplicantsAsync(IFormFile file, string uploadedBy)
        {
            if (file == null || file.Length == 0)
            {
                return BuildExecuteErrorResponse("No file uploaded.", HttpStatusCode.BadRequest);
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".xlsx" && extension != ".xls")
            {
                return BuildExecuteErrorResponse("Only Excel files (.xlsx, .xls) are allowed.", HttpStatusCode.BadRequest);
            }

            List<ApplicantUploadParsedRow> parsedRows;
            try
            {
                parsedRows = ParseExcel(file, out var missingHeaders, out var parseErrors);
                if (missingHeaders.Any())
                {
                    return BuildExecuteErrorResponse($"Missing header(s): {string.Join(", ", missingHeaders)}", HttpStatusCode.BadRequest);
                }

                if (parseErrors.Any())
                {
                    return BuildExecuteErrorResponse(BuildValidationMessage(parseErrors), HttpStatusCode.BadRequest);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Applicant upload parsing failed.");
                return BuildExecuteErrorResponse($"Failed to read Excel file: {ex.Message}", HttpStatusCode.BadRequest);
            }

            if (!parsedRows.Any())
            {
                return BuildExecuteErrorResponse("No data rows found in the uploaded file.", HttpStatusCode.BadRequest);
            }

            var stCodes = parsedRows
                .Select(r => r.JobLocationCode)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim().ToUpperInvariant())
                .Distinct()
                .ToList();

            var locationMap = await _context.tblLocations
                .Where(l => stCodes.Contains(l.STCode))
                .ToDictionaryAsync(l => l.STCode, StringComparer.OrdinalIgnoreCase);

            var missingCodes = stCodes.Where(code => !locationMap.ContainsKey(code)).ToList();
            if (missingCodes.Any())
            {
                return BuildExecuteErrorResponse($"Unknown STCode(s): {string.Join(", ", missingCodes)}", HttpStatusCode.BadRequest);
            }

            var requestedStatuses = parsedRows
                .Select(r => r.StatusName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim().TrimEnd(';', ',', '.', ' ', '\t'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var missingStatuses = requestedStatuses.Where(name => !AllowedStatusMap.ContainsKey(name)).ToList();
            if (missingStatuses.Any())
            {
                var allowedStatuses = AllowedStatusMap.Keys.OrderBy(n => n).ToList();
                return BuildExecuteErrorResponse(
                    $"Unknown Status(es): {string.Join(", ", missingStatuses)}. Allowed statuses: {string.Join(", ", allowedStatuses)}",
                    HttpStatusCode.BadRequest);
            }

            var statusMap = AllowedStatusMap;

            var requestedEcodes = parsedRows
                .SelectMany(r => r.Interviews)
                .SelectMany(i => i.Interviewers)
                .Select(i => i.Name) // This now contains Ecode, not name
                .Where(ecode => !string.IsNullOrWhiteSpace(ecode))
                .Select(ecode => ecode.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var employeeMap = new Dictionary<string, tblEmployee>(StringComparer.OrdinalIgnoreCase);
            var interviewerMap = new Dictionary<string, Interviewer>(StringComparer.OrdinalIgnoreCase);
            if (requestedEcodes.Any())
            {
                // Validate Ecodes against tblEmployee where IsActive = true and IsDeleted = false
                try
                {
                    var employeeLookup = await _context.tblEmployees
                        .Where(e => !string.IsNullOrWhiteSpace(e.Ecode))
                        .Where(e => requestedEcodes.Contains(e.Ecode.Trim().ToUpper()) &&
                                    e.IsActive == true &&
                                    e.IsDeleted == false)
                        .ToListAsync();
                foreach (var employee in employeeLookup)
                {
                    if (!string.IsNullOrWhiteSpace(employee.Ecode))
                    {
                        var key = employee.Ecode.Trim().ToUpperInvariant();
                        if (!employeeMap.ContainsKey(key))
                        {
                            employeeMap[key] = employee;
                        }
                    }
                }

                foreach (var employee in employeeLookup)
                {
                    var ecodeKey = employee.Ecode.Trim().ToUpperInvariant();
                    var employeeName = $"{employee.FirstName} {employee.LastName}".Trim();
                    if (employee.EmployeeId > int.MaxValue)
                    {
                        throw new InvalidOperationException($"EmployeeId {employee.EmployeeId} exceeds supported range for interviewer mapping.");
                    }

                    interviewerMap[ecodeKey] = new Interviewer
                    {
                        InterviewerId = (int)employee.EmployeeId,
                        Name = string.IsNullOrWhiteSpace(employeeName) ? employee.Ecode : employeeName
                    };
                }
                }
                catch (Exception ex) { }

                var missingEcodes = requestedEcodes.Where(ecode => !employeeMap.ContainsKey(ecode)).ToList();
                if (missingEcodes.Any())
                {
                    var allowedEcodes = await _context.tblEmployees
                        .Where(e => e.IsActive == true && e.IsDeleted == false && !string.IsNullOrWhiteSpace(e.Ecode))
                        .Select(e => e.Ecode)
                        .Distinct()
                        .OrderBy(e => e)
                        .ToListAsync();

                    return BuildExecuteErrorResponse(
                        $"Unknown interviewer Ecode(s): {string.Join(", ", missingEcodes)}. Please use valid Ecodes from active employees. Allowed Ecodes: {string.Join(", ", allowedEcodes.Take(50))}{(allowedEcodes.Count > 50 ? "..." : "")}",
                        HttpStatusCode.BadRequest);
                }
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
            var departmentNames = parsedRows
                .Select(r => r.DepartmentName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var departmentMap = await _context.tblDepartments
                .Where(d => departmentNames.Contains(d.DepartmentName))
                .ToDictionaryAsync(d => d.DepartmentName, StringComparer.OrdinalIgnoreCase);

            var missingDepartments = departmentNames.Where(name => !departmentMap.ContainsKey(name)).ToList();
            if (missingDepartments.Any())
            {
                return BuildExecuteErrorResponse($"Unknown Department(s): {string.Join(", ", missingDepartments)}", HttpStatusCode.BadRequest);
            }

            var designationNames = parsedRows
                .Select(r => r.DesignationName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var designationMap = await _context.tblDesignations
                .Where(d => designationNames.Contains(d.DesignationName))
                .ToDictionaryAsync(d => d.DesignationName, StringComparer.OrdinalIgnoreCase);

            var missingDesignations = designationNames.Where(name => !designationMap.ContainsKey(name)).ToList();
            if (missingDesignations.Any())
            {
                return BuildExecuteErrorResponse($"Unknown Designation(s): {string.Join(", ", missingDesignations)}", HttpStatusCode.BadRequest);
            }
                var lastApplicantId = await _context.Candidates
                    .OrderByDescending(c => c.Id)
                    .Select(c => c.ApplicantId)
                    .FirstOrDefaultAsync();

                var applicantSequence = ExtractApplicantSequence(lastApplicantId);
                var processedRows = BuildCandidates(parsedRows, locationMap, departmentMap, designationMap, statusMap, interviewerMap, ref applicantSequence, uploadedBy);

                await _context.Candidates.AddRangeAsync(processedRows.Select(r => r.Candidate));
                await _context.SaveChangesAsync();

                var experiences = BuildExperienceRows(processedRows, uploadedBy);
                if (experiences.Any())
                {
                    await _context.tblExperiences.AddRangeAsync(experiences);
                }

                var qualifications = BuildQualificationRows(processedRows, uploadedBy);
                if (qualifications.Any())
                {
                    await _context.tblQualifications.AddRangeAsync(qualifications);
                }

                var locationHistories = BuildAssignLocationRows(processedRows, uploadedBy);
                if (locationHistories.Any())
                {
                    await _context.AssignLocationHistories.AddRangeAsync(locationHistories);
                }

                var statusHistories = BuildStatusHistoryRows(processedRows, uploadedBy);
                if (statusHistories.Any())
                {
                    await _context.CandidateStatus_Histories.AddRangeAsync(statusHistories);
                }

                var scheduleSeeds = BuildScheduleSeeds(processedRows, uploadedBy);
                if (scheduleSeeds.Any())
                {
                    await _context.tblScheduleInterviews.AddRangeAsync(scheduleSeeds.Select(s => s.Schedule));
                }

                await _context.SaveChangesAsync();

                if (scheduleSeeds.Any())
                {
                    foreach (var seed in scheduleSeeds)
                    {
                        foreach (var round in seed.InterviewRounds)
                        {
                            round.ScheduleId = seed.Schedule.ScheduleId;
                        }

                        foreach (var log in seed.ApprovalLogs)
                        {
                            log.ScheduleId = seed.Schedule.ScheduleId;
                        }
                    }

                    var interviewRounds = scheduleSeeds.SelectMany(s => s.InterviewRounds).ToList();
                    if (interviewRounds.Any())
                    {
                        await _context.tblInterviewRounds.AddRangeAsync(interviewRounds);
                    }

                    var approvalLogs = scheduleSeeds.SelectMany(s => s.ApprovalLogs).ToList();
                    if (approvalLogs.Any())
                    {
                        await _context.InterviewApprovalLogs.AddRangeAsync(approvalLogs);
                    }

                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                return BuildExecuteSuccessResponse($"Uploaded {processedRows.Count} applicant(s) successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to upload applicants.");
                return BuildExecuteErrorResponse("Failed to upload applicants. Please check the logs for details.", HttpStatusCode.InternalServerError);
            }
        }

        private static List<ApplicantUploadParsedRow> ParseExcel(IFormFile file, out List<string> missingHeaders, out List<string> rowErrors)
        {
            missingHeaders = new List<string>();
            rowErrors = new List<string>();
            var parsedRows = new List<ApplicantUploadParsedRow>();

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            var headerRow = worksheet.FirstRowUsed();
            if (headerRow == null)
            {
                missingHeaders.Add("Header row not found.");
                return parsedRows;
            }

            var headerMap = ResolveHeaderIndexes(headerRow, out missingHeaders);
            if (missingHeaders.Any())
            {
                return parsedRows;
            }

            foreach (var row in worksheet.RowsUsed().Skip(1))
            {
                if (IsRowEmpty(row))
                {
                    continue;
                }

                var parsed = ParseRow(row, headerMap, out var rowValidationErrors);
                if (rowValidationErrors.Any())
                {
                    rowErrors.AddRange(rowValidationErrors);
                    continue;
                }

                parsedRows.Add(parsed);
            }

            return parsedRows;
        }

        private static Dictionary<string, int> ResolveHeaderIndexes(IXLRow headerRow, out List<string> missingHeaders)
        {
            missingHeaders = new List<string>();
            var headerMap = headerRow.CellsUsed()
                .Where(cell => !string.IsNullOrWhiteSpace(cell.GetValue<string>()))
                .ToDictionary(cell => cell.GetValue<string>().Trim(), cell => cell.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);

            var resolved = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in TemplateColumns)
            {
                foreach (var alias in column.Aliases)
                {
                    if (headerMap.TryGetValue(alias, out var index))
                    {
                        resolved[column.Key] = index;
                        break;
                    }
                }

                if (column.Required && !resolved.ContainsKey(column.Key))
                {
                    missingHeaders.Add(string.Join(" / ", column.Aliases));
                }
            }

            return resolved;
        }

        private static ApplicantUploadParsedRow ParseRow(IXLRow row, IDictionary<string, int> headerMap, out List<string> errors)
        {
            errors = new List<string>();
            var rowNumber = row.RowNumber();

            var jobCode = GetString(row, headerMap, "JobPosition");
            var departmentName = GetString(row, headerMap, "DepartmentName");
            var designationName = GetString(row, headerMap, "DesignationName");
            var preferredLocation = GetString(row, headerMap, "PreferredLocation");
            var statusName = GetString(row, headerMap, "StatusName")?.Trim().TrimEnd(';', ',', '.', ' ', '\t');
            var fullName = GetString(row, headerMap, "FullName");
            var phone = GetString(row, headerMap, "Phone");

            if (string.IsNullOrWhiteSpace(jobCode))
            {
                errors.Add($"Row {rowNumber}: Job Position (STCode) is required.");
            }
            if (string.IsNullOrWhiteSpace(departmentName))
            {
                errors.Add($"Row {rowNumber}: Department is required.");
            }
            if (string.IsNullOrWhiteSpace(designationName))
            {
                errors.Add($"Row {rowNumber}: Designation is required.");
            }
            if (string.IsNullOrWhiteSpace(preferredLocation))
            {
                errors.Add($"Row {rowNumber}: Preferred Job Location is required.");
            }
            if (string.IsNullOrWhiteSpace(fullName))
            {
                errors.Add($"Row {rowNumber}: Full Name is required.");
            }
            if (string.IsNullOrWhiteSpace(phone))
            {
                errors.Add($"Row {rowNumber}: Phone is required.");
            }
            if (string.IsNullOrWhiteSpace(statusName))
            {
                errors.Add($"Row {rowNumber}: Status is required.");
            }

            var dob = GetDate(row, headerMap, "DOB");
            var joiningDate = GetDate(row, headerMap, "JoiningDate");
            var totalExperience = GetDecimal(row, headerMap, "TotalExperience");
            var previousSalary = GetDecimal(row, headerMap, "PreviousSalary");
            var salaryExpectation = GetDecimal(row, headerMap, "SalaryExpectation");

            if (totalExperience == decimal.MinValue)
            {
                errors.Add($"Row {rowNumber}: Invalid Total Experience value.");
                totalExperience = null;
            }
            if (previousSalary == decimal.MinValue)
            {
                errors.Add($"Row {rowNumber}: Invalid Previous Salary value.");
                previousSalary = null;
            }
            if (salaryExpectation == decimal.MinValue)
            {
                errors.Add($"Row {rowNumber}: Invalid Salary Expectation value.");
                salaryExpectation = null;
            }

            var interviewEntries = new List<ApplicantUploadParsedInterview>();
            var interview1Date = GetDate(row, headerMap, "Interview1Date");
            var interview1Names = GetString(row, headerMap, "Interview1Interviewers");
            var interview1Status = GetString(row, headerMap, "Interview1Status");
            TryAddInterview(interviewEntries, rowNumber, 1, interview1Date, interview1Names, interview1Status, statusName, errors);

            var interview2Date = GetDate(row, headerMap, "Interview2Date");
            var interview2Names = GetString(row, headerMap, "Interview2Interviewers");
            var interview2Status = GetString(row, headerMap, "Interview2Status");
            TryAddInterview(interviewEntries, rowNumber, 2, interview2Date, interview2Names, interview2Status, statusName, errors);

            // Business rule: If no interview details are provided, only certain statuses are allowed
            if (interviewEntries.Count == 0 && !string.IsNullOrWhiteSpace(statusName))
            {
                var statusTrimmed = statusName.Trim();
                if (!StatusesAllowedWithoutInterview.Any(s => string.Equals(s, statusTrimmed, StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add($"Row {rowNumber}: Status '{statusTrimmed}' is not allowed when no interview details are provided. Allowed statuses without interview details: {string.Join(", ", StatusesAllowedWithoutInterview)}");
                }
            }

            var finalRemark = GetString(row, headerMap, "InterviewFinalRemark");

            return new ApplicantUploadParsedRow
            {
                RowNumber = rowNumber,
                JobLocationCode = jobCode,
                PreferredLocationValue = preferredLocation,
                DepartmentName = departmentName,
                DesignationName = designationName,
                FullName = fullName,
                Phone = phone,
                StatusName = statusName,
                Email = GetString(row, headerMap, "Email"),
                Dob = dob,
                JoiningDate = joiningDate,
                TotalExperience = totalExperience,
                PreviousCompany = GetString(row, headerMap, "PreviousCompany"),
                PreviousDesignation = GetString(row, headerMap, "PreviousDesignation"),
                PreviousSalary = previousSalary,
                SalaryExpectation = salaryExpectation,
                Source = GetString(row, headerMap, "Source"),
                Reference = GetString(row, headerMap, "Reference"),
                HigherQualification = GetString(row, headerMap, "HigherQualification"),
                PassingYear = GetString(row, headerMap, "PassingYear"),
                Notes = GetString(row, headerMap, "Notes"),
                InterviewFinalRemark = finalRemark,
                Interviews = interviewEntries
            };
        }

        private static bool IsRowEmpty(IXLRow row)
        {
            return !row.CellsUsed().Any(cell => !string.IsNullOrWhiteSpace(cell.GetValue<string>()));
        }

        private static string GetString(IXLRow row, IDictionary<string, int> headerMap, string key)
        {
            if (!headerMap.TryGetValue(key, out var columnIndex))
            {
                return string.Empty;
            }

            var cell = row.Cell(columnIndex);
            if (cell.DataType == XLDataType.Number)
            {
                var numericValue = cell.GetDouble();
                return numericValue % 1 == 0
                    ? Convert.ToInt64(numericValue).ToString(CultureInfo.InvariantCulture)
                    : numericValue.ToString(CultureInfo.InvariantCulture);
            }

            return cell.GetValue<string>()?.Trim() ?? string.Empty;
        }

        private static DateTime? GetDate(IXLRow row, IDictionary<string, int> headerMap, string key)
        {
            if (!headerMap.TryGetValue(key, out var columnIndex))
            {
                return null;
            }

            var cell = row.Cell(columnIndex);
            if (cell.DataType == XLDataType.DateTime)
            {
                return cell.GetDateTime();
            }

            var raw = cell.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var formats = new[]
            {
                "dd-MM-yyyy", "d-M-yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "yyyy/MM/dd", "MM/dd/yyyy"
            };

            if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
            {
                return parsed;
            }

            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed))
            {
                return parsed;
            }

            return null;
        }

        private static decimal? GetDecimal(IXLRow row, IDictionary<string, int> headerMap, string key)
        {
            if (!headerMap.TryGetValue(key, out var columnIndex))
            {
                return null;
            }

            var cell = row.Cell(columnIndex);
            if (cell.DataType == XLDataType.Number)
            {
                return Convert.ToDecimal(cell.GetDouble());
            }

            var raw = cell.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            raw = raw.Replace(",", "").Replace("₹", "").Replace("INR", "", StringComparison.OrdinalIgnoreCase);
            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.GetCultureInfo("en-IN"), out value))
            {
                return value;
            }

            return decimal.MinValue;
        }

        private static void TryAddInterview(
            IList<ApplicantUploadParsedInterview> target,
            int rowNumber,
            int roundId,
            DateTime? interviewDate,
            string interviewerNamesRaw, // This now contains Ecodes, not names
            string interviewerStatusesRaw,
            string candidateStatus,
            IList<string> errors)
        {
            var hasData = interviewDate.HasValue ||
                          !string.IsNullOrWhiteSpace(interviewerNamesRaw) ||
                          !string.IsNullOrWhiteSpace(interviewerStatusesRaw);

            if (!hasData)
            {
                return;
            }

            // If candidate status is in the allowed list, skip validation and allow empty interview info
            if (!string.IsNullOrWhiteSpace(candidateStatus) &&
                StatusesAllowedWithoutInterview.Any(s => string.Equals(s, candidateStatus.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                // For allowed statuses, if interview data is incomplete, just skip adding it (no error)
                if (!interviewDate.HasValue || string.IsNullOrWhiteSpace(interviewerNamesRaw))
                {
                    return;
                }
            }

            if (!interviewDate.HasValue)
            {
                errors.Add($"Row {rowNumber}: Interview {roundId} date/time is required when interviewer data is provided.");
                return;
            }

            var interviewerEcodes = SplitValues(interviewerNamesRaw);
            if (!interviewerEcodes.Any())
            {
                errors.Add($"Row {rowNumber}: Interview {roundId} interviewer Ecode(s) are required.");
                return;
            }

            var statusValues = SplitValues(interviewerStatusesRaw);
            var normalizedStatuses = new List<string?>();
            if (statusValues.Count == interviewerEcodes.Count)
            {
                normalizedStatuses.AddRange(statusValues);
            }
            else if (statusValues.Count > 0)
            {
                normalizedStatuses.AddRange(interviewerEcodes.Select(_ => statusValues[0]));
            }
            else
            {
                normalizedStatuses.AddRange(interviewerEcodes.Select(_ => (string?)null));
            }

            var errorCountBeforeStatusValidation = errors.Count;
            for (var idx = 0; idx < normalizedStatuses.Count; idx++)
            {
                var statusValue = normalizedStatuses[idx];
                if (string.IsNullOrWhiteSpace(statusValue))
                {
                    normalizedStatuses[idx] = null;
                    continue;
                }

                var trimmedStatus = statusValue.Trim();
                var matchedStatus = AllowedInterviewerStatuses.FirstOrDefault(
                    s => string.Equals(s, trimmedStatus, StringComparison.OrdinalIgnoreCase));

                if (matchedStatus == null)
                {
                    errors.Add($"Row {rowNumber}: Interview {roundId} status '{trimmedStatus}' is invalid. Allowed status values: {string.Join(", ", AllowedInterviewerStatuses)}");
                }
                else
                {
                    normalizedStatuses[idx] = matchedStatus;
                }
            }

            if (errors.Count > errorCountBeforeStatusValidation)
            {
                return;
            }

            var parsed = new ApplicantUploadParsedInterview
            {
                RoundId = roundId,
                InterviewDateTime = interviewDate.Value
            };

            for (var i = 0; i < interviewerEcodes.Count; i++)
            {
                parsed.Interviewers.Add(new ApplicantUploadParsedInterviewer
                {
                    Name = interviewerEcodes[i], // This now contains Ecode, not name
                    Status = normalizedStatuses[i]
                });
            }

            target.Add(parsed);
        }

        private static List<string> SplitValues(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new List<string>();
            }

            return raw
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrEmpty(value))
                .ToList();
        }

        private static int ExtractApplicantSequence(string? applicantId)
        {
            if (string.IsNullOrWhiteSpace(applicantId) || applicantId.Length < 3)
            {
                return 0;
            }

            var digits = new string(applicantId.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out var sequence) ? sequence : 0;
        }

        private static List<ApplicantUploadProcessedRow> BuildCandidates(
            IEnumerable<ApplicantUploadParsedRow> rows,
            IDictionary<string, tblLocation> locationMap,
            IDictionary<string, tblDepartment> departmentMap,
            IDictionary<string, tblDesignation> designationMap,
            IDictionary<string, CandidateProcessStatus> statusMap,
            IDictionary<string, Interviewer> interviewerMap,
            ref int sequence,
            string uploadedBy)
        {
            var list = new List<ApplicantUploadProcessedRow>();
            var now = DateTime.UtcNow;

            foreach (var row in rows)
            {
                sequence++;
                var applicantId = $"AV{sequence.ToString("D6")}";
                var nameParts = SplitName(row.FullName);
                var jobLocation = locationMap[row.JobLocationCode.Trim().ToUpperInvariant()];
                var preferredLocationValue = string.IsNullOrWhiteSpace(row.PreferredLocationValue)
                    ? null
                    : row.PreferredLocationValue;
                var department = departmentMap[row.DepartmentName];
                var designation = designationMap[row.DesignationName];
                var statusNameCleaned = row.StatusName?.Trim().TrimEnd(';', ',', '.', ' ', '\t') ?? string.Empty;
                var status = statusMap[statusNameCleaned];

                var candidate = new HRMSAPI.Data.Candidate
                {
                    ApplicantId = applicantId,
                    APPLICANT_CODE = applicantId,
                    FIRST_NAME = nameParts.first,
                    MIDDLE_NAME = nameParts.middle,
                    LAST_NAME = nameParts.last,
                    MOBILE = row.Phone,
                    EMAIL_ADDRESS = row.Email,
                    DOB = row.Dob,
                    JOINING_DATE = row.JoiningDate,
                    LOCATION = jobLocation.LocationId.ToString(CultureInfo.InvariantCulture),
                    WORK_LOCATION = jobLocation.LocationId.ToString(CultureInfo.InvariantCulture),
                    PreferredLocation = preferredLocationValue,
                    DEPARTMENT = department.DepartmentId.ToString(CultureInfo.InvariantCulture),
                    DESIGNATION = designation.DesignationId.ToString(CultureInfo.InvariantCulture),
                    Source = row.Source,
                    REFERENCE = row.Reference,
                    HIGHEST_QUALIFICATION = row.HigherQualification,
                    TotalExperience = row.TotalExperience,
                    SalaryExpectation = row.SalaryExpectation,
                    COMPANY_1 = row.PreviousCompany,
                    POSITION_HELD_IN_PREVIOUS_COMPANY = row.PreviousDesignation,
                    In_Hand_Salary = row.PreviousSalary?.ToString(CultureInfo.InvariantCulture),
                    LAST_CTC_ANNUAL_ = row.PreviousSalary?.ToString(CultureInfo.InvariantCulture),
                    AdditionalInfoApplicant = row.Notes,
                    StatusId = status.StatusId,
                    IsApplicant = true,
                    IsApplicantApproved = false,
                    IsActive = true,
                    IsDeleted = false,
                    CreatedBy = uploadedBy,
                    CreatedOn = now,
                    UpdatedOn = now
                };

                var processedInterviews = new List<ApplicantUploadProcessedInterview>();
                foreach (var parsedInterview in row.Interviews)
                {
                    var processed = new ApplicantUploadProcessedInterview
                    {
                        InterviewDateTime = parsedInterview.InterviewDateTime,
                        Mode = parsedInterview.Mode,
                        Location = parsedInterview.Location,
                        Notes = parsedInterview.Notes,
                        RoundId = parsedInterview.RoundId
                    };

                    foreach (var parsedInterviewer in parsedInterview.Interviewers)
                    {
                        // parsedInterviewer.Name now contains Ecode
                        var ecodeKey = parsedInterviewer.Name?.Trim().ToUpperInvariant() ?? string.Empty;
                        if (!interviewerMap.TryGetValue(ecodeKey, out var interviewer))
                        {
                            continue;
                        }

                        processed.Interviewers.Add(new ApplicantUploadProcessedInterviewer
                        {
                            InterviewerId = interviewer.InterviewerId,
                            Name = interviewer.Name,
                            Status = parsedInterviewer.Status
                        });
                    }

                    if (processed.Interviewers.Any())
                    {
                        processedInterviews.Add(processed);
                    }
                }

                list.Add(new ApplicantUploadProcessedRow
                {
                    Row = row,
                    Candidate = candidate,
                    JobLocation = jobLocation,
                    PreferredLocation = preferredLocationValue,
                    Department = department,
                    Designation = designation,
                    Status = status,
                    Interviews = processedInterviews
                });
            }

            return list;
        }

        private static (string first, string? middle, string? last) SplitName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return (string.Empty, null, null);
            }

            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch
            {
                0 => (string.Empty, null, null),
                1 => (parts[0], null, null),
                2 => (parts[0], null, parts[1]),
                _ => (parts[0], string.Join(" ", parts.Skip(1).Take(parts.Length - 2)), parts[^1])
            };
        }

        private static List<tblExperience> BuildExperienceRows(IEnumerable<ApplicantUploadProcessedRow> rows, string uploadedBy)
        {
            var list = new List<tblExperience>();
            var now = DateTime.UtcNow;

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Row.PreviousCompany) &&
                    string.IsNullOrWhiteSpace(row.Row.PreviousDesignation) &&
                    !row.Row.PreviousSalary.HasValue)
                {
                    continue;
                }

                list.Add(new tblExperience
                {
                    CID = row.Candidate.Id,
                    Name_of_Company = row.Row.PreviousCompany,
                    Work_Location = row.JobLocation.LocationName,
                    Position_Held = row.Row.PreviousDesignation,
                    Last_CTC = row.Row.PreviousSalary,
                    InHand = row.Row.PreviousSalary,
                    Expected_CTC = row.Row.SalaryExpectation,
                    TotalIndustryExperience_yrs = row.Row.TotalExperience.HasValue ? Convert.ToInt32(Math.Round(row.Row.TotalExperience.Value, MidpointRounding.AwayFromZero)) : null,
                    CreatedOn = now,
                    CreatedBy = uploadedBy,
                    IsActive = true,
                    IsDeleted = false
                });
            }

            return list;
        }

        private static List<tblQualification> BuildQualificationRows(IEnumerable<ApplicantUploadProcessedRow> rows, string uploadedBy)
        {
            var list = new List<tblQualification>();
            var now = DateTime.UtcNow;

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Row.HigherQualification) && string.IsNullOrWhiteSpace(row.Row.PassingYear))
                {
                    continue;
                }

                list.Add(new tblQualification
                {
                    CID = row.Candidate.Id,
                    Education = row.Row.HigherQualification,
                    YOP = row.Row.PassingYear,
                    Type = "Highest",
                    CreatedOn = now,
                    CreatedBy = uploadedBy,
                    IsActive = true,
                    IsDeleted = false
                });
            }

            return list;
        }

        private static List<AssignLocationHistory> BuildAssignLocationRows(IEnumerable<ApplicantUploadProcessedRow> rows, string uploadedBy)
        {
            var list = new List<AssignLocationHistory>();
            var now = DateTime.UtcNow;

            foreach (var row in rows)
            {
                list.Add(new AssignLocationHistory
                {
                    CandidateId = row.Candidate.Id,
                    AssignedLocation = row.JobLocation.LocationId,
                    departmentid = row.Department?.DepartmentId,
                    designationid = row.Designation?.DesignationId,
                    AssignedReason = "Applicant upload",
                    IsActive = true,
                    AssignedOnDate = now,
                    TransferApprovalStatus = 0,
                    IsReportingHeadApproval = 0,
                    IsHRApproval = 0,
                    PermanentTransfer = false,
                    TemporaryTransfer = false,
                    CreatedOn = now,
                    CreatedBy = uploadedBy,
                    IsDeleted = false
                });
            }

            return list;
        }

        private static List<CandidateStatus_History> BuildStatusHistoryRows(IEnumerable<ApplicantUploadProcessedRow> rows, string uploadedBy)
        {
            var list = new List<CandidateStatus_History>();
            var now = DateTime.UtcNow;

            foreach (var row in rows)
            {
                if (row.Status == null)
                {
                    continue;
                }

                list.Add(new CandidateStatus_History
                {
                    ApplicantId = Convert.ToInt32(row.Candidate.Id),
                    OldStatusId = null,
                    OldStatusName = null,
                    NewStatusId = row.Status.StatusId,
                    NewStatusName = row.Status.StatusName,
                    CreatedDate = now,
                    CreatedBy = uploadedBy
                });
            }

            return list;
        }

        private static List<ScheduleSeed> BuildScheduleSeeds(IEnumerable<ApplicantUploadProcessedRow> rows, string uploadedBy)
        {
            var list = new List<ScheduleSeed>();
            var now = DateTime.UtcNow;

            foreach (var row in rows)
            {
                if (row.Interviews == null || row.Interviews.Count == 0)
                {
                    continue;
                }

                foreach (var interview in row.Interviews)
                {
                    if (interview == null || interview.Interviewers.Count == 0)
                    {
                        continue;
                    }

                    var schedule = new tblScheduleInterview
                    {
                        ApplicantId = Convert.ToInt32(row.Candidate.Id),
                        CandidateName = string.IsNullOrWhiteSpace(row.Row.FullName)
                            ? $"{row.Candidate.FIRST_NAME} {row.Candidate.LAST_NAME}".Trim()
                            : row.Row.FullName,
                        InterviewDateTime = interview.InterviewDateTime,
                        InterviewMode = string.IsNullOrWhiteSpace(interview.Mode) ? "In-Person" : interview.Mode,
                        InterviewLocation = string.IsNullOrWhiteSpace(interview.Location) ? row.JobLocation.LocationId.ToString(CultureInfo.InvariantCulture) : interview.Location,
                        Notes = !string.IsNullOrWhiteSpace(interview.Notes) ? interview.Notes : row.Row.InterviewFinalRemark,
                        RoundId = interview.RoundId,
                        CreatedBy = uploadedBy,
                        CreatedOn = now,
                        IsActive = true,
                        IsDeleted = false
                    };

                    var rounds = interview.Interviewers.Select(interviewer => new tblInterviewRound
                    {
                        RoundId = interview.RoundId,
                        InterviewerId = interviewer.InterviewerId,
                        FeedBack = row.Row.InterviewFinalRemark,
                        Status = interviewer.Status ?? "Pending",
                        InterviewerStatus = interviewer.Status ?? "Pending",
                        CreatedBy = uploadedBy,
                        CreatedDate = now
                    }).ToList();

                    var logs = interview.Interviewers.Select(interviewer => new InterviewApprovalLog
                    {
                        InterviewerId = interviewer.InterviewerId,
                        RoundId = interview.RoundId,
                        ApprovedBy = uploadedBy,
                        ApprovalDate = now,
                        Feedback = row.Row.InterviewFinalRemark,
                        Status = interviewer.Status ?? "Pending"
                    }).ToList();

                    list.Add(new ScheduleSeed
                    {
                        Row = row,
                        Interview = interview,
                        Schedule = schedule,
                        InterviewRounds = rounds,
                        ApprovalLogs = logs
                    });
                }
            }

            return list;
        }

        private static string BuildValidationMessage(IReadOnlyCollection<string> errors)
        {
            const int maxItems = 20;
            if (errors.Count <= maxItems)
            {
                return string.Join(" | ", errors);
            }

            return $"{string.Join(" | ", errors.Take(maxItems))} ... (showing {maxItems} of {errors.Count} issues)";
        }

        private class ApplicantUploadParsedRow
        {
            public int RowNumber { get; set; }
            public string JobLocationCode { get; set; } = string.Empty;
            public string PreferredLocationValue { get; set; } = string.Empty;
            public string DepartmentName { get; set; } = string.Empty;
            public string DesignationName { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string StatusName { get; set; } = string.Empty;
            public string? Email { get; set; }
            public DateTime? Dob { get; set; }
            public DateTime? JoiningDate { get; set; }
            public decimal? TotalExperience { get; set; }
            public string? PreviousCompany { get; set; }
            public string? PreviousDesignation { get; set; }
            public decimal? PreviousSalary { get; set; }
            public decimal? SalaryExpectation { get; set; }
            public string? Source { get; set; }
            public string? Reference { get; set; }
            public string? HigherQualification { get; set; }
            public string? PassingYear { get; set; }
            public string? Notes { get; set; }
            public string? InterviewFinalRemark { get; set; }
            public List<ApplicantUploadParsedInterview> Interviews { get; set; } = new();
        }

        private class ApplicantUploadProcessedRow
        {
            public ApplicantUploadParsedRow Row { get; set; }
            public HRMSAPI.Data.Candidate Candidate { get; set; }
            public tblLocation JobLocation { get; set; }
            public string? PreferredLocation { get; set; }
            public tblDepartment Department { get; set; }
            public tblDesignation Designation { get; set; }
            public CandidateProcessStatus Status { get; set; }
            public List<ApplicantUploadProcessedInterview> Interviews { get; set; } = new();
        }

        private class ApplicantUploadParsedInterview
        {
            public int RoundId { get; set; }
            public DateTime InterviewDateTime { get; set; }
            public string? Mode { get; set; }
            public string? Location { get; set; }
            public string? Notes { get; set; }
            public List<ApplicantUploadParsedInterviewer> Interviewers { get; set; } = new();
        }

        private class ApplicantUploadParsedInterviewer
        {
            public string Name { get; set; } = string.Empty;
            public string? Status { get; set; }
        }

        private class ApplicantUploadProcessedInterview
        {
            public DateTime InterviewDateTime { get; set; }
            public string? Mode { get; set; }
            public string? Location { get; set; }
            public string? Notes { get; set; }
            public int RoundId { get; set; }
            public List<ApplicantUploadProcessedInterviewer> Interviewers { get; set; } = new();
        }

        private class ApplicantUploadProcessedInterviewer
        {
            public int InterviewerId { get; set; }
            public string Name { get; set; }
            public string? Status { get; set; }
        }

        private class ScheduleSeed
        {
            public ApplicantUploadProcessedRow Row { get; set; }
            public ApplicantUploadProcessedInterview Interview { get; set; }
            public tblScheduleInterview Schedule { get; set; }
            public List<tblInterviewRound> InterviewRounds { get; set; } = new();
            public List<InterviewApprovalLog> ApprovalLogs { get; set; } = new();
        }
    }
}

