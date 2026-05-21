namespace AssessmentPlatform.Dtos.chatDto
{
    public class PerformanceSummary
    {
        public string Trend { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;
    }

    public class CombinedRiskItem
    {
        public int Rank { get; set; }

        public string Title { get; set; } = string.Empty;

        public int RiskScore { get; set; }

        public string Severity { get; set; } = string.Empty;

        public string Trend { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Recommendation { get; set; } = string.Empty;
    }

    public class EarlyWarningItem
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Timeframe { get; set; } = string.Empty;

        public string ImpactLevel { get; set; } = string.Empty;
    }

    public class CityExecutiveSlidesResult
    {
        public CityRankingResponseDto City { get; set; }

        public string CityName { get; set; } = string.Empty;

        public PerformanceSummary RecentPerformance { get; set; } = new();

        public List<CombinedRiskItem> CombinedRisks { get; set; } = new();

        public List<EarlyWarningItem> EarlyWarnings { get; set; } = new();
    }

    public class ChatCityExecutiveSlidesResponse
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public CityExecutiveSlidesResult Result { get; set; } = new();
    }

    public class CitySlidesRequest
    {
        public int CityId { get; set; }
    }

    public class CityRankingResponseDto
    {
        public int CityID { get; set; }
        public string CityName { get; set; }
        public string State { get; set; }
        public string Country { get; set; }
        public string Region { get; set; }
        public int TotalCity { get; set; }
        public int CityRank { get; set; }
        public int TotalCityInCountry { get; set; }
        public int CountryRank { get; set; }
        public decimal CityAIScore { get; set; }
        public int? DataYear { get; set; }
        public List<PillarsUserHistoryResponseDto> Pillars { get; set; }
    }
    public class PillarsUserHistoryResponseDto
    {
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public decimal PillarScore { get; set; }
        public int DisplayOrder { get; set; }

        public string ImagePath { get; set; }
    }
}
