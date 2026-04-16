using System.ComponentModel.DataAnnotations;

namespace HRMSAPI.DTO
{
    public class EmployeeStoreVisibilityMappingDto
    {
        public long Id { get; set; }
        public string ECode { get; set; } = string.Empty;
        public string StCode { get; set; } = string.Empty;
        public bool? IsActive { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedOn { get; set; }
        public string? UpdatedBy { get; set; }
    }

    public class EmployeeStoreMappingResponseDto
    {
        public string ECode { get; set; } = string.Empty;
        public string StCodes { get; set; } = string.Empty; // Comma separated store codes
    }

    public class EmployeeStoreMappingUpsertDto
    {
        
        public List<EmployeeStoreMappingItemDto> Mappings { get; set; } = new List<EmployeeStoreMappingItemDto>();
        public string? UpdatedBy { get; set; }
    }

    public class EmployeeStoreMappingItemDto
    {
        [Required]
        public string ECode { get; set; } = string.Empty;
        //[Required]
        public string StCodes { get; set; } = string.Empty; // Comma separated store codes
    }

    public class EmployeeStoreMappingUploaderDto
    {
        [Required]
        public string ECode { get; set; } = string.Empty;
        [Required]
        public string StCode { get; set; } = string.Empty;
        public string? CreatedBy { get; set; }
    }

    public class EmployeeStoreDeptMappingUploaderDto
    {
        [Required]
        public string ECode { get; set; } = string.Empty;
        [Required]
        public string StCode { get; set; } = string.Empty;
        [Required]
        public string DeptName { get; set; } = string.Empty;
        public string? CreatedBy { get; set; }
    }

    public class ExcelUploadResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new List<string>();
        public int TotalRows { get; set; }
        public int ValidRows { get; set; }
        public int InvalidRows { get; set; }
        public int DuplicateRows { get; set; }
    }

    public class ExcelRowValidationDto
    {
        public int RowNumber { get; set; }
        public string ECode { get; set; } = string.Empty;
        public string StCode { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public List<string> ValidationErrors { get; set; } = new List<string>();
    }

    public class StoreStateDto
    {
        public string StCode { get; set; } = string.Empty;
        public bool IsChecked { get; set; }
        public bool IsIndeterminate { get; set; }
        public string State { get; set; } = string.Empty;
    }

    public class DeptStateDto
    {
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public bool IsChecked { get; set; }
        public bool IsIndeterminate { get; set; }
        public string State { get; set; } = string.Empty;
    }

    public class SetDeptExceptionsForStoreDto
    {
        [Required]
        public string ECode { get; set; } = string.Empty;
        
        [Required]
        public string StCode { get; set; } = string.Empty;
        
        public List<long> DeselectedDeptIds { get; set; } = new List<long>();
        
        public string? Actor { get; set; }
    }

    public class DesigStateDto
    {
        public int DesignationId { get; set; }
        public string DesignationName { get; set; } = string.Empty;
        public bool IsChecked { get; set; }
        public string State { get; set; } = string.Empty;
    }

    public class SetDesigExceptionsForStoreDeptDto
    {
        [Required]
        public string ECode { get; set; } = string.Empty;
        
        [Required]
        public string StCode { get; set; } = string.Empty;
        
        [Required]
        public string DeptId { get; set; } = string.Empty;
        
        public List<long> DeselectedDesigIds { get; set; } = new List<long>();
        
        public string? Actor { get; set; }
    }

    public class AllowedStoreDto
    {
        public string StCode { get; set; } = string.Empty;
    }

    public class DeptExceptionDto
    {
        public string StCode { get; set; } = string.Empty;
        public string DeptId { get; set; } = string.Empty;
    }

    public class DesigExceptionDto
    {
        public string StCode { get; set; } = string.Empty;
        public string DeptId { get; set; } = string.Empty;
        public string DesigId { get; set; } = string.Empty;
    }

    public class PermissionIndexDto
    {
        public List<AllowedStoreDto> AllowedStores { get; set; } = new List<AllowedStoreDto>();
        public List<DeptExceptionDto> DeptExceptions { get; set; } = new List<DeptExceptionDto>();
        public List<DesigExceptionDto> DesigExceptions { get; set; } = new List<DesigExceptionDto>();
    }
}
