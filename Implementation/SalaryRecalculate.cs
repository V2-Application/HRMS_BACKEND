using ClosedXML.Excel;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Roomsy.DTOS.GenericsResponses;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace HRMSAPI.Implementation
{
    public class SalaryRecalculateRepository : ISalaryRecalculate
    {
        private readonly HRMSContext _context;

        public SalaryRecalculateRepository(HRMSContext context)
        {
            _context = context;
        }

        public async Task<ExecuteAndReponse> SalaryRecalculate(SalaryRecalculateDto obj)
        {
            try
            {
                // 1. Validate ECodes
                if (string.IsNullOrWhiteSpace(obj.ECodes))
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Ecodes cannot be empty."
                    };
                }

                // 2. Validate Month format (MMM-YY)
                if (!DateTime.TryParseExact(obj.Month, "MMM-yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedMonth))
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Month must be in format MMM-YY (e.g., Jul-25)."
                    };
                }
                // 2.a Disallow future months (including future years)
                var currentMonthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var parsedMonthStart = new DateTime(parsedMonth.Year, parsedMonth.Month, 1);
                if (parsedMonthStart > currentMonthStart)
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Month cannot be in the future."
                    };
                }

                // Execute stored procedure
                var ecodeList = obj.ECodes
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim())
            .ToList();

                if (ecodeList.Count == 0)
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "No valid ECodes found."
                    };
                }
                // 3. Validate that all ECodes exist in tblEmployee (case-insensitive)
                var existingEcodes = await _context.tblEmployees
                    .AsNoTracking()
                    .Select(e => e.Ecode)
                    .ToListAsync();

                var existingSet = new HashSet<string>(existingEcodes.Where(x => x != null).Select(x => x.Trim()), StringComparer.OrdinalIgnoreCase);
                var missingEcodes = ecodeList.Where(e => !existingSet.Contains(e)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (missingEcodes.Any())
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = $"These ECodes do not exist: {string.Join(", ", missingEcodes)}"
                    };
                }

                // Call procedure for each ECode
                foreach (var ecode in ecodeList)
                {
                    await _context.GetProcedures()
                        .prc_runecode_iterate_wrapper_PT_LWFAsync(ecode, obj.Month);
                }

                return new ExecuteAndReponse
                {
                    Status = true,
                    Message = "Executed Successfully."
                };
            }
            catch (Exception ex)
            {
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message,
                };
            }
        }

        public async Task<ExecuteAndReponse> SalaryRecalculateByMonth(SalaryRecalculateByMonthDto obj)
        {
            try
            {
                // 1. Validate Month format (MMM-YY)
                if (string.IsNullOrWhiteSpace(obj.Month))
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Month cannot be empty."
                    };
                }

                if (!DateTime.TryParseExact(obj.Month, "MMM-yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Month must be in format MMM-YY (e.g., Jul-25)."
                    };
                }

                // Execute stored procedure for all employees by month
                var result = await _context.GetProcedures()
                    .prc_iterate_PT_LWF_by_monthAsync(obj.Month);

                return new ExecuteAndReponse
                {
                    Status = true,
                    Message = $"Salary recalculation completed successfully for month {obj.Month}."
                };
            }
            catch (Exception ex)
            {
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message,
                };
            }
        }

        public async Task<ExecuteAndReponse> SalaryRecalculateNew(SalaryRecalculateDto obj)
        {
            try
            {
                // 1. Validate ECodes
                if (string.IsNullOrWhiteSpace(obj.ECodes))
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Ecodes cannot be empty."
                    };
                }

                // 2. Validate Month format (MMM-YY)
                if (!DateTime.TryParseExact(obj.Month, "MMM-yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedMonth))
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Month must be in format MMM-YY (e.g., Jul-25)."
                    };
                }

                // 2.a Disallow future months (including future years)
                var currentMonthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var parsedMonthStart = new DateTime(parsedMonth.Year, parsedMonth.Month, 1);
                if (parsedMonthStart > currentMonthStart)
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Month cannot be in the future.",
                        Code = System.Net.HttpStatusCode.BadRequest
                    };
                }

                // Execute stored procedure
                var ecodeList = obj.ECodes
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim())
            .ToList();

                if (ecodeList.Count == 0)
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "No valid ECodes found.",
                        Code = System.Net.HttpStatusCode.BadRequest
                    };
                }

                // 3. Validate that all ECodes exist in tblEmployees (case-insensitive)
                var existingEcodes = await _context.tblEmployees
                    .AsNoTracking()
                    .Select(e => e.Ecode)
                    .ToListAsync();

                var existingSet = new HashSet<string>(existingEcodes.Where(x => x != null).Select(x => x.Trim()), StringComparer.OrdinalIgnoreCase);
                var missingEcodes = ecodeList.Where(e => !existingSet.Contains(e)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (missingEcodes.Any())
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = $"These ECodes do not exist: {string.Join(", ", missingEcodes)}"
                    };
                }
                var skippedMessage = new OutputParameter<string>();
                var previousTimeout = _context.Database.GetCommandTimeout();
                _context.Database.SetCommandTimeout(600); // 10 min — heavy multi-ecode recalculation
                try
                {
                    var result = await _context.GetProcedures().prc_runecode_iterate_New_DevAsync(obj.Month, obj.ECodes, skippedMessage);
                }
                finally
                {
                    _context.Database.SetCommandTimeout(previousTimeout);
                }

                // Call procedure for each ECode
                //foreach (var ecode in ecodeList)
                //{
                //    await _context.GetProcedures()
                //        .prc_runecode_iterate_wrapper_PT_LWFAsync(ecode, obj.Month);
                //}

                return new ExecuteAndReponse
                {
                    Status = true,
                    Message = $"Executed Successfully. {skippedMessage.Value}"
                };
            }
            catch (Exception ex)
            {
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message,
                };
            }
        }

        public async Task<ExecuteAndReponse> SalaryRecalculateByMonthNew(SalaryRecalculateByMonthDto obj)
        {
            try
            {
                // 1. Validate Month format (MMM-YY)
                if (string.IsNullOrWhiteSpace(obj.Month))
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Month cannot be empty."
                    };
                }

                if (!DateTime.TryParseExact(obj.Month, "MMM-yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Month must be in format MMM-YY (e.g., Jul-25)."
                    };
                }
                var skippedMessage = new OutputParameter<string>();
                // Execute stored procedure for all employees by month
                var previousTimeout = _context.Database.GetCommandTimeout();
                _context.Database.SetCommandTimeout(600); // 10 min — full-tenant recalculation is heavy
                try
                {
                    var result = await _context.GetProcedures().prc_runecode_iterate_New_DevAsync(obj.Month, null, skippedMessage);
                }
                finally
                {
                    _context.Database.SetCommandTimeout(previousTimeout);
                }

                return new ExecuteAndReponse
                {
                    Status = true,
                    Message = $"Salary recalculation completed successfully for month {obj.Month}. {skippedMessage.Value}"
                };
            }
            catch (Exception ex)
            {
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message,
                };
            }
        }
    }
}