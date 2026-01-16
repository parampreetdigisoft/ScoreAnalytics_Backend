using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Common.Models.settings;
using AssessmentPlatform.Data;
using AssessmentPlatform.IServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AssessmentPlatform.Services
{
    public class AIAnalyzeService : IAIAnalyzeService
    {
        private readonly HttpService _httpService;
        private readonly  string aiUrl = "http://127.0.0.1:8000";
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private Dictionary<string, string> headers;
        public AIAnalyzeService(HttpService httpService, IOptions<AppSettings> appSettings, ApplicationDbContext context, IAppLogger appLogger)
        {
            _httpService = httpService;
            aiUrl = appSettings?.Value?.AiUrl ?? aiUrl;
            _context = context;
            _appLogger = appLogger;
            headers = new Dictionary<string, string> { { "X-API-Key", appSettings?.Value?.AiToken ?? "" } };
        }
        public async Task RunMonthlyJob()
        {
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
                await ImportAiScore();
            }
            catch (Exception ex)
            {
               await _appLogger.LogAsync("Error in Running job in Every 2-hour AI ", ex);
            }

        }

        public async Task ImportAiScore()
        {
            // if new city added
            var totalPillar = await _context.Pillars.CountAsync();
            var allCitiesIds = _context.Cities.Where(x=>x.IsActive && !x.IsDeleted).Select(x=>x.CityID).ToList();
            var importedCitiesIds = _context.AICityScores.Select(x => x.CityID);

            var newCitiesIds = allCitiesIds.Where(x=> !importedCitiesIds.Contains(x)).ToList();
            foreach (var id in newCitiesIds)
            {
                await AnalyzeSingleCityFull(id);
            }

            var now = DateTime.UtcNow;

            // Run at 1st day of every month at 01:00 AM UTC
            var date = new DateTime(now.Year, now.Month, 1, 1, 0, 0, DateTimeKind.Utc)
                            .AddMonths(-1);

            var importPillarsCityIds = _context.AIPillarScores
                .GroupBy(x => x.CityID)
                .Where(g => g.Max(x => x.UpdatedAt) < date || g.Count() < totalPillar)
                .Select(g => g.Key)
                .ToList();


            foreach (var id in importPillarsCityIds)
            {
                await AnalyzeCityPillars(id);
            }


            var needtoImportCityIds = _context.AICityScores.Where(x => x.UpdatedAt < date).Select(x=>x.CityID);
            foreach (var id in needtoImportCityIds)
            {
                await AnalyzeSingleCity(id);
            }
        }

        public async Task AnalyzeAllCitiesFull()
        {
            var url = aiUrl + AiEndpoints.AnalyzeAllCitiesFull;
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }

        public async Task AnalyzeSingleCityFull(int cityId)
        {
            var url = aiUrl + AiEndpoints.AnalyzeSingleCityFull(cityId);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }

        public async Task AnalyzeSingleCity(int cityId)
        {
            var url = aiUrl + AiEndpoints.AnalyzeSingleCity(cityId);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }

        public async Task AnalyzeCityPillars(int cityId)
        {
            var url = aiUrl + AiEndpoints.AnalyzeCityPillars(cityId);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }

        public async Task AnalyzeQuestionsOfCity(int cityId)
        {
            var url = aiUrl + AiEndpoints.AnalyzeCityQuestions(cityId);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }

        public async Task AnalyzeQuestionsOfCityPillar(int cityId, int pillarId)
        {
            var url = aiUrl + AiEndpoints.AnalyzeCityPillarQuestions(cityId, pillarId);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }


    }

    #region AiEndpoints

    public static class AiEndpoints
    {
        private const string BasePath = "/api/cities-score-analysis";

        public static string AnalyzeAllCitiesFull =>
            $"{BasePath}/analyze/full";

        public static string AnalyzeSingleCityFull(int cityId) =>
            $"{BasePath}/analyze/{cityId}/full";

        public static string AnalyzeSingleCity(int cityId) =>
            $"{BasePath}/analyze/{cityId}";

        public static string AnalyzeCityPillars(int cityId) =>
            $"{BasePath}/analyze/{cityId}/pillars";

        public static string AnalyzeCityQuestions(int cityId) =>
            $"{BasePath}/analyze/{cityId}/questions";

        public static string AnalyzeCityPillarQuestions(int cityId, int pillarId) =>
            $"{BasePath}/analyze/{cityId}/pillars/{pillarId}/questions";
    }

    #endregion
}
