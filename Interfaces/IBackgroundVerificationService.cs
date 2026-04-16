using HRMSAPI.DTO;
using HRMSAPI.Models.Auth;

namespace HRMSAPI.Interfaces
{
    public interface IBackgroundVerificationService
    {
        // Method signature only, no body
        Task<List<BgvListDTO>> GetBgvList(int status = 4, int pageSize = 10, int pageNumber = 1);

        Task<List<BgvListDTO>> GetBgvListAudit(long auditorId, int status = 4, int pageSize = 10, int pageNumber = 1);

        Task<List<AuditEmployeesDTO>> GetAuditEmployees();

        Task<Response> AssignAuditor(AssignAuditorDTO request);
        Task<Response> AuditorFeedback(AuditorBgvFeedbackDTO request);
        Task<BgvCandidateDetailDTO> GetBgvCandidateDetails(long id);
    }
}