using System.Collections.Generic;
using System.Threading.Tasks;
using AssessmentPlatform.Models;

namespace AssessmentPlatform.Services
{
    public interface IPillarService
    {
        Task<List<Pillar>> GetAllAsync();
        Task<Pillar> GetByIdAsync(int id);
        Task<Pillar> AddAsync(Pillar pillar);
        Task<Pillar> UpdateAsync(int id, Pillar pillar);
        Task<bool> DeleteAsync(int id);
    }
} 