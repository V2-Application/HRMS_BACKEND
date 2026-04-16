using Microsoft.AspNetCore.Mvc;
using HRMSAPI.Interfaces;
using Roomsy.DTOS.GenericsResponses;

namespace HRMSAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JDController : ControllerBase
    {
        private readonly IJDService _jdService;

        public JDController(IJDService jdService)
        {
            _jdService = jdService;
        }

        [HttpPost("upsert")]
        public async Task<ActionResult<ExecuteAndReponse>> UpsertJDs([FromBody] List<JDUpsertDto> jdList)
        {
            var result = await _jdService.UpsertJDsAsync(jdList);
            return StatusCode((int)result.Code, result);
        }

        [HttpPost("delete")]
        public async Task<ActionResult<ExecuteAndReponse>> DeleteJD([FromQuery] int jdId)
        {
            var result = await _jdService.DeleteJDAsync(jdId);
            return StatusCode((int)result.Code, result);
        }

        [HttpGet]
        public async Task<ActionResult<FetchAndResponse>> GetAllJDs()
        {
            var result = await _jdService.GetAllJDsAsync();
            return StatusCode((int)result.Code, result);
        }
    }
}
