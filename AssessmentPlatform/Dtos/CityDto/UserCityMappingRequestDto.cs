namespace AssessmentPlatform.Dtos.CityDto
{
    public class UserCityMappingRequestDto
    {
        public int UserId { get; set; }
        public int CityId { get; set; }
        public int AssignedByUserId { get; set; }
    }
    public class UserCityUnMappingRequestDto
    {
        public int UserId { get; set; }
        public int AssignedByUserId { get; set; }
    }
}
