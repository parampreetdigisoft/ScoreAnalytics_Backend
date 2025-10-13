using AssessmentPlatform.Enums;

namespace AssessmentPlatform.Dtos.CityUserDto
{
    public class UserCityGetPillarInfoRequstDto
    {
        public int UserID { get; set; } = 0;
        public int CityID { get; set; }
        public int PillarID { get; set; }
        public DateTime UpdatedAt { get; set; } = new DateTime(DateTime.Now.Year, 1, 1);
        public TieredAccessPlan Tiered { get; set; } = TieredAccessPlan.Pending;
    }
}
