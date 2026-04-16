using HRMSAPI.Data;
using HRMSAPI.Interfaces;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Roomsy.DTOS.GenericsResponses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace HRMSAPI.Implementation
{
    public class EmployeeDeductionService : BaseService, IEmployeeDeductionService
    {
        private readonly HRMSContext _context;
        public EmployeeDeductionService(HRMSContext context) : base(context)
        {
            _context = context;
        }

        public async Task<ExecuteAndReponse> UploadEmployeeDeductionExcel(IFormFile file, string user)
        {
            if (file == null || file.Length == 0)
                return BuildExecuteErrorResponse("File is mandatory to serve.", HttpStatusCode.BadRequest);

            int inserted = 0, updated = 0, skipped = 0;
            var excelRows = new List<(string ECode, string STCode, int Month, string PF, string ESIC, string TDS, string PTax, string Loan, string CashShort, string DieselDeduction, string Penality, string Lwf)>();
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
                    string pf = row.Cell(colMap.GetValueOrDefault("PF", 0)).GetString();
                    string esic = row.Cell(colMap.GetValueOrDefault("ESIC", 0)).GetString();
                    string tds = row.Cell(colMap.GetValueOrDefault("TDS", 0)).GetString();
                    string ptax = row.Cell(colMap.GetValueOrDefault("PTAX", 0)).GetString();
                    string loan = row.Cell(colMap.GetValueOrDefault("LOAN", 0)).GetString();
                    string cashShort = row.Cell(colMap.GetValueOrDefault("CASHSHORT", 0)).GetString();
                    string dieselDeduction = row.Cell(colMap.GetValueOrDefault("DIESELDEDUCTION", 0)).GetString();
                    string penality = row.Cell(colMap.GetValueOrDefault("PENALITY", 0)).GetString();
                    string lwf = row.Cell(colMap.GetValueOrDefault("LWF", 0)).GetString();

                    if (string.IsNullOrWhiteSpace(ecode) || string.IsNullOrWhiteSpace(stcode) || string.IsNullOrWhiteSpace(monthStr) || !int.TryParse(monthStr, out int month))
                    {
                        skipped++;
                        continue;
                    }

                    excelRows.Add((ecode, stcode, month, pf, esic, tds, ptax, loan, cashShort, dieselDeduction, penality, lwf));
                }
            }

            //var keys = excelRows.Select(x => new { x.ECode, x.STCode, x.Month }).ToList();
            var keySet = new HashSet<string>(excelRows.Select(x => $"{x.ECode}|{x.STCode}|{x.Month}"));
            var existingRecords = await _context.tblEmployeeDeductions
                .Where(x => keySet.Contains(x.ECode + "|" + x.STCode + "|" + x.MONTH)
                    && (x.IsDeleted == null || x.IsDeleted == false)
                    && (x.IsActive == null || x.IsActive == true))
                .ToListAsync();
            //var existingRecords = await _context.tblEmployeeDeductions.AsQueryable()
            //    .Where(x => keys.Any(k => k.ECode == x.ECode && k.STCode == x.STCode && k.Month == x.MONTH) && (x.IsDeleted == null || x.IsDeleted == false) && (x.IsActive == null || x.IsActive == true))
            //    .ToListAsync();

            var existingDict = existingRecords.ToDictionary(
                x => $"{x.ECode}|{x.STCode}|{x.MONTH}",
                x => x
            );

            foreach (var row in excelRows)
            {
                string key = $"{row.ECode}|{row.STCode}|{row.Month}";
                if (existingDict.TryGetValue(key, out var existing))
                {
                    existing.PF = row.PF;
                    existing.ESIC = row.ESIC;
                    existing.TDS = row.TDS;
                    existing.PTax = row.PTax;
                    existing.Loan = row.Loan;
                    existing.CashShort = row.CashShort;
                    existing.DieselDeduction = row.DieselDeduction;
                    existing.Penality = row.Penality;
                    existing.Lwf = row.Lwf;
                    existing.UpdatedBy = user;
                    existing.UpdatedOn = DateTime.Now;
                    updated++;
                }
                else
                {
                    var newRec = new tblEmployeeDeduction
                    {
                        ECode = row.ECode,
                        STCode = row.STCode,
                        MONTH = row.Month.ToString(),
                        PF = row.PF,
                        ESIC = row.ESIC,
                        TDS = row.TDS,
                        PTax = row.PTax,
                        Loan = row.Loan,
                        CashShort = row.CashShort,
                        DieselDeduction = row.DieselDeduction,
                        Penality = row.Penality,
                        Lwf = row.Lwf,
                        CreatedBy = user,
                        CreatedOn = DateTime.Now,
                        IsActive = true,
                        IsDeleted = false
                    };
                    _context.tblEmployeeDeductions.Add(newRec);
                    inserted++;
                }
            }

            int ra = await _context.SaveChangesAsync();
            if (ra > 0)
            {
                return BuildExecuteSuccessResponse($"Inserted: {inserted}, Updated: {updated}, Skipped: {skipped}");
            }
            return BuildExecuteErrorResponse("Unable to Update , something went Wrong", HttpStatusCode.InternalServerError);
        }
    }
} 