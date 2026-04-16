using HRMSAPI.DTO;

namespace HRMSAPI.Interfaces
{
    public interface IEmployeeChangeLogService
    {
        /// <summary>
        /// Gets all employee change log records for the given ecode.
        /// </summary>
        Task<List<EmployeeChangeLogDto>> GetEmployeeChangeLogAsync(string ecode);
    }
}

