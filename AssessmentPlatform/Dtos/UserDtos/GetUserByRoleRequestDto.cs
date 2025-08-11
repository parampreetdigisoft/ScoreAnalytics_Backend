using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Models;

namespace AssessmentPlatform.Dtos.UserDtos
{
    public class GetUserByRoleRequestDto : PaginationRequest
    {
        public UserRole UserRole { get; set; }
        public int UserID { get; set; }
    }
}
