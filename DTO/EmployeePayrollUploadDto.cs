namespace HRMSAPI.DTO
{

    public class EmployeePayrollUploadDto
    {
        public int Id { get; set; }
        public string? ECode { get; set; }
        public string? LocCode { get; set; }
        public string? Location { get; set; }
        public string? EmpName { get; set; }
        public string? Department { get; set; }
        public string? Designation { get; set; }

        public string? MonthYear { get; set; }       // MMM-yy
        public string? ExcelMonthYear { get; set; }
        public decimal? PayableDays { get; set; }

        public decimal? EmpPF { get; set; }
        public decimal? EmprPF { get; set; }
        public decimal? DepositedPF { get; set; }

        public string? ChallanNumber { get; set; }
        public string? ChallanPdfPath { get; set; }

    }
    public class CompOffDto
    {
        public int CompOffId { get; set; }
        public string Ecode { get; set; }
        public string MonthYear { get; set; }
        public decimal CompOffEarn { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
    }
    public class PagedResultNew<T>
    {
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public List<T> Data { get; set; } = new();
    }
    public class EmployeeESICUploadDto
    {
        public int Id { get; set; }

        // Employee Info
        public string? ECode { get; set; }
        public string? LocCode { get; set; }
        public string? Location { get; set; }
        public string? EmpName { get; set; }
        public string? Department { get; set; }
        public string? Designation { get; set; }

        // Month
        public string? MonthYear { get; set; }

        // ESIC Values
        public decimal? PayableDays { get; set; }
        public decimal? EmpESIC { get; set; }
        public decimal? EmprESIC { get; set; }
        public decimal? DepositedESIC { get; set; }

        // Store & Challan
        public string? StoreCode { get; set; }
        public string? ChallanNumber { get; set; }
        public string? ChallanPdfPath { get; set; }
    }
}
