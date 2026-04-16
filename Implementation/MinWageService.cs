using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data.Common;

namespace HRMSAPI.Implementation
{
    public class MinWageService : IMinWageService
    {
        private readonly HRMSContext _context;
        private readonly ILogger<MinWageService> _logger;

        public MinWageService(HRMSContext context, ILogger<MinWageService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<MinWageValidationResponseDto> ValidateSalaryAgainstMinWageAsync(string stCode, decimal salary)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(stCode))
                {
                    throw new ArgumentException("STCode cannot be null or empty.", nameof(stCode));
                }

                if (salary <= 0)
                {
                    throw new ArgumentException("Salary must be greater than 0.", nameof(salary));
                }

                // Call the SQL function using direct database command
                bool isAboveMinWage = false;
                await using var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();
                
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT dbo.ufn_IsSalaryAboveMinWage(@STCode, @Salary)";
                command.Parameters.Add(new SqlParameter("@STCode", stCode));
                command.Parameters.Add(new SqlParameter("@Salary", salary));
                
                var result = await command.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    isAboveMinWage = Convert.ToBoolean(result);
                }

                // Get the minimum wage for the location
                var location = await _context.tblLocations.AsNoTracking().AsQueryable()
                    .Where(l => l.STCode == stCode)
                    .FirstOrDefaultAsync();

                decimal? minWage = null;
                if (location?.StateIdForMinWage.HasValue == true)
                {
                    var stateMinWage = await _context.StateMasterWithMinWages.AsNoTracking().AsQueryable()
                        .Where(s => s.Id == location.StateIdForMinWage.Value)
                        .Select(s => (decimal?)s.MinWages)
                        .FirstOrDefaultAsync();
                    minWage = stateMinWage;
                }

                // If salary is below minimum wage, throw an exception
                if (!isAboveMinWage && minWage.HasValue)
                {
                    throw new ArgumentException($"Salary ({salary}) is below the minimum wage ({minWage.Value}) for STCode {stCode}. Minimum required salary is {minWage.Value}.");
                }

                // If minimum wage information not found, throw an exception
                if (!minWage.HasValue)
                {
                    throw new InvalidOperationException($"Minimum wage information not found for STCode {stCode}.");
                }

                var response = new MinWageValidationResponseDto
                {
                    STCode = stCode,
                    Salary = salary,
                    IsSalaryAboveMinWage = isAboveMinWage,
                    MinWage = minWage.Value,
                    Message = $"Salary ({salary}) is above the minimum wage ({minWage.Value}) for STCode {stCode}."
                };

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating salary against minimum wage for STCode: {STCode}, Salary: {Salary}", stCode, salary);
                throw new InvalidOperationException($"Failed to validate salary against minimum wage: {ex.Message}", ex);
            }
        }

        public async Task<List<StateMinWageDto>> GetStateMinWagesListAsync()
        {
            try
            {
                var stateMinWages = await _context.StateMasterWithMinWages
                    .AsNoTracking()
                    .OrderBy(s => s.StateName)
                    .Select(s => new StateMinWageDto
                    {
                        Id = s.Id,
                        StateName = s.StateName ?? string.Empty,
                        MinWages = s.MinWages
                    })
                    .ToListAsync();

                return stateMinWages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving state minimum wages list");
                throw new InvalidOperationException($"Failed to retrieve state minimum wages list: {ex.Message}", ex);
            }
        }

        public async Task<StateMinWageDto> UpdateMinWageAsync(int id, int minWages)
        {
            try
            {
                if (id <= 0)
                {
                    throw new ArgumentException("Id must be greater than 0.", nameof(id));
                }

                if (minWages < 0)
                {
                    throw new ArgumentException("MinWages must be greater than or equal to 0.", nameof(minWages));
                }

                var stateMinWage = await _context.StateMasterWithMinWages
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (stateMinWage == null)
                {
                    throw new InvalidOperationException($"State with Id {id} not found.");
                }

                // Update the min wage
                stateMinWage.MinWages = minWages;

                // Save changes
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully updated minimum wage for State: {StateName} (Id: {Id}) to {MinWages}", 
                    stateMinWage.StateName, id, minWages);

                return new StateMinWageDto
                {
                    Id = stateMinWage.Id,
                    StateName = stateMinWage.StateName ?? string.Empty,
                    MinWages = stateMinWage.MinWages
                };
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error updating minimum wage for Id: {Id}", id);
                throw new InvalidOperationException($"Failed to update minimum wage due to database error: {ex.InnerException?.Message ?? ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating minimum wage for Id: {Id}", id);
                throw new InvalidOperationException($"Failed to update minimum wage: {ex.Message}", ex);
            }
        }
    }
}

