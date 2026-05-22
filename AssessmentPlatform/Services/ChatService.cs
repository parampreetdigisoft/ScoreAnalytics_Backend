

using AssessmentPlatform.Common.Interface;
using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.chatDto;
using AssessmentPlatform.Dtos.PillarDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics.Metrics;



namespace AssessmentPlatform.Services
{
    public class ChatService : IChatService
    {
        #region  constructor
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly IAIAnalyzeService _aIAnalyzeService;
        private readonly IMemoryCache _cache;
        private readonly ICommonService _commonService;
        public ChatService(ApplicationDbContext context,
            IAppLogger appLogger, IAIAnalyzeService aIAnalyzeService, IMemoryCache cache, ICommonService commonService)
        {
            _context = context;
            _appLogger = appLogger;
            _aIAnalyzeService = aIAnalyzeService;
            _cache = cache;
            _commonService = commonService;
        }
        public async Task<ResultResponseDto<List<AIAssistantFAQDto>>> GetAssistantFAQDs(int userId, UserRole userRole)
        {
            try
            {
                var faqs = _context.AIAssistantFAQ
                    .Where(x => x.IsActive)
                    .Select(x => new AIAssistantFAQDto
                    {
                        FAQID = x.FAQID,
                        Related = x.Related,
                        Category = x.Category,
                        QuestionText = x.QuestionText,
                        DisplayOrder = x.DisplayOrder,
                        IsAnsweredFaq =  !string.IsNullOrEmpty(x.AnswerText)
                    }).ToList();

                return ResultResponseDto<List<AIAssistantFAQDto>>.Success(faqs, new[] { "Faqs get successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("An error occurred while getting the GetAssistantFAQDs request.", ex);
                return ResultResponseDto<List<AIAssistantFAQDto>>.Failure(new[] { "An error occurred while processing your request. Please try again later." });
            }
        }

        public async Task<ResultResponseDto<ChatResponseDto>> AskAboutCity(CityChatRequestDto request)
        {
            try
            {
                var r = new ChatCityAskQuestionRequest
                {
                    CityID = request.CityID,
                    PillarID = request.PillarID,
                    QuestionText = request.QuestionText,
                    FAQID = request.FAQID,
                    HistoryText = request.HistoryText
                };

                var resutl = await _aIAnalyzeService.ChatCityAsk(r);
          
                if (resutl == null || resutl.Success != true)
                {
                    return ResultResponseDto<ChatResponseDto>.Failure(
                        new[] { resutl?.Message ?? "Failed to query request from VUI Aevum." }
                    );
                }

                return ResultResponseDto<ChatResponseDto>.Success(new ChatResponseDto
                {
                    CityID = request.CityID,
                    PillarID = request.PillarID,
                    QuestionText = request.QuestionText,
                    FAQID = request.FAQID,
                    ResponseText = resutl.Result ?? "No response from ."
                });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("An error occurred while processing the AskAboutCity request.", ex);
                return ResultResponseDto<ChatResponseDto>.Failure(new[] { "An error occurred while processing your request. Please try again later." });
            }
        }     


        public async Task<ResultResponseDto<ChatCityExecutiveSlidesResponse>> GetCitySlides(int cityId, int userId, UserRole userRole)
        {
            string cacheKey = $"CitySlides_{cityId}";

            try
            {
                if (userRole == UserRole.CityUser)
                {
                    var isValidCity = _context.PublicUserCityMappings.Where(x => x.UserID == userId).Any(c => c.CityID == cityId);
                    if (!isValidCity)
                    {
                        return ResultResponseDto<ChatCityExecutiveSlidesResponse>.Failure(new[] { "You don't have access to this city data." });
                    }
                }
                var year = DateTime.UtcNow.Year;

                var cityExists = await _commonService.GetCitiesRankings(cityId, year);

                var city = cityExists.FirstOrDefault(x => x.CityID == cityId);

                if (city == null)
                {
                    return ResultResponseDto<ChatCityExecutiveSlidesResponse>.Failure(new[] { "City not found." });
                }

                var pillars = (
                    from p in _context.Pillars

                    join x in _context.AIPillarScores
                        .Where(a => a.CityID == city.CityID
                                 && a.Year == city.DataYear)
                    on p.PillarID equals x.PillarID into pillarScores

                    from score in pillarScores.DefaultIfEmpty()

                    select new PillarsUserHistoryResponseDto
                    {
                        PillarID = p.PillarID,
                        PillarName = p.PillarName ?? "",
                        DisplayOrder = p.DisplayOrder,
                        PillarScore = score != null ? score.AIProgress ?? 0 : 0,
                        ImagePath = p.ImagePath
                    }
                ).ToList();

                if (userRole == UserRole.CityUser)
                {
                    var validPillars = _context.CityUserPillarMappings.Where(x => x.UserID == userId).Select(x => x.PillarID);
                    pillars = pillars.Where(x => validPillars.Contains(x.PillarID)).ToList();
                }
                var cityResult = new CityRankingResponseDto
                {
                    State = city.State,
                    CityID = city.CityID,
                    CityName = city.CityName,
                    CityRank = city.CityRank,
                    Country = city.Country,
                    CityAIScore = city.CityAIScore,
                    DataYear = city.DataYear,
                    Region = city.Region,
                    CountryRank= city.CountryRank,
                    TotalCity = city.TotalCity,
                    TotalCityInCountry = city.TotalCityInCountry,
                    Pillars = pillars.OrderBy(p => p.DisplayOrder).ToList()
                };
                if (_cache.TryGetValue(cacheKey, out ChatCityExecutiveSlidesResponse cachedResult))
                {
                    cachedResult.Result.City = cityResult;
                    return ResultResponseDto<ChatCityExecutiveSlidesResponse>.Success(
                        cachedResult,
                        new List<string>
                        {
                            "City executive slides fetched successfully from cache."
                        }
                    );
                }
                // ✅ Fetch from AI service
                var result = await _aIAnalyzeService.GetCitySlides(cityId);

                if (result == null || result.Success != true)
                {
                    return ResultResponseDto<ChatCityExecutiveSlidesResponse>.Failure(
                        new[]
                        {
                            result?.Message ??
                            "Failed to fetch City executive slides from PEM Aevum."
                        }
                    );
                }

                // ✅ Store in cache
                _cache.Set(cacheKey, result,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12),
                        SlidingExpiration = TimeSpan.FromHours(10),
                        Priority = CacheItemPriority.High
                    });
                result.Result.City = cityResult;
                return ResultResponseDto<ChatCityExecutiveSlidesResponse>.Success(
                    result,
                    new List<string>
                    {
                         "City executive slides fetched successfully."
                    }
                );
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync(
                    "An error occurred while processing the GetCitySlides request.",
                    ex
                );

                return ResultResponseDto<ChatCityExecutiveSlidesResponse>.Failure(
                    new[]
                    {
                        "An error occurred while processing your request. Please try again later."
                    }
                );
            }
        }

        public async Task<ResultResponseDto<ChatResponseDto>> AskAboutGlobal(ChatGlobalAskQuestionRequestDto request)
        {
            try
            {
                var r = new ChatGlobalAskQuestionRequest
                {  
                    QuestionText = request.QuestionText,
                    FAQID = request.FAQID,
                    HistoryText = request.HistoryText
                };

                var result = await _aIAnalyzeService.ChatGlobalAsk(r);

                if (result == null || result.Success != true)
                {
                    return ResultResponseDto<ChatResponseDto>.Failure(
                        new[] { result?.Message ?? "Failed to query request from VUI Aevum." }
                    );
                }

                return ResultResponseDto<ChatResponseDto>.Success(new ChatResponseDto
                {       
                    QuestionText = request.QuestionText,
                    FAQID = request.FAQID,
                    ResponseText = result.Result ?? "An error occurred or we do not have an answer for that."
                });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("An error occurred while processing the AskAboutGlobal request.", ex);
                return ResultResponseDto<ChatResponseDto>.Failure(new[] { "An error occurred while processing your request. Please try again later." });
            }
        }

        public async Task<ResultResponseDto<ChatResponseDto>> CrossComparision(CrossComparisionRequestDto request)
        {
            try
            {
                var r = new CrossComparisionRequest
                {
                    CityIDs = request.CityIDs,
                    QuestionText = request.QuestionText,
                    HistoryText = request.HistoryText
                };

                var resutl = await _aIAnalyzeService.CrossComparision(r);

                if (resutl == null || resutl.Success != true)
                {
                    return ResultResponseDto<ChatResponseDto>.Failure(
                        new[] { resutl?.Message ?? "Failed to query request from VUI Aevum." }
                    );
                }

                return ResultResponseDto<ChatResponseDto>.Success(new ChatResponseDto
                {
                    QuestionText = request.QuestionText,
                    FAQID = null,
                    ResponseText = resutl.Result ?? "An error occurred or we do not have an answer for that."
                });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("An error occurred while processing the CrossComparision request.", ex);
                return ResultResponseDto<ChatResponseDto>.Failure(new[] { "An error occurred while processing your request. Please try again later." });
            }
        }


        #endregion
    }
}
