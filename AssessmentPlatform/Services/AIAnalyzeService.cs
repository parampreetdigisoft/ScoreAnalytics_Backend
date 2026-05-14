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

        public async Task RunDailyJob()
        {
            try
            {
                await ImportAllCityImmediateSummary();
                await ImportRemainingDocumentsToVectorDB();
                await DeleteRemainingDocumentsToVectorDB();

            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in Running job in Run daily job ", ex);
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


            var needtoImportCityIds = _context.AICityScores.Where(x => x.UpdatedAt < date && x.City.IsActive && !x.City.IsDeleted).Select(x=>x.CityID);
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
        public async Task AnalyzeSinglePillar(int cityId, int pillarId)
        {
            var url = aiUrl + AiEndpoints.AnalyzeSinglePillar(cityId, pillarId);
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

        public async Task AnalyzeCityImmediateSituation(int cityId)
        {
            var url = aiUrl + AiEndpoints.AnalyzeCityImmediateSituation(cityId);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }

        public async Task ProcessDocument(int documentID)
        {
            var url = aiUrl + AiEndpoints.ProcessDocument(documentID);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }
        public async Task DeleteDocument(int documentID)
        {
            var url = aiUrl + AiEndpoints.DeleteDocument(documentID);
            await _httpService.SendAsync<dynamic>(HttpMethod.Post, url, null, headers);
        }

        public async Task<ChatCityAskQuestionResponse> ChatCityAsk(ChatCityAskQuestionRequest request)
        {
            var url = aiUrl + AiEndpoints.ChatCityAsk();
            return await _httpService.SendAsync<ChatCityAskQuestionResponse>(HttpMethod.Post, url, request, headers) ?? new ChatCityAskQuestionResponse();            
        }
        public async Task<ChatCityAskQuestionResponse> ChatGlobalAsk(ChatGlobalAskQuestionRequest request)
        {
            var url = aiUrl + AiEndpoints.ChatGlobalAsk();
            return await _httpService.SendAsync<ChatCityAskQuestionResponse>(HttpMethod.Post, url, request, headers) ?? new ChatCityAskQuestionResponse();            
        }

        public async Task<ChatCityExecutiveSlidesResponse?> GetCitySlides(int cityId)
        {
            var url = aiUrl + AiEndpoints.CitySlides();

            return await _httpService.SendAsync<ChatCityExecutiveSlidesResponse>(
                HttpMethod.Post,
                url,
                new CitySlidesRequest
                {
                    CityId = cityId
                },
                headers
            );
        }

        public async Task ImportAllCityImmediateSummary()
        {
            var allCitiesIds = await _context.Cities.Where(x => x.IsActive && !x.IsDeleted).Select(x => x.CityID).ToListAsync();

            foreach (var id in allCitiesIds)
            {
                await AnalyzeCityImmediateSituation(id);
                await Task.Delay(200);
            }

        }

        public async Task ImportRemainingDocumentsToVectorDB()
        {
            var activeDocumentIds = _context.CityDocuments
                    .Where(x => !x.IsDeleted)
                    .Select(x => x.CityDocumentID);

            var data = await _context.DocumentChunks
                .Where(x => !activeDocumentIds.Contains(x.CityDocumentID))
                .Select(x => x.CityDocumentID)

                .Union(
                    _context.DocumentTOC
                        .Where(x => !activeDocumentIds.Contains(x.CityDocumentID))
                        .Select(x => x.CityDocumentID)
                )
                .Distinct()
                .ToListAsync();


            foreach (var documentID in data)
            {
                await ProcessDocument(documentID);
                await Task.Delay(200);
            }
        }
        public async Task DeleteRemainingDocumentsToVectorDB()
        {
            var activeDocumentIds = _context.CityDocuments
                    .Where(x => x.IsDeleted)
                    .Select(x => x.CityDocumentID);

            var data = await _context.DocumentChunks
                .Where(x => activeDocumentIds.Contains(x.CityDocumentID))
                .Select(x => x.CityDocumentID)

                .Union(
                    _context.DocumentTOC
                        .Where(x => activeDocumentIds.Contains(x.CityDocumentID))
                        .Select(x => x.CityDocumentID)
                )
                .Distinct()
                .ToListAsync();

            foreach (var documentID in data)
            {
                await DeleteDocument(documentID);
                await Task.Delay(200);
            }
        }


    }

    #region AiEndpoints

    public static class AiEndpoints
    {
        private const string BasePath = "/api/cities-score-analysis";
        private const string DocumentPath = "/api/rag";
        private const string ChatPath = "/api/chat";

        public static string AnalyzeAllCitiesFull =>
            $"{BasePath}/analyze/full";

        public static string AnalyzeSingleCityFull(int cityId) =>
            $"{BasePath}/analyze/{cityId}/full";

        public static string AnalyzeSingleCity(int cityId) =>
            $"{BasePath}/analyze/{cityId}";

        public static string AnalyzeCityPillars(int cityId) =>
            $"{BasePath}/analyze/{cityId}/pillars";
        public static string AnalyzeSinglePillar(int cityId, int pillarId) =>
            $"{BasePath}/analyze/{cityId}/single-pillar/{pillarId}";

        public static string AnalyzeCityQuestions(int cityId) =>
            $"{BasePath}/analyze/{cityId}/questions";

        public static string AnalyzeCityPillarQuestions(int cityId, int pillarId) =>
            $"{BasePath}/analyze/{cityId}/pillars/{pillarId}/questions";

        public static string AnalyzeCityImmediateSituation(int cityId) =>
           $"{BasePath}/analyze/{cityId}/immediateSituation";

        public static string ProcessDocument(int documentId) =>
            $"{DocumentPath}/process-document/{documentId}";
        public static string DeleteDocument(int documentId) =>
            $"{DocumentPath}/delete-document/{documentId}";
        public static string ChatCityAsk() => $"{ChatPath}/city";
        public static string ChatGlobalAsk() => $"{ChatPath}/global";

        public static string CitySlides() => $"{ChatPath}/executive-slides";
    }

    #endregion


    #region Ai Models 

    public class ChatCityAskQuestionRequest : ChatGlobalAskQuestionRequest
    {
        public int CityID { get; set; }
        public int? PillarID { get; set; }
    }
    public class ChatGlobalAskQuestionRequest
    {
        public string QuestionText { get; set; }
        public string? HistoryText { get; set; }
        public int? FAQID { get; set; }
    }
    public class ChatCityAskQuestionResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Result { get; set; }
    }
    public class PerformanceSummary
    {
        public string Trend { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;
    }

    public class CombinedRiskItem
    {
        public int Rank { get; set; }

        public string Title { get; set; } = string.Empty;

        public int RiskScore { get; set; }

        public string Severity { get; set; } = string.Empty;

        public string Trend { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Recommendation { get; set; } = string.Empty;
    }

    public class EarlyWarningItem
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Timeframe { get; set; } = string.Empty;

        public string ImpactLevel { get; set; } = string.Empty;
    }

    public class CityExecutiveSlidesResult
    {
        public int CityId { get; set; }

        public string CityName { get; set; } = string.Empty;

        public PerformanceSummary DailyPerformance { get; set; } = new();

        public PerformanceSummary WeeklyPerformance { get; set; } = new();

        public PerformanceSummary MonthlyPerformance { get; set; } = new();

        public List<CombinedRiskItem> CombinedRisks { get; set; } = new();

        public List<EarlyWarningItem> EarlyWarnings { get; set; } = new();
    }

    public class ChatCityExecutiveSlidesResponse
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public CityExecutiveSlidesResult Result { get; set; } = new();
    }

    public class CitySlidesRequest
    {
        public int CityId { get; set; }
    }
    #endregion
}
