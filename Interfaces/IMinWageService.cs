using HRMSAPI.DTO;

namespace HRMSAPI.Interfaces
{
    public interface IMinWageService
    {
        Task<MinWageValidationResponseDto> ValidateSalaryAgainstMinWageAsync(string stCode, decimal salary);
        Task<List<StateMinWageDto>> GetStateMinWagesListAsync();
        Task<StateMinWageDto> UpdateMinWageAsync(int id, int minWages);
    }
}

