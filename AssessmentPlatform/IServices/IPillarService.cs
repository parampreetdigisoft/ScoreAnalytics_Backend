using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Dtos.AssessmentDto;
using AssessmentPlatform.Dtos.PillarDto;
using AssessmentPlatform.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AssessmentPlatform.IServices
{
    public interface IPillarService
    {
        Task<List<Pillar>> GetAllAsync();
        Task<Pillar> GetByIdAsync(int id);
        Task<Pillar> AddAsync(Pillar pillar);
        Task<Pillar> UpdateAsync(int id, UpdatePillarDto pillar);
        Task<bool> DeleteAsync(int id);
        Task<ResultResponseDto<List<PillarsHistroyResponseDto>>> GetPillarsHistoryByUserId(GetCityPillarHistoryRequestDto id);
    }
} 