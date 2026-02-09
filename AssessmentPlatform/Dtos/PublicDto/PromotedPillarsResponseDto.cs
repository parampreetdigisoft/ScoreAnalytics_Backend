namespace AssessmentPlatform.Dtos.PublicDto
{
    public class PromotedPillarsResponseDto
    {
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public string ImagePath { get; set; }
        public int DisplayOrder { get; set; }
        public List<PromotedCityResponseDto> Cities { get; set; }
    }

    public class PromotedCityResponseDto
    {
        public int CityID { get; set; }
        public string Country { get; set; }
        public string State { get; set; }
        public string CityName { get; set; }
        public string? PostalCode { get; set; }
        public string? Region { get; set; }
        public string? Image { get; set; }
        public decimal? ScoreProgress { get; set; }
        public string Description { get; set; }
    }
}
