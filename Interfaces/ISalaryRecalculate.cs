using HRMSAPI.DTO;
using Roomsy.DTOS.GenericsResponses;

namespace HRMSAPI.Interfaces
{
    public interface ISalaryRecalculate
    {
        Task<ExecuteAndReponse> SalaryRecalculate(SalaryRecalculateDto obj);
        Task<ExecuteAndReponse> SalaryRecalculateByMonth(SalaryRecalculateByMonthDto obj);
        Task<ExecuteAndReponse> SalaryRecalculateNew(SalaryRecalculateDto obj);
        Task<ExecuteAndReponse> SalaryRecalculateByMonthNew(SalaryRecalculateByMonthDto obj);
    }
}
