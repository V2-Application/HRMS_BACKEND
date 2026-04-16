using HRMSAPI.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HRMSAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InsuranceEnrollmentController : ControllerBase
    {
        private readonly HRMSContext _context;

        public InsuranceEnrollmentController(HRMSContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> CreateInsuranceEnrollment([FromBody] InsuranceEnrollment model)
        {
            try
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC sp_InsertInsuranceEnrollment @NameofInsured, @EmployeeId, @Relation, @Gender, @DateOfBirth, @DateOfJoin, @GrossSalary",
                    new[]
                    {
                        new Microsoft.Data.SqlClient.SqlParameter("@NameofInsured", (object?)model.NameofInsured ?? DBNull.Value),
                        new Microsoft.Data.SqlClient.SqlParameter("@EmployeeId", (object?)model.EmployeeId ?? DBNull.Value),
                        new Microsoft.Data.SqlClient.SqlParameter("@Relation", (object?)model.Relation ?? DBNull.Value),
                        new Microsoft.Data.SqlClient.SqlParameter("@Gender", (object?)model.Gender ?? DBNull.Value),
                        new Microsoft.Data.SqlClient.SqlParameter("@DateOfBirth", (object?)model.DateOfBirth ?? DBNull.Value),
                        new Microsoft.Data.SqlClient.SqlParameter("@DateOfJoin", (object?)model.DateOfJoin ?? DBNull.Value),
                        new Microsoft.Data.SqlClient.SqlParameter("@GrossSalary", (object?)model.GrossSalary ?? DBNull.Value)
                    });

                return Ok("Insurance enrollment data inserted successfully");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }
}
