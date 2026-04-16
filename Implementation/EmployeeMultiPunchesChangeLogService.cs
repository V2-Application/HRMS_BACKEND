using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;

namespace HRMSAPI.Implementation
{
    public class EmployeeMultiPunchesChangeLogService : IEmployeeMultiPunchesChangeLogService
    {
        private readonly HRMSContext _context;
        private readonly ILogger<EmployeeMultiPunchesChangeLogService> _logger;

        public EmployeeMultiPunchesChangeLogService(HRMSContext context, ILogger<EmployeeMultiPunchesChangeLogService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<EmployeeMultiPunchesChangeLogDto>> GetEmployeeMultiPunchesChangeLogAsync(string ecode, string month)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ecode))
                {
                    throw new ArgumentException("Ecode cannot be null or empty.", nameof(ecode));
                }

                if (string.IsNullOrWhiteSpace(month))
                {
                    throw new ArgumentException("Month cannot be null or empty.", nameof(month));
                }

                var changeLogs = new List<EmployeeMultiPunchesChangeLogDto>();

                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "[HRMS].[dbo].[usp_GetEmployeeMultiPunchesChangeLog]";
                        command.CommandType = CommandType.StoredProcedure;

                        // Add parameters
                        command.Parameters.Add(new SqlParameter("@Ecode", SqlDbType.NVarChar) { Value = ecode });
                        command.Parameters.Add(new SqlParameter("@MonthCode", SqlDbType.NVarChar) { Value = month });

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                // Helper method to safely get DateTime from reader (handles both DateTime and DateTimeOffset)
                                DateTime GetDateTimeValue(string columnName)
                                {
                                    var ordinal = reader.GetOrdinal(columnName);
                                    if (reader.IsDBNull(ordinal))
                                        return DateTime.MinValue;
                                    
                                    var value = reader.GetValue(ordinal);
                                    return value switch
                                    {
                                        DateTime dt => dt,
                                        DateTimeOffset dto => dto.DateTime,
                                        _ => DateTime.MinValue
                                    };
                                }

                                var changeLog = new EmployeeMultiPunchesChangeLogDto
                                {
                                    Ecode = reader.IsDBNull(reader.GetOrdinal("Ecode")) 
                                        ? string.Empty 
                                        : reader.GetString(reader.GetOrdinal("Ecode")),
                                    UserID = reader.IsDBNull(reader.GetOrdinal("UserID")) 
                                        ? string.Empty 
                                        : reader.GetString(reader.GetOrdinal("UserID")),
                                    PunchDate = GetDateTimeValue("PunchDate"),
                                    ColumnName = reader.IsDBNull(reader.GetOrdinal("ColumnName")) 
                                        ? string.Empty 
                                        : reader.GetString(reader.GetOrdinal("ColumnName")),
                                    OldValue = reader.IsDBNull(reader.GetOrdinal("OldValue")) 
                                        ? string.Empty 
                                        : reader.GetString(reader.GetOrdinal("OldValue")),
                                    NewValue = reader.IsDBNull(reader.GetOrdinal("NewValue")) 
                                        ? string.Empty 
                                        : reader.GetString(reader.GetOrdinal("NewValue")),
                                    VersionLabel = reader.IsDBNull(reader.GetOrdinal("VersionLabel")) 
                                        ? string.Empty 
                                        : reader.GetString(reader.GetOrdinal("VersionLabel")),
                                    ChangedBy = reader.IsDBNull(reader.GetOrdinal("ChangedBy")) 
                                        ? string.Empty 
                                        : reader.GetString(reader.GetOrdinal("ChangedBy")),
                                    ChangedOn = GetDateTimeValue("ChangedOn")
                                };
                                changeLogs.Add(changeLog);
                            }
                        }
                    }
                }

                return changeLogs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching employee multi punches change log for Ecode: {Ecode}, Month: {Month}", ecode, month);
                throw new ApplicationException($"An error occurred while fetching employee multi punches change log: {ex.Message}", ex);
            }
        }
    }
}

