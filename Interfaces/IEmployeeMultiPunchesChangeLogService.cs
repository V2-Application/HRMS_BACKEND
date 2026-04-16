using HRMSAPI.DTO;

namespace HRMSAPI.Interfaces
{
    public interface IEmployeeMultiPunchesChangeLogService
    {
        Task<List<EmployeeMultiPunchesChangeLogDto>> GetEmployeeMultiPunchesChangeLogAsync(string ecode, string month);
    }
}

