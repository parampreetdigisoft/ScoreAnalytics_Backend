using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.Dtos.kpiDto;
using AssessmentPlatform.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AssessmentPlatform.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class KpiController : ControllerBase
    {
        private readonly IKpiService _kpiService;

        public KpiController(IKpiService kpiService)
        {
            _kpiService = kpiService;
        }
        [HttpGet]
        [Route("GetAnalyticalLayerResults")]
        [Authorize]
        public async Task<IActionResult> GetAnalyticalLayerResults([FromQuery] GetAnalyticalLayerRequestDto response)
        {
            var result = await _kpiService.GetAnalyticalLayerResults(response);
            return Ok(result);
        }
    }
}
