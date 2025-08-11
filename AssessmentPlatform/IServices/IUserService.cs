using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Dtos.UserDtos;
using AssessmentPlatform.Models;

namespace AssessmentPlatform.IServices
{
    public interface IUserService
    {
        User GetByEmail(string email);
        Task<PaginationResponse<GetUserByRoleResponse>> GetUserByRole(GetUserByRoleRequestDto requestDto);
    }
} 