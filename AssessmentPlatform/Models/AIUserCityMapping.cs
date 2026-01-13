namespace AssessmentPlatform.Models
{
    public class AIUserCityMapping
    {
        public int AIUserCityMappingID { get; set; }

        public int CityID { get; set; }

        public int UserID { get; set; }

        public int? AssignBy { get; set; }

        public bool IsActive { get; set; } = false;

        public string? Comment { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

