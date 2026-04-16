using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRMSAPI.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;
using Roomsy.DTOS.GenericsResponses;

namespace HRMSAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JobOpeningController : ControllerBase
    {
        private readonly IJobOpeningService _jobOpeningService;

        public JobOpeningController(IJobOpeningService jobOpeningService)
        {
            _jobOpeningService = jobOpeningService;
        }

        [HttpGet]
        public async Task<ActionResult<FetchAndResponse>> GetJobOpenings(string? searchText = null)
        {
            var result = await _jobOpeningService.GetJobOpeningsAsync(searchText);
            return StatusCode((int)result.Code, result);
        }

        [HttpGet("proc-openings")]
        public async Task<ActionResult<FetchAndResponse>> GetProcOpenings()
        {
            var result = await _jobOpeningService.GetProcOpeningsAsync();
            return StatusCode((int)result.Code, result);
        }

    }
}


