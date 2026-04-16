using HRMSAPI.DTO;
using Roomsy.DTOS.GenericsResponses;

namespace HRMSAPI.Interfaces
{
    public interface IEmployeeStoreVisibilityMappingService
    {
        /// <summary>
        /// Get all employee store visibility mappings grouped by ECode with comma-separated StCodes
        /// </summary>
        /// <returns>List of EmployeeStoreMappingResponseDto</returns>
        Task<FetchAndResponse> GetAllMappingsAsync();

        /// <summary>
        /// Upsert employee store visibility mappings
        /// Handles insert, update, and soft delete based on provided mappings
        /// </summary>
        /// <param name="upsertDto">List of ECode and StCodes mappings</param>
        /// <returns>ExecuteAndReponse</returns>
        Task<ExecuteAndReponse> UpsertMappingsAsync(EmployeeStoreMappingUpsertDto upsertDto);

        /// <summary>
        /// Uploader method for inserting new ECode and StCode mapping
        /// Checks for duplicates and returns error if mapping already exists
        /// </summary>
        /// <param name="uploaderDto">ECode and StCode to insert</param>
        /// <returns>ExecuteAndReponse</returns>
        Task<ExecuteAndReponse> UploaderMappingAsync(EmployeeStoreMappingUploaderDto uploaderDto);

        /// <summary>
        /// Upload Excel file for bulk insertion of ECode and StCode mappings
        /// Validates Excel format, headers, and checks for duplicates
        /// </summary>
        /// <param name="file">Excel file containing ECode and StCode mappings</param>
        /// <param name="createdBy">User creating the mappings</param>
        /// <returns>ExecuteAndReponse with detailed validation results</returns>
        Task<ExecuteAndReponse> UploadExcelAsync(IFormFile file, string? createdBy = null);

        /// <summary>
        /// Get all active locations from tblLocation (IsActive = true)
        /// </summary>
        /// <returns>FetchAndResponse with list of active locations</returns>
        Task<FetchAndResponse> GetActiveLocationsAsync();

        /// <summary>
        /// Get store state for a specific ECode using ufn_StoreState function
        /// </summary>
        /// <param name="eCode">Employee code to check store states for</param>
        /// <returns>FetchAndResponse with list of store states</returns>
        Task<FetchAndResponse> GetStoreStateAsync(string eCode);

        /// <summary>
        /// Get department state for a specific ECode and StCode using ufn_DeptState function
        /// </summary>
        /// <param name="eCode">Employee code to check department states for</param>
        /// <param name="stCode">Store code to check department states for</param>
        /// <returns>FetchAndResponse with list of department states</returns>
        Task<FetchAndResponse> GetDeptStateAsync(string eCode, string stCode);

        /// <summary>
        /// Set department exceptions for a store using SetDeptExceptionsForStore stored procedure
        /// </summary>
        /// <param name="request">Request containing ECode, StCode, and deselected department IDs</param>
        /// <returns>ExecuteAndReponse</returns>
        Task<ExecuteAndReponse> SetDeptExceptionsForStoreAsync(SetDeptExceptionsForStoreDto request);

        /// <summary>
        /// Get designation state for a specific ECode, StCode, and DeptId using ufn_DesigState function
        /// </summary>
        /// <param name="eCode">Employee code to check designation states for</param>
        /// <param name="stCode">Store code to check designation states for</param>
        /// <param name="deptId">Department ID to check designation states for</param>
        /// <returns>FetchAndResponse with list of designation states</returns>
        Task<FetchAndResponse> GetDesigStateAsync(string eCode, string stCode, string deptId);

        /// <summary>
        /// Set designation exceptions for a store and department using SetDesigExceptionsForStoreDept stored procedure
        /// </summary>
        /// <param name="request">Request containing ECode, StCode, DeptId, and deselected designation IDs</param>
        /// <returns>ExecuteAndReponse</returns>
        Task<ExecuteAndReponse> SetDesigExceptionsForStoreDeptAsync(SetDesigExceptionsForStoreDeptDto request);

        /// <summary>
        /// Get permission index for an ECode using GetPermissionIndexForECode stored procedure
        /// </summary>
        /// <param name="eCode">Employee code to get permissions for</param>
        /// <param name="stCode">Optional store code to filter by</param>
        /// <param name="deptId">Optional department ID to filter by</param>
        /// <returns>FetchAndResponse with permission index data</returns>
        Task<FetchAndResponse> GetPermissionIndexForECodeAsync(string eCode, string? stCode = null, string? deptId = null);

        /// <summary>
        /// Upload Excel file for bulk insertion of ECode, StCode, and DeptName mappings
        /// Creates store access, department exceptions, and designation exceptions
        /// </summary>
        /// <param name="file">Excel file containing ECode, StCode, and DeptName mappings</param>
        /// <param name="createdBy">User creating the mappings</param>
        /// <returns>ExecuteAndReponse with detailed validation results</returns>
        Task<ExecuteAndReponse> UploadExcelWithDeptAsync(IFormFile file, string? createdBy = null);
    }
}
