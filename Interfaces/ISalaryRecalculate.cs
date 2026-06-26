using HRMSAPI.DTO;
using Roomsy.DTOS.GenericsResponses;
using System.Threading;

namespace HRMSAPI.Interfaces
{
    public interface ISalaryRecalculate
    {
        Task<ExecuteAndReponse> SalaryRecalculate(SalaryRecalculateDto obj);
        Task<ExecuteAndReponse> SalaryRecalculateByMonth(SalaryRecalculateByMonthDto obj);
        Task<ExecuteAndReponse> SalaryRecalculateNew(SalaryRecalculateDto obj, CancellationToken cancellationToken = default);
        Task<ExecuteAndReponse> SalaryRecalculateByMonthNew(SalaryRecalculateByMonthDto obj, CancellationToken cancellationToken = default);
    }
}
