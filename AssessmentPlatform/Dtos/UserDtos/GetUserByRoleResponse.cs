using AssessmentPlatform.Dtos.CityDto;

namespace AssessmentPlatform.Dtos.UserDtos
{
    public class GetUserByRoleResponse : PublicUserResponse
    {
        public List<AddUpdateCityDto> cities { get; set; } = new();
    }
}
