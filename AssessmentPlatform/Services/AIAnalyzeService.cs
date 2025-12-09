using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Common.Models.settings;
using AssessmentPlatform.Data;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.Extensions.Options;

namespace AssessmentPlatform.Services
{
    public class AIAnalyzeService : IAIAnalyzeService
    {
        private readonly HttpService _httpService;
        private readonly  string aiUrl = "http://127.0.0.1:8000";
        private readonly ApplicationDbContext _context;

        public AIAnalyzeService(HttpService httpService, IOptions<AppSettings> appSettings, ApplicationDbContext context)
        {
            _httpService = httpService;
            aiUrl = appSettings?.Value?.AiUrl ?? aiUrl;
            _context = context;
        }
        public async Task RunMonthlyJob()
        {
            // your API call logic
            //Console.WriteLine("Running monthly AI job...");
            var newCitiesIds = _context.Cities.Where(x => x.IsActive && !x.IsDeleted ).Select(x => x.CityID).ToList();
            foreach (var id in newCitiesIds)
            {
                await AnalyzeSingleCityFull(id);
            }

        }

        public async Task RunEvery2HoursJob()
        {
            try
            {
                // your API call logic
                Console.WriteLine("Running 2-hour AI job...");
                await ImportAiScore();
            }
            catch (Exception ex)
            {
                // log error
                Console.WriteLine(ex.Message);
            }

        }

        public async Task ImportAiScore()
        {
            // if new city added
            var importedCitiesIds = _context.AICityScores.Select(x => x.CityID);

            var newCitiesIds = _context.Cities.Where(x=>x.IsActive && !x.IsDeleted && !importedCitiesIds.Contains(x.CityID)).Select(x=>x.CityID).ToList();
            foreach (var id in newCitiesIds)
            {
                await AnalyzeSingleCityFull(id);
            }

            var date = DateTime.UtcNow.AddDays(-31);
            var needtoImportCityIds = _context.AICityScores.Where(x => x.UpdatedAt < date).Select(x=>x.CityID);


            foreach (var id in needtoImportCityIds)
            {
                await AnalyzeSingleCity(id);
            }

            var importPillarsCityIds = _context.AIPillarScores.Where(x => x.UpdatedAt < date).Select(x => x.CityID);
            foreach (var id in importPillarsCityIds)
            {
                await AnalyzeCityPillars(id);
            }
        }

        public async Task AnalyzeAllCitiesFull()
        {
             var url = aiUrl + "/api/cities-score-analysis/analyze/full";
             await _httpService.SendAsync<dynamic>(HttpMethod.Post, url);
        }

        public async Task AnalyzeSingleCityFull(int cityId)
        {
            var url = aiUrl + $"/api/cities-score-analysis/analyze/{cityId}/full";
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url);
        }
        public async Task AnalyzeSingleCity(int cityId)
        {
            var url = aiUrl + $"/api/cities-score-analysis/analyze/{cityId}";
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url);
        }
        public async Task AnalyzeCityPillars(int cityId)
        {
            var url = aiUrl + $"/api/cities-score-analysis/analyze/{cityId}/pillars";
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url);
        }

        public async Task AnalyzeQuestionsOfCity(int cityId)
        {
            var url = aiUrl + $"/api/cities-score-analysis/analyze/{cityId}/questions";
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url);
        }

        public async Task AnalyzeQuestionsOfCityPillar(int cityId, int pillarId)
        {
            var url = aiUrl + $"/api/cities-score-analysis/analyze/{cityId}/pillars/{pillarId}/questions";
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url);
        }

    }
}
