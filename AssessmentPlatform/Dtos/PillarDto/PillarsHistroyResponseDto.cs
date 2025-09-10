namespace AssessmentPlatform.Dtos.PillarDto
{
    public class PillarsHistroyResponseDto
    {
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public int DisplayOrder { get; set; } = 0;
        public List<PillarsUserHistroyResponseDto> Users { get; set; } = new();
    }
    public class PillarsUserHistroyResponseDto
    {
        public int UserID { get; set; }
        public string FullName { get; set; }
        public decimal Score { get; set; }
        public decimal ScoreProgress { get; set; }
        public int AnsPillar { get; set; }
        public int TotalQuestion { get; set; }
        public int AnsQuestion { get; set; }
    }

}
