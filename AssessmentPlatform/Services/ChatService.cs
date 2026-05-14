

using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.chatDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.Extensions.Caching.Memory;



namespace AssessmentPlatform.Services
{
    public class ChatService : IChatService
    {
        #region  constructor
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly IAIAnalyzeService _aIAnalyzeService;
        private readonly IMemoryCache _cache;
        public ChatService(ApplicationDbContext context,
            IAppLogger appLogger, IAIAnalyzeService aIAnalyzeService, IMemoryCache cache)
        {
            _context = context;
            _appLogger = appLogger;
            _aIAnalyzeService = aIAnalyzeService;
            _cache = cache;
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


        public async Task<ResultResponseDto<ChatCityExecutiveSlidesResponse>> GetCitySlides(int cityId)
        {
            string cacheKey = $"CitySlides_{cityId}";

            try
            {
                // ✅ Try cache first
                if (_cache.TryGetValue(
                    cacheKey,
                    out ChatCityExecutiveSlidesResponse cachedResult))
                {
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
                    "Failed to fetch city executive slides from VUI Aevum."
                        }
                    );
                }

                // ✅ Store in cache
                _cache.Set(
                    cacheKey,
                    result,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                        SlidingExpiration = TimeSpan.FromMinutes(5),
                        Priority = CacheItemPriority.High
                    });

                // ✅ Return response
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


        #endregion
    }
}
