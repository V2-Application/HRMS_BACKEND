using DocumentFormat.OpenXml.Office2010.ExcelAc;
using System.Collections.Generic;
using Roomsy.DTOS.GenericsResponses;

namespace HRMSAPI.Interfaces
{
   
     public interface IJobOpeningService {
        Task<FetchAndResponse> GetJobOpeningsAsync(string? searchText);
        Task<FetchAndResponse> GetProcOpeningsAsync();
    }
    
}
