namespace AssessmentPlatform.Dtos.CityDto
{
    public class GetCitiesProgressAdminDto
    {
        public int CityID { get; set; }
        public string CityName { get; set; }
        public string State { get; set; }
        public string Country { get; set; }
        public int PillarID { get; set; }
        public string PillarName { get; set; } 
        public int DisplayOrder { get; set; }
        public int TotalScore { get; set; }
        public int TotalAns { get; set; }
        public decimal PillarProgress { get; set; }
        public decimal AIPillarProgress { get; set; }
        public decimal AICityProgress { get; set; }
    }
}
