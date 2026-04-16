using HRMSAPI.Data;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Roomsy.DTOS.GenericsResponses;
using System.Net;
using HRMSAPI.Interfaces;

namespace HRMSAPI.Implementation
{
    public class EmployeeSalaryAddOnsService : BaseService, IEmployeeSalaryAddOnsService
    {
        private readonly HRMSContext _context;
        public EmployeeSalaryAddOnsService(HRMSContext context) : base(context)
        {
            _context = context;
        }

        public async Task<ExecuteAndReponse> UploadSalaryAddOnsExcel(IFormFile file, string user)
        {
            if (file == null || file.Length == 0)
                return BuildExecuteErrorResponse("File is mandatory to serve.",HttpStatusCode.BadRequest);

            int inserted = 0, updated = 0, skipped = 0;

            // Step 1: Read Excel and collect all rows
            var excelRows = new List<(string ECode, string STCode, int Month, string Arears, string Incentive, string Reimbursement)>();
            using (var stream = file.OpenReadStream())
            using (var workbook = new XLWorkbook(stream))
            {
                var worksheet = workbook.Worksheet(1);
                var headerRow = worksheet.Row(1);
                var colMap = headerRow.Cells().ToDictionary(
                    c => c.GetString().Trim().ToUpper(),
                    c => c.Address.ColumnNumber
                );

                foreach (var row in worksheet.RowsUsed().Skip(1))
                {
                    string ecode = row.Cell(colMap.GetValueOrDefault("ECODE", 0)).GetString().Trim();
                    string stcode = row.Cell(colMap.GetValueOrDefault("STCODE", 0)).GetString().Trim();
                    string monthStr = row.Cell(colMap.GetValueOrDefault("MONTH", 0)).GetString().Trim();
                    string arears = row.Cell(colMap.GetValueOrDefault("AREARS", 0)).GetString();
                    string incentive = row.Cell(colMap.GetValueOrDefault("INCENTIVE", 0)).GetString();
                    string reimbursement = row.Cell(colMap.GetValueOrDefault("REIMBURSEMENT", 0)).GetString();

                    if (string.IsNullOrWhiteSpace(ecode) || string.IsNullOrWhiteSpace(stcode) || string.IsNullOrWhiteSpace(monthStr) || !int.TryParse(monthStr, out int month))
                    {
                        skipped++;
                        continue;
                    }

                    excelRows.Add((ecode, stcode, month, arears, incentive, reimbursement));
                }
            }

            // Step 2: Query all existing records in one go
            var keySet = new HashSet<string>(excelRows.Select(x => $"{x.ECode}|{x.STCode}|{x.Month}"));
            var existingRecords = await _context.tblEmployeeSalaryAddons
                .Where(x => keySet.Contains(x.ECode + "|" + x.STCode + "|" + x.MONTH)
                    && (x.IsDeleted == null || x.IsDeleted == false)
                    && (x.IsActive == null || x.IsActive == true))
                .ToListAsync();

            // Step 3: Build a dictionary for fast lookup
            var existingDict = existingRecords.ToDictionary(
                x => $"{x.ECode}|{x.STCode}|{x.MONTH}",
                x => x
            );

            // Step 4: Process rows in memory
            foreach (var row in excelRows)
            {
                string key = $"{row.ECode}|{row.STCode}|{row.Month}";
                if (existingDict.TryGetValue(key, out var existing))
                {
                    existing.Arears = row.Arears;
                    existing.Incentive = row.Incentive;
                    existing.Reimbursement = row.Reimbursement;
                    existing.UpdatedBy = user;
                    existing.UpdatedOn = DateTime.Now;
                    updated++;
                }
                else
                {
                    var newRec = new tblEmployeeSalaryAddon
                    {
                        ECode = row.ECode,
                        STCode = row.STCode,
                        MONTH = row.Month,
                        Arears = row.Arears,
                        Incentive = row.Incentive,
                        Reimbursement = row.Reimbursement,
                        CreatedBy = user,
                        CreatedOn = DateTime.Now,
                        IsActive = true,
                        IsDeleted = false
                    };
                    _context.tblEmployeeSalaryAddons.Add(newRec);
                    inserted++;
                }
            }

            int ra = await _context.SaveChangesAsync();

            if (ra > 0) {
                return BuildExecuteSuccessResponse($"Inserted: {inserted}, Updated: {updated}, Skipped: {skipped}");
            }
            return BuildExecuteErrorResponse("Unable to Update , something went Wrong",HttpStatusCode.InternalServerError);
        }
    }
}
