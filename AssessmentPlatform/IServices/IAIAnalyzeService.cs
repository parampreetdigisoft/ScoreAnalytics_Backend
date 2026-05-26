using AssessmentPlatform.Dtos.chatDto;
using AssessmentPlatform.Dtos.PublicDto;
using AssessmentPlatform.Services;

namespace AssessmentPlatform.IServices
{
    public interface IAIAnalyzeService
    {
        Task AnalyzeAllCitiesFull();
        Task AnalyzeSingleCityFull(int cityId);
        Task AnalyzeSingleCity(int cityId);
        Task AnalyzeCityPillars(int cityId);
        Task AnalyzeSinglePillar(int cityId, int pillarId);
        Task AnalyzeQuestionsOfCity(int cityId);
        Task AnalyzeQuestionsOfCityPillar(int cityId, int pillarId);
        Task ProcessDocument(int documentID);
        Task DeleteDocument(int documentID);
        Task AnalyzeCityImmediateSituation(int cityId);
        Task AnalyzeCityMissingQuestions(MissingCityQuestionRequest request);
        Task<ChatCityAskQuestionResponse> ChatCityAsk(ChatCityAskQuestionRequest request);
        Task<ChatCityAskQuestionResponse> ChatGlobalAsk(ChatGlobalAskQuestionRequest request);
        Task<ChatCityAskQuestionResponse> CrossComparision(CrossComparisionRequest request);
        Task<ChatEmergingTrendsResponse?> GetEmergingTrendsAndIssues(int city_count);
        Task<ChatCityExecutiveSlidesResponse?> GetCitySlides(int cityId);
        Task<ChatPillarLiveSignalsResponse?> GetPillarLiveSignals();
        Task RunEvery2HoursJob();
        Task RunMonthlyJob();
        Task RunDailyJob();        
    }
}
