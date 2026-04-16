namespace HRMSAPI.DTO
{
    public class VendorListDto
    {
        public long VendorId { get; set; }
        public string ContractorName { get; set; }
        public string ContractorCode { get; set; }
        public DateTime? ContractStartDate { get; set; }
        public DateTime? ContractEndDate { get; set; }
        public int EmployeeCount { get; set; }
    }
    public class PagedVendorListDto
    {
        public List<VendorListDto>? Vendors { get; set; } = new List<VendorListDto>();
        public int TotalRecords { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

}
