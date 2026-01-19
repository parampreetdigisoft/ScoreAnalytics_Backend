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

        Task RunEvery2HoursJob();
        Task RunMonthlyJob();
    }
}
