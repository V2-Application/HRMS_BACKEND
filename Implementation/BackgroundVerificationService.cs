using DocumentFormat.OpenXml.Wordprocessing;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using HRMSAPI.Models.Auth;
using Microsoft.EntityFrameworkCore;
using System.Data;
using static Emgu.CV.Stitching.Stitcher;
namespace HRMSAPI.Implementation
{
    public class BackgroundVerificationService : IBackgroundVerificationService
    {
        public readonly HRMSContext _context;
        public BackgroundVerificationService(HRMSContext context)
        {
            _context = context;
        }

        public async Task<List<BgvListDTO>> GetBgvList(int status = 4, int pageSize = 10, int pageNumber = 1)
        {
            await using var conn = _context.Database.GetDbConnection();

            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            var cmd = conn.CreateCommand();

            cmd.CommandText = "dbo.sp_GetBgvCandidates";
            cmd.CommandType = CommandType.StoredProcedure;

            // Set up parameters for the stored procedure
            var pageSizeParam = cmd.CreateParameter();
            pageSizeParam.ParameterName = "@PageSize";
            pageSizeParam.Value = pageSize;
            cmd.Parameters.Add(pageSizeParam);

            var pageNumberParam = cmd.CreateParameter();
            pageNumberParam.ParameterName = "@PageNumber";
            pageNumberParam.Value = pageNumber;
            cmd.Parameters.Add(pageNumberParam);

            var statusParam = cmd.CreateParameter();
            statusParam.ParameterName = "@Status";
            statusParam.Value = status;
            cmd.Parameters.Add(statusParam);

            // Execute the command
            await using var reader = await cmd.ExecuteReaderAsync();

            // First, read the data result set (first result set)
            var result = new List<BgvListDTO>();
            if (await reader.ReadAsync()) // This reads the first row of data
            {
                // Reading the paginated data
                do
                {
                    var bgvListDto = new BgvListDTO
                    {
                        CandidateId = reader.GetInt64(reader.GetOrdinal("Id")),
                        Name = reader["Name"].ToString(),
                        Email = reader["EMAIL ADDRESS"].ToString(),
                        DOB = reader.GetDateTime(reader.GetOrdinal("DOB")),
                        Designation = reader["DESIGNATION"].ToString(),
                        Department = reader["DEPARTMENT"].ToString(),
                        Mobile = reader["MOBILE"].ToString(),
                        Store = reader["StoreId"].ToString(),
                        Ecode = reader["Ecode"].ToString(),
                        BgvId = reader["bgvId"] == DBNull.Value ? (long?)null : reader.GetInt64(reader.GetOrdinal("bgvId")),
                        AuditorId = reader["AuditorId"] == DBNull.Value ? (long?)null : reader.GetInt64(reader.GetOrdinal("AuditorId"))
                    };

                    result.Add(bgvListDto);
                }
                while (await reader.ReadAsync());  // Continue reading rows in the first result set
            }

            // Now, move to the second result set (Success Message)
            if (await reader.NextResultAsync()) // Move to the second result set (the success message)
            {
                if (await reader.ReadAsync())  // Read the success message from the second result set
                {
                    var success = Convert.ToBoolean(reader["Success"]);
                    var message = reader["Message"]?.ToString();

                    if (!success)
                    {
                        // If the success flag is false, throw an exception with the message
                        throw new Exception(message);
                    }
                }
            }

            return result;
        }

        public async Task<List<BgvListDTO>> GetBgvListAudit(long auditorId, int status = 4, int pageSize = 10, int pageNumber = 1)
        {
            await using var conn = _context.Database.GetDbConnection();

            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            var cmd = conn.CreateCommand();

            cmd.CommandText = "dbo.sp_GetBgvCandidatesAudit";
            cmd.CommandType = CommandType.StoredProcedure;

            // Set up parameters for the stored procedure
            var pageSizeParam = cmd.CreateParameter();
            pageSizeParam.ParameterName = "@PageSize";
            pageSizeParam.Value = pageSize;
            cmd.Parameters.Add(pageSizeParam);

            var pageNumberParam = cmd.CreateParameter();
            pageNumberParam.ParameterName = "@PageNumber";
            pageNumberParam.Value = pageNumber;
            cmd.Parameters.Add(pageNumberParam);

            var auditorIdParam = cmd.CreateParameter();
            auditorIdParam.ParameterName = "@AuditorId";
            auditorIdParam.Value = auditorId;
            cmd.Parameters.Add(auditorIdParam);

            var statusParam = cmd.CreateParameter();
            statusParam.ParameterName = "@Status";
            statusParam.Value = status;
            cmd.Parameters.Add(statusParam);

            // Execute the command
            await using var reader = await cmd.ExecuteReaderAsync();

            // First, read the data result set (first result set)
            var result = new List<BgvListDTO>();
            if (await reader.ReadAsync()) // This reads the first row of data
            {
                // Reading the paginated data
                do
                {
                    var bgvListDto = new BgvListDTO
                    {
                        CandidateId = reader.GetInt64(reader.GetOrdinal("Id")),
                        Name = reader["Name"].ToString(),
                        Email = reader["EMAIL ADDRESS"].ToString(),
                        DOB = reader.GetDateTime(reader.GetOrdinal("DOB")),
                        Designation = reader["DESIGNATION"].ToString(),
                        Department = reader["DEPARTMENT"].ToString(),
                        Mobile = reader["MOBILE"].ToString(),
                        Store = reader["StoreId"].ToString(),
                        BgvId = reader["bgvId"] == DBNull.Value
                        ? (long?)null
                        : reader.GetInt64(reader.GetOrdinal("bgvId"))
                    };

                    result.Add(bgvListDto);
                }
                while (await reader.ReadAsync());  // Continue reading rows in the first result set
            }

            // Now, move to the second result set (Success Message)
            if (await reader.NextResultAsync()) // Move to the second result set (the success message)
            {
                if (await reader.ReadAsync())  // Read the success message from the second result set
                {
                    var success = Convert.ToBoolean(reader["Success"]);
                    var message = reader["Message"]?.ToString();

                    if (!success)
                    {
                        // If the success flag is false, throw an exception with the message
                        throw new Exception(message);
                    }
                }
            }

            return result;
        }

        public async Task<List<AuditEmployeesDTO>> GetAuditEmployees()
        {
            await using var conn = _context.Database.GetDbConnection();

            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            var cmd = conn.CreateCommand();

            cmd.CommandText = "dbo.sp_GetEmployeesWithRole";
            cmd.CommandType = CommandType.StoredProcedure;

            await using var reader = await cmd.ExecuteReaderAsync();

            var result = new List<AuditEmployeesDTO>();
            if (await reader.ReadAsync())
            {
                do
                {
                    var auditEmployee = new AuditEmployeesDTO
                    {
                        Id = reader.GetInt64(reader.GetOrdinal("Id")),
                        Name = reader.GetString(reader.GetOrdinal("Name"))
                    };

                    result.Add(auditEmployee);
                }
                while (await reader.ReadAsync());
            }

            if (await reader.NextResultAsync())
            {
                if (await reader.ReadAsync())
                {
                    var success = Convert.ToBoolean(reader["Success"]);
                    var message = reader["Message"]?.ToString();

                    if (!success)
                    {
                        throw new Exception(message);
                    }
                }
            }

            return result;
        }

        public async Task<Response> AssignAuditor(AssignAuditorDTO request)
        {
            await using var conn = _context.Database.GetDbConnection();

            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            var cmd = conn.CreateCommand();

            cmd.CommandText = "dbo.sp_AssignAuditorToBGV";
            cmd.CommandType = CommandType.StoredProcedure;

            // Set up parameters for the stored procedure
            var bgvidParam = cmd.CreateParameter();
            bgvidParam.ParameterName = "@CandidateId";
            bgvidParam.Value = request.CandidateId;
            cmd.Parameters.Add(bgvidParam);

            var auditorParam = cmd.CreateParameter();
            auditorParam.ParameterName = "@AuditorId";
            auditorParam.Value = request.AuditorId;
            cmd.Parameters.Add(auditorParam);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var success = Convert.ToBoolean(reader["Success"]);
                var message = reader["Message"]?.ToString();

                long? insertedId = reader["InsertedId"] == DBNull.Value
                 ? null
                 : Convert.ToInt64(reader["InsertedId"]);

                return new Response
                {
                    Status = success,
                    Message = message,
                    Data = new { BgvId = insertedId }
                };
            }

            return new Response
            {
                Status = false,
                Message = "Unexpected error occurred or no results returned."
            };
        }


        public async Task<Response> AuditorFeedback(AuditorBgvFeedbackDTO request)
        {
            await using var conn = _context.Database.GetDbConnection();

            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            var cmd = conn.CreateCommand();

            cmd.CommandText = "dbo.sp_AuditorBgvFeedback";
            cmd.CommandType = CommandType.StoredProcedure;

            // Set up parameters for the stored procedure
            var bgvidParam = cmd.CreateParameter();
            bgvidParam.ParameterName = "@BgvId";
            bgvidParam.Value = request.BgvId;
            cmd.Parameters.Add(bgvidParam);

            var statusParam = cmd.CreateParameter();
            statusParam.ParameterName = "@Status";
            statusParam.Value = request.Status;
            cmd.Parameters.Add(statusParam);

            var remarksParam = cmd.CreateParameter();
            remarksParam.ParameterName = "@Remarks";
            remarksParam.Value = request.Remarks;
            cmd.Parameters.Add(remarksParam);

            var auditDateParam = cmd.CreateParameter();
            auditDateParam.ParameterName = "@AuditDate";
            auditDateParam.Value = request.AuditDate;
            cmd.Parameters.Add(auditDateParam);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var success = Convert.ToBoolean(reader["Success"]);
                var message = reader["Message"]?.ToString();

                return new Response
                {
                    Status = success,
                    Message = message,
                };
            }

            return new Response
            {
                Status = false,
                Message = "Unexpected error occurred or no results returned."
            };
        }

        public async Task<BgvCandidateDetailDTO> GetBgvCandidateDetails(long id)
        {
            await using var conn = _context.Database.GetDbConnection();

            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            var cmd = conn.CreateCommand();

            cmd.CommandText = "dbo.sp_GetBgvCandidateDetail";
            cmd.CommandType = CommandType.StoredProcedure;

            // Set up parameters for the stored procedure
            var bgvidParam = cmd.CreateParameter();
            bgvidParam.ParameterName = "@BgvId";
            bgvidParam.Value = id;
            cmd.Parameters.Add(bgvidParam);

            await using var reader = await cmd.ExecuteReaderAsync();

            var result = new BgvCandidateDetailDTO();

            if (await reader.ReadAsync())
            {
                var _detail = new BgvCandidateDetailDTO
                {
                    CandidateName = reader.GetString(reader.GetOrdinal("Name")),
                    CandidateDocs = reader.IsDBNull(reader.GetOrdinal("CandidateDocs")) ? null : reader.GetString(reader.GetOrdinal("CandidateDocs")),
                    CandidateExperience = reader.IsDBNull(reader.GetOrdinal("CandidateExperience")) ? null : reader.GetString(reader.GetOrdinal("CandidateExperience")),
                    Designation = reader.IsDBNull(reader.GetOrdinal("DesignationName")) ? string.Empty : reader.GetString(reader.GetOrdinal("DesignationName")),
                    JoiningDate = reader.IsDBNull(reader.GetOrdinal("JoiningDate")) ? null : reader.GetDateTime(reader.GetOrdinal("JoiningDate")),
                    CTC = reader.IsDBNull(reader.GetOrdinal("CTC")) ? null : reader.GetDecimal(reader.GetOrdinal("CTC"))
                };

                result = _detail;
            }

            if (await reader.NextResultAsync())
            {
                if (await reader.ReadAsync())
                {
                    var success = Convert.ToBoolean(reader["Success"]);
                    var message = reader["Message"]?.ToString();

                    if (!success)
                    {
                        throw new Exception(message);
                    }
                }
            }

            return result;
        }
    }
}
