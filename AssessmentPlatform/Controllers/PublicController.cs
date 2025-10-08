using AssessmentPlatform.Dtos.PublicDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AssessmentPlatform.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class PublicController : ControllerBase
    {
        public readonly IPublicService _publicService;
        public PublicController(IPublicService publicService)
        {
            _publicService = publicService;
        }
        [HttpGet("GetPartnerCitiesFilterRecord")]
        public async Task<IActionResult> GetPartnerCitiesFilterRecord() => Ok(await _publicService.GetPartnerCitiesFilterRecord());

        [HttpGet]
        [Route("GetAllPillarAsync")]
        public async Task<IActionResult> GetAllPillarAsync() => Ok(await _publicService.GetAllPillarAsync());

        [HttpGet("GetPartnerCities")]
        public async Task<IActionResult> GetPartnerCities([FromQuery] PartnerCityRequestDto r)
        {
            var response = await _publicService.GetPartnerCities(r);
            return Ok(response);
        }
        [HttpGet("DownloadExecutiveSummeryPdf")]
        public IActionResult DownloadExecutiveSummeryPdf()
        {
            try
            {
                var fileName = "Executive-Summary.pdf";
                // Assuming PDFs are in wwwroot/pdf folder
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "pdf", fileName);

                if (!System.IO.File.Exists(filePath))
                    return NotFound("File not found");

                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                return File(fileBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error");
            }
        }
        [HttpGet("DownloadSummeryReportPdf")]
        public IActionResult DownloadSummeryReportPdf()
        {
            try
            {
                var fileName = "download-summary-report.pdf";
                // Assuming PDFs are in wwwroot/pdf folder
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "pdf", fileName);

                if (!System.IO.File.Exists(filePath))
                    return NotFound("File not found");

                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                return File(fileBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
