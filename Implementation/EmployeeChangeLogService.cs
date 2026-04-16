using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;

namespace HRMSAPI.Implementation
{
    public class EmployeeChangeLogService : IEmployeeChangeLogService
    {
        private readonly HRMSContext _context;
        private readonly ILogger<EmployeeChangeLogService> _logger;

        public EmployeeChangeLogService(HRMSContext context, ILogger<EmployeeChangeLogService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<EmployeeChangeLogDto>> GetEmployeeChangeLogAsync(string ecode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ecode))
                {
                    throw new ArgumentException("Ecode cannot be null or empty.", nameof(ecode));
                }

                var allChangeLogs = new List<EmployeeChangeLogDto>();

                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "[HRMS].[dbo].[usp_GetEmployeeChangeLog]";
                        command.CommandType = CommandType.StoredProcedure;

                        // Only pass @Ecode parameter - no pagination
                        command.Parameters.Add(new SqlParameter("@Ecode", SqlDbType.NVarChar, 50) { Value = ecode });

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var changeLog = new EmployeeChangeLogDto
                                {
                                    Ecode = reader.IsDBNull(reader.GetOrdinal("Ecode"))
                                        ? string.Empty
                                        : reader.GetString(reader.GetOrdinal("Ecode")),
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
                                    ChangedOn = reader.IsDBNull(reader.GetOrdinal("ChangedOn"))
                                        ? DateTime.MinValue
                                        : reader.GetDateTime(reader.GetOrdinal("ChangedOn"))
                                };
                                allChangeLogs.Add(changeLog);
                            }
                        }
                    }
                }

                return allChangeLogs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching employee change log for Ecode: {Ecode}", ecode);
                throw new ApplicationException($"An error occurred while fetching employee change log: {ex.Message}", ex);
            }
        }
    }
}

