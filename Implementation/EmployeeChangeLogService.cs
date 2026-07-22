using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;

namespace HRMSAPI.Implementation
{
    public class EmployeeChangeLogService : IEmployeeChangeLogService
    {
        private readonly HRMSContext _context;
        private readonly IConfiguration _config;
        private readonly ILogger<EmployeeChangeLogService> _logger;

        public EmployeeChangeLogService(HRMSContext context, IConfiguration config, ILogger<EmployeeChangeLogService> logger)
        {
            _context = context;
            _config = config;
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

                // Use a DEDICATED connection from the connection string — NOT the shared
                // DbContext connection (_context.Database.GetDbConnection()). The controller's
                // [RequirePageAccess] filter runs first and uses the same scoped DbContext for
                // its RBAC query; reusing/opening that connection here can throw
                // "The connection was already open" (or dispose the context's connection),
                // surfacing as an intermittent 500. A private connection avoids that entirely.
                var connString = _context.Database.GetConnectionString()
                                 ?? _config.GetConnectionString("DefaultConnection");

                using (var connection = new SqlConnection(connString))
                {
                    await connection.OpenAsync();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "[dbo].[usp_GetEmployeeChangeLog]";
                        command.CommandType = CommandType.StoredProcedure;
                        command.CommandTimeout = 120;
                        command.Parameters.Add(new SqlParameter("@Ecode", SqlDbType.NVarChar, 50) { Value = ecode });

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            // Resolve ordinals once, tolerating a column being absent on a given
                            // environment (returns -1 -> treated as empty) so a schema difference
                            // can never crash the whole request.
                            int oEcode = SafeOrdinal(reader, "Ecode");
                            int oColumn = SafeOrdinal(reader, "ColumnName");
                            int oOld = SafeOrdinal(reader, "OldValue");
                            int oNew = SafeOrdinal(reader, "NewValue");
                            int oVer = SafeOrdinal(reader, "VersionLabel");
                            int oBy = SafeOrdinal(reader, "ChangedBy");
                            int oOn = SafeOrdinal(reader, "ChangedOn");
                            int oIp = SafeOrdinal(reader, "ChangedIp");

                            while (await reader.ReadAsync())
                            {
                                allChangeLogs.Add(new EmployeeChangeLogDto
                                {
                                    Ecode = GetStr(reader, oEcode),
                                    ColumnName = GetStr(reader, oColumn),
                                    OldValue = GetStr(reader, oOld),
                                    NewValue = GetStr(reader, oNew),
                                    VersionLabel = GetStr(reader, oVer),
                                    ChangedBy = GetStr(reader, oBy),
                                    ChangedOn = (oOn < 0 || reader.IsDBNull(oOn)) ? DateTime.MinValue : reader.GetDateTime(oOn),
                                    ChangedIp = GetStr(reader, oIp)
                                });
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

        private static int SafeOrdinal(IDataRecord reader, string name)
        {
            try { return reader.GetOrdinal(name); }
            catch (IndexOutOfRangeException) { return -1; }
        }

        // Read any column as a string regardless of its SQL type (handles sql_variant / non-nvarchar
        // columns that GetString would otherwise throw on). Returns "" for missing/null.
        private static string GetStr(IDataRecord reader, int ordinal)
        {
            if (ordinal < 0 || reader.IsDBNull(ordinal)) return string.Empty;
            var v = reader.GetValue(ordinal);
            return v?.ToString() ?? string.Empty;
        }
    }
}
