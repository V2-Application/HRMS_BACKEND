using HRMSAPI.Extension;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Roomsy.DTOS.GenericsResponses;

namespace HRMSAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [RequirePageAccess("/emp-store-assignment")]
    public class EmployeeStoreVisibilityMappingController : ControllerBase
    {
        private readonly IEmployeeStoreVisibilityMappingService _service;

        public EmployeeStoreVisibilityMappingController(IEmployeeStoreVisibilityMappingService service)
        {
            _service = service;
        }

        /// <summary>
        /// Get all employee store visibility mappings grouped by ECode with comma-separated StCodes
        /// </summary>
        /// <returns>List of EmployeeStoreMappingResponseDto</returns>
        //[HttpGet("GetAllMappings")]
        //public async Task<IActionResult> GetAllMappings()
        //{
        //    var result = await _service.GetAllMappingsAsync();
        //    return StatusCode((int)result.Code, new ApiFetchAndResponse
        //    {
        //        Status = result.Status,
        //        Message = result.Message,
        //        Data = result.Data
        //    });
        //}

        [HttpGet("GetActiveLocations")]
        public async Task<IActionResult> GetActiveLocations()
        {
            var result = await _service.GetActiveLocationsAsync();
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }

        /// <summary>
        /// Get store state for a specific ECode using ufn_StoreState function
        /// </summary>
        /// <param name="eCode">Employee code to check store states for</param>
        /// <returns>FetchAndResponse with list of store states</returns>
        [HttpGet("GetStoreState")]
        public async Task<IActionResult> GetStoreState([FromQuery] string eCode)
        {
            var result = await _service.GetStoreStateAsync(eCode);
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }

        /// <summary>
        /// Get department state for a specific ECode and StCode using ufn_DeptState function
        /// </summary>
        /// <param name="eCode">Employee code to check department states for</param>
        /// <param name="stCode">Store code to check department states for</param>
        /// <returns>FetchAndResponse with list of department states</returns>
        [HttpGet("GetDeptState")]
        public async Task<IActionResult> GetDeptState([FromQuery] string eCode, [FromQuery] string stCode)
        {
            var result = await _service.GetDeptStateAsync(eCode, stCode);
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }

        /// <summary>
        /// Set department exceptions for a store using SetDeptExceptionsForStore stored procedure
        /// </summary>
        /// <param name="request">Request containing ECode, StCode, and deselected department IDs</param>
        /// <returns>ExecuteAndReponse</returns>
        [HttpPost("SetDeptExceptionsForStore")]
        public async Task<IActionResult> SetDeptExceptionsForStore([FromBody] SetDeptExceptionsForStoreDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiExecuteAndReponse
                {
                    Status = false,
                    Message = "Invalid model state"
                });
            }

            var result = await _service.SetDeptExceptionsForStoreAsync(request);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse
            {
                Status = result.Status,
                Message = result.Message
            });
        }

        /// <summary>
        /// Get designation state for a specific ECode, StCode, and DeptId using ufn_DesigState function
        /// </summary>
        /// <param name="eCode">Employee code to check designation states for</param>
        /// <param name="stCode">Store code to check designation states for</param>
        /// <param name="deptId">Department ID to check designation states for</param>
        /// <returns>FetchAndResponse with list of designation states</returns>
        [HttpGet("GetDesigState")]
        public async Task<IActionResult> GetDesigState([FromQuery] string eCode, [FromQuery] string stCode, [FromQuery] string deptId)
        {
            var result = await _service.GetDesigStateAsync(eCode, stCode, deptId);
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }

        /// <summary>
        /// Set designation exceptions for a store and department using SetDesigExceptionsForStoreDept stored procedure
        /// </summary>
        /// <param name="request">Request containing ECode, StCode, DeptId, and deselected designation IDs</param>
        /// <returns>ExecuteAndReponse</returns>
        [HttpPost("SetDesigExceptionsForStoreDept")]
        public async Task<IActionResult> SetDesigExceptionsForStoreDept([FromBody] SetDesigExceptionsForStoreDeptDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiExecuteAndReponse
                {
                    Status = false,
                    Message = "Invalid model state"
                });
            }

            var result = await _service.SetDesigExceptionsForStoreDeptAsync(request);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse
            {
                Status = result.Status,
                Message = result.Message
            });
        }

        /// <summary>
        /// Get permission index for an ECode using GetPermissionIndexForECode stored procedure
        /// </summary>
        /// <param name="eCode">Employee code to get permissions for</param>
        /// <param name="stCode">Optional store code to filter by</param>
        /// <param name="deptId">Optional department ID to filter by</param>
        /// <returns>FetchAndResponse with permission index data</returns>
        [HttpGet("GetPermissionIndexForECode")]
        public async Task<IActionResult> GetPermissionIndexForECode([FromQuery] string eCode, [FromQuery] string? stCode = null, [FromQuery] string? deptId = null)
        {
            var result = await _service.GetPermissionIndexForECodeAsync(eCode, stCode, deptId);
            return StatusCode((int)result.Code, new ApiFetchAndResponse
            {
                Status = result.Status,
                Message = result.Message,
                Data = result.Data
            });
        }

        /// <summary>
        /// Upsert employee store visibility mappings
        /// Handles insert, update, and soft delete based on provided mappings
        /// </summary>
        /// <param name="upsertDto">List of ECode and StCodes mappings</param>
        /// <returns>ExecuteAndReponse</returns>
        [HttpPost("UpsertMappings")]
        public async Task<IActionResult> UpsertMappings([FromBody] EmployeeStoreMappingUpsertDto upsertDto)
        {
            if (!ModelState.IsValid)
            {
            
                return BadRequest(new ApiExecuteAndReponse
                {
                    Status = false,
                    Message = "Invalid model state",
                    
                });
            }

            var result = await _service.UpsertMappingsAsync(upsertDto);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse
            {
                Status = result.Status,
                Message = result.Message,
                
            });
        }

        /// <summary>
        /// Uploader method for inserting new ECode and StCode mapping
        /// Checks for duplicates and returns error if mapping already exists
        /// </summary>
        /// <param name="uploaderDto">ECode and StCode to insert</param>
        /// <returns>ExecuteAndReponse</returns>
        //[HttpPost("UploaderMapping")]
        //public async Task<IActionResult> UploaderMapping([FromBody] EmployeeStoreMappingUploaderDto uploaderDto)
        //{
        //    if (!ModelState.IsValid)
        //    {
        //        return BadRequest(new ApiExecuteAndReponse
        //        {
        //            Status = false,
        //            Message = "Invalid model state",
                    
        //        });
        //    }

        //    var result = await _service.UploaderMappingAsync(uploaderDto);
        //    return StatusCode((int)result.Code, new ApiExecuteAndReponse
        //    {
        //        Status = result.Status,
        //        Message = result.Message,
                
        //    });
        //}

        /// <summary>
        /// Upload Excel file for bulk insertion of ECode and StCode mappings
        /// Validates Excel format, headers, and checks for duplicates
        /// </summary>
        /// <param name="file">Excel file containing ECode and StCode mappings</param>
        /// <param name="createdBy">User creating the mappings</param>
        /// <returns>ExecuteAndReponse with detailed validation results</returns>
        //[HttpPost("UploadExcel")]
        //public async Task<IActionResult> UploadExcel([FromForm] IFormFile file, [FromForm] string? createdBy = null)
        //{
        //    if (file == null || file.Length == 0)
        //    {
        //        return BadRequest(new ApiExecuteAndReponse
        //        {
        //            Status = false,
        //            Message = "File is required",
                    
        //        });
        //    }

        //    var result = await _service.UploadExcelAsync(file, createdBy);
        //    return StatusCode((int)result.Code, new ApiExecuteAndReponse
        //    {
        //        Status = result.Status,
        //        Message = result.Message,
                
        //    });
        //}

        /// <summary>
        /// Upload Excel file for bulk insertion of ECode, StCode, and DeptName mappings
        /// Creates store access, department exceptions, and designation exceptions
        /// </summary>
        /// <param name="file">Excel file containing ECode, StCode, and DeptName mappings</param>
        /// <param name="createdBy">User creating the mappings</param>
        /// <returns>ExecuteAndReponse with detailed validation results</returns>
        [HttpPost("UploadExcelWithDept")]
        public async Task<IActionResult> UploadExcelWithDept([FromForm] FileDTO fileD, [FromForm] string? createdBy = null)
        {
            var file = fileD.File;
            if (file == null || file.Length == 0)
            {
                return BadRequest(new ApiExecuteAndReponse
                {
                    Status = false,
                    Message = "File is required"
                });
            }

            var result = await _service.UploadExcelWithDeptAsync(file, createdBy);
            return StatusCode((int)result.Code, new ApiExecuteAndReponse
            {
                Status = result.Status,
                Message = result.Message
            });
        }
    }
}
