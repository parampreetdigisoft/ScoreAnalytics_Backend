
using AssessmentPlatform.Dtos.kpiDto;
using AssessmentPlatform.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssessmentPlatform.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class KpiController : ControllerBase
    {
        private readonly IKpiService _kpiService;

        public KpiController(IKpiService kpiService)
        {
            _kpiService = kpiService;
        }
        [HttpGet]
        [Route("GetAnalyticalLayerResults")]
        public async Task<IActionResult> GetAnalyticalLayerResults([FromQuery] GetAnalyticalLayerRequestDto response)
        {
            var result = await _kpiService.GetAnalyticalLayerResults(response);
            return Ok(result);
        }
        [HttpGet]
        [Route("GetAllKpi")]
        public async Task<IActionResult> GetAllKpi()
        {
            var result = await _kpiService.GetAllKpi();
            return Ok(result);
        }
    }
}
