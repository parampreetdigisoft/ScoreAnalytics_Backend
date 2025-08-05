using AssessmentPlatform.Models;

namespace AssessmentPlatform.IServices
{
    public interface ICityService
    {
        Task<List<City>> GetCitiesAsync();
        Task<City> GetByIdAsync(int id);
        Task<City> AddCityAsync(City q);
        Task<City> EditCityAsync(int id, City q);
        Task<bool> DeleteCityAsync(int id);
    }
}
