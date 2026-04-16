namespace HRMSAPI.DTO
{
    public class ContractorDTO
    {
        public string ContractorCode { get; set; }
        public string ContractorName { get; set; }
        public string ServiceCategory { get; set; }
        public string NatureOfWork { get; set; }
        public string RegisteredAddress { get; set; }
        public string SiteAddress { get; set; }
        public string PAN { get; set; }
        public string GSTIN { get; set; }
        public bool? IsActive { get; set; }  
        public int EmployeeCount { get; set; }
    }
    public class PagedContractorListDto
    {
        public List<ContractorDTO> Contractors { get; set; }
        public int TotalRecords { get; set; }
        public int PageSize { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
    }

}
