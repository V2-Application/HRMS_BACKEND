using HRMSAPI.Data;
using HRMSAPI.Interfaces;
using HRMSAPI.Models.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net;

namespace HRMSAPI.Implementation
{
    public class FnfDetailsService : IFnfDetailsService
    {
        private readonly HRMSContext _context;
        private readonly ILogger<FnfDetailsService> _logger;

        public FnfDetailsService(HRMSContext context, ILogger<FnfDetailsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Response> GetFnfDetailsByEcodeAsync(string ecode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ecode))
                {
                    return new Response
                    {
                        Status = false,
                        Message = "Ecode is required.",
                        StatusCode = HttpStatusCode.BadRequest,
                        Data = null
                    };
                }

                // Call stored procedure using EF Core Database.SqlQueryRaw
                //var result = await _context.GetProcedures().sp_FNF_GetFnfDetailsByEcodeAsync(ecode);
                //var result = await _context.Database
                //    .SqlQueryRaw<sp_FNF_GetFnfDetailsByEcodeResult>(
                //        "EXEC [dbo].[sp_FNF_GetFnfDetailsByEcode] @Ecode = {0}",
                //        ecode)
                //    .ToListAsync();

                var result = await _context.Database
                   .SqlQueryRaw<sp_FNF_GetFnfDetailsByEcodeByGautamResult>(
                       "EXEC [dbo].[sp_FNF_GetFnfDetailsByEcodeByGautam] @Ecode = {0}",
                       ecode)
                   .ToListAsync();

                if (result == null || result.Count == 0)
                {
                    return new Response
                    {
                        Status = false,
                        Message = "No FNF details found for the given Ecode.",
                        StatusCode = HttpStatusCode.NotFound,
                        Data = null
                    };
                }

                // Return the first result (stored procedure returns TOP 1)
                var fnfDetails = result.FirstOrDefault();

                _logger.LogInformation("FNF details fetched successfully for Ecode: {Ecode}", ecode);

                return new Response
                {
                    Status = true,
                    Message = "FNF details fetched successfully.",
                    StatusCode = HttpStatusCode.OK,
                    Data = fnfDetails
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching FNF details for Ecode: {Ecode}", ecode);
                return new Response
                {
                    Status = false,
                    Message = $"An error occurred while fetching FNF details: {ex.Message}",
                    StatusCode = HttpStatusCode.InternalServerError,
                    Data = null
                };
            }
        }
    }
}



