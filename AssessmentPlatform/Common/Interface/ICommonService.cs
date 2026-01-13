using AssessmentPlatform.Dtos.CityDto;

namespace AssessmentPlatform.Common.Interface
{
    public interface ICommonService
    {
        public Task<List<EvaluationCityProgressResultDto>> GetCitiesProgressAsync(int userId,int role, int year);
    }
}
