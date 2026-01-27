using AssessmentPlatform.Dtos.CityDto;

namespace AssessmentPlatform.Common.Interface
{
    public interface ICommonService
    {
        /// <summary>
        /// Based on user role it will return pillar wise Manual progress and Ai progress Score
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="role"></param>
        /// <param name="year"></param>
        /// <returns></returns>
        public Task<List<EvaluationCityProgressResultDto>> GetCitiesProgressAsync(int userId,int role, int year);
    }
}
