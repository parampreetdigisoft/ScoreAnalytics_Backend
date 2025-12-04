using AssessmentPlatform.Dtos.PublicDto;
using AssessmentPlatform.IServices;
using Microsoft.AspNetCore.Authorization;
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

        [HttpGet("getAllCities")]
        public async Task<IActionResult> getAllCities()
        {
            var response = await _publicService.GetAllCities();
            return Ok(response);
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
        [HttpGet("countries-cities")]
        public async Task<IActionResult> GetCountriesCities()
        {
            var data = await _publicService.GetCountriesAndCities_WithStaleSupport();
            return Ok(data);
        }

    }
}
