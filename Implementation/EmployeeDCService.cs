using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HRMSAPI.Implementation
{
    public class EmployeeDCService : IDCEmployeeService
    {
        private readonly HRMSContext _context;

        public EmployeeDCService(HRMSContext context)
        {
            _context = context;
        }

        public async Task<List<DCEmployeeDTO>> DCLoginAsync(DCLoginRequest request)
        {
            var employee = await _context.TBL_DCEMPLOYEEs
                .Where(e => e.RPT_PERSON_CARD == request.RptPersonCard && e.PasswordHash == request.Password)
                .FirstOrDefaultAsync();

            if (employee == null)
            {
                return new List<DCEmployeeDTO>(); // Return empty list if credentials don't match
            }

            var matchingEmployees = await _context.TBL_DCEMPLOYEEs
                .Where(e => e.RPT_PERSON_CARD == request.RptPersonCard)
                .Select(e => new DCEmployeeDTO
                {
                    ECode = e.E_CODE ?? "NA",
                    EmpName = e.EMP_NAME ?? "NA",
                    EmailId = e.EMAIL_ID ?? "NA",
                    IsContract = (bool)(e.IS_CONTRACT ?? false),
                    Designation = e.DESIGNATION ?? "NA",
                    Department = e.DEPARTMENT ?? "NA",
                    SubDepartment = e.SUB_DEPARTMENT ?? "NA",
                    ReportingManagerName = e.REPORTING_MANAGER_NAME ?? "NA",
                    RptPersonCard = e.RPT_PERSON_CARD ?? "NA",
                    ActInAct = e.ACT_IN_ACT ?? "NA"
                })
                .ToListAsync();

            return matchingEmployees;
        }
    }
}