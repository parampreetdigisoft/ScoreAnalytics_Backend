namespace AssessmentPlatform.Dtos.PublicDto
{
    public class EmergingTrendCityCard
    {
        public string ImagePath { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;

        public string CityCode { get; set; } = string.Empty;

        public string Region { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string Urgency { get; set; } = string.Empty;

        public int Confidence { get; set; }

        public string Icon { get; set; } = string.Empty;

        public string Color { get; set; } = string.Empty;

        public string SourceUrl { get; set; } = string.Empty;
    }

    public class EmergingTrendsResult
    {
        public string UpdatedAt { get; set; } = string.Empty;

        public string Headline { get; set; } = string.Empty;

        public string SubHeadline { get; set; } = string.Empty;

        public List<EmergingTrendCityCard> Cities { get; set; } = new();
    }

    public class ChatEmergingTrendsResponse
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public EmergingTrendsResult Result { get; set; } = new();
    }
}
