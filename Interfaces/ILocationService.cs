using HRMSAPI.DTO;
using Microsoft.AspNetCore.Http;
using Roomsy.DTOS.GenericsResponses;
using System.Threading.Tasks;

namespace HRMSAPI.Interfaces
{
    public interface ILocationService
    {
        Task<FetchAndResponse> UploadLocationsExcelAsync(IFormFile file, string? updatedBy);
        Task<FetchAndResponse> getAllLocation();
        Task<LocationforgeoDto?> UpdateGeoAsync(LocationGeoUpdateRequest request);
        Task<FetchAndResponse> GetAllLocationsData();
        Task<FetchAndResponse> SoftDeleteLocationAsync(int locationId, string updatedBy);
        Task<FetchAndResponse> ToggleLocationStatusAsync(int locationId, string updatedBy);
        Task<FetchAndResponse> GetActiveEmployeesByLocationAsync(string stcode);
    }
} 