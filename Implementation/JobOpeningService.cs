namespace HRMSAPI.Implementation
{
    using HRMSAPI.Data;
    using HRMSAPI.Interfaces;
    using Microsoft.Data.SqlClient;
    using Microsoft.EntityFrameworkCore;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading.Tasks;
    using Roomsy.DTOS.GenericsResponses;
    using System.Net;

    public class JobOpeningService : BaseService, IJobOpeningService
    {
        private readonly HRMSContext _context;

        public JobOpeningService(HRMSContext context) : base(context)
        {
            _context = context;
        }

        //public async Task<List<JobOpeningDto>> GetJobOpeningsAsync(string? searchText)
        //{
        //    var query = _context.tblStoreBudgets
        //        .Include(sb => sb.Designation)
        //        .Include(sb => sb.StoreLocations)
        //        .Select(sb => new JobOpeningDto
        //        {
        //            StoreBudgetId = sb.StoreBudgetId,
        //            DesignationName = sb.Designation.DesignationName,
        //            DesignationId = sb.DesignationId,
        //            LocationId = (int)sb.StoreLocations.LocationId,
        //            LocationName = _context.tblLocations
        //                .Where(a => a.LocationId == sb.StoreLocations.LocationId)
        //                .Select(a => a.LocationName)
        //                .FirstOrDefault(),
        //            BudgetManpowerCount = sb.BudgetManpowerCount,
        //            BudgetAmount = sb.BudgetAmount,
        //            KeyResponsibility = sb.Designation.JDs.FirstOrDefault().KeyResponsibility,
        //            KeySkill = sb.Designation.JDs.FirstOrDefault().KeySkills
        //        });

        //    if (!string.IsNullOrWhiteSpace(searchText))
        //    {
        //        searchText = searchText.ToLower();

        //    }

        //    return await query.ToListAsync();
        //}
        public async Task<FetchAndResponse> GetJobOpeningsAsync(string? searchText)
        {
            var result = new List<JobOpeningDto>();

            try
            {
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "sp_GetJobOpenings";
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add(new SqlParameter("@SearchText",
                            string.IsNullOrWhiteSpace(searchText) ? DBNull.Value : (object)searchText));

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                result.Add(new JobOpeningDto
                                {
                                    StoreBudgetId = reader.IsDBNull(reader.GetOrdinal("StoreBudgetId"))
                                        ? (int?)null : reader.GetInt32(reader.GetOrdinal("StoreBudgetId")),

                                    DesignationName = reader.IsDBNull(reader.GetOrdinal("DesignationName"))
                                        ? null : reader.GetString(reader.GetOrdinal("DesignationName")),

                                    DesignationId = reader.IsDBNull(reader.GetOrdinal("DesignationId"))
                                        ? (int?)null : reader.GetInt32(reader.GetOrdinal("DesignationId")),

                                    LocationId = reader.IsDBNull(reader.GetOrdinal("LocationId"))
                                        ? (int?)null : reader.GetInt32(reader.GetOrdinal("LocationId")),

                                    LocationName = reader.IsDBNull(reader.GetOrdinal("LocationName"))
                                        ? null : reader.GetString(reader.GetOrdinal("LocationName")),

                                    BudgetManpowerCount = reader.IsDBNull(reader.GetOrdinal("BudgetManpowerCount"))
                                        ? (int?)null : reader.GetInt32(reader.GetOrdinal("BudgetManpowerCount")),

                                    BudgetAmount = reader.IsDBNull(reader.GetOrdinal("BudgetAmount"))
                                        ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("BudgetAmount")),

                                    KeyResponsibility = reader.IsDBNull(reader.GetOrdinal("KeyResponsibility"))
                                        ? null : reader.GetString(reader.GetOrdinal("KeyResponsibility")),

                                    KeySkill = reader.IsDBNull(reader.GetOrdinal("KeySkills"))
                                        ? null : reader.GetString(reader.GetOrdinal("KeySkills"))
                                });
                            }
                        }
                    }
                }

                if (result == null || result.Count == 0)
                {
                    return BuildFetchErrorResponse("No job openings found", HttpStatusCode.NotFound);
                }

                return BuildFetchSuccessResponse("Job openings fetched successfully", result);
            }
            catch (SqlException ex)
            {
                Console.Error.WriteLine($"SQL Error: {ex.Message}");
                return BuildFetchErrorResponse($"Database error: {ex.Message}", HttpStatusCode.InternalServerError);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unexpected Error: {ex.Message}");
                return BuildFetchErrorResponse($"Unexpected error: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<FetchAndResponse> GetProcOpeningsAsync()
        {
            var result = new List<ProcOpeningsDto>();

            try
            {
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "proc_Openings";
                        command.CommandType = CommandType.StoredProcedure;

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                result.Add(new ProcOpeningsDto
                                {
                                    LOC_CODE = reader.IsDBNull(reader.GetOrdinal("LOC_CODE"))
                                        ? null : reader.GetString(reader.GetOrdinal("LOC_CODE")),

                                    Location = reader.IsDBNull(reader.GetOrdinal("Location"))
                                        ? null : reader.GetString(reader.GetOrdinal("Location")),

                                    DepartmentId = reader.IsDBNull(reader.GetOrdinal("DepartmentId"))
                                        ? (int?)null : reader.GetInt32(reader.GetOrdinal("DepartmentId")),

                                    DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName"))
                                        ? null : reader.GetString(reader.GetOrdinal("DepartmentName")),

                                    DesignationId = reader.IsDBNull(reader.GetOrdinal("DesignationId"))
                                        ? (int?)null : reader.GetInt32(reader.GetOrdinal("DesignationId")),

                                    DesignationName = reader.IsDBNull(reader.GetOrdinal("DesignationName"))
                                        ? null : reader.GetString(reader.GetOrdinal("DesignationName")),

                                    SeatBudget = reader.IsDBNull(reader.GetOrdinal("SeatBudget"))
                                        ? (int?)null : reader.GetInt32(reader.GetOrdinal("SeatBudget")),

                                    EmpCount = reader.IsDBNull(reader.GetOrdinal("EmpCount"))
                                        ? 0 : reader.GetInt32(reader.GetOrdinal("EmpCount")),

                                    Vacancy = reader.IsDBNull(reader.GetOrdinal("Vacancy"))
                                        ? (int?)null : reader.GetInt32(reader.GetOrdinal("Vacancy")),

                                    KeyResponsibility = reader.IsDBNull(reader.GetOrdinal("KeyResponsibility"))
                                        ? null : reader.GetString(reader.GetOrdinal("KeyResponsibility")),

                                    KeySkills = reader.IsDBNull(reader.GetOrdinal("KeySkills"))
                                        ? null : reader.GetString(reader.GetOrdinal("KeySkills"))
                                });
                            }
                        }
                    }
                }

                if (result == null || result.Count == 0)
                {
                    return BuildFetchErrorResponse("No openings found", HttpStatusCode.NotFound);
                }

                return BuildFetchSuccessResponse("Openings fetched successfully", result);
            }
            catch (SqlException ex)
            {
                Console.Error.WriteLine($"SQL Error: {ex.Message}");
                return BuildFetchErrorResponse($"Database error: {ex.Message}", HttpStatusCode.InternalServerError);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unexpected Error: {ex.Message}");
                return BuildFetchErrorResponse($"Unexpected error: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

    }
}
