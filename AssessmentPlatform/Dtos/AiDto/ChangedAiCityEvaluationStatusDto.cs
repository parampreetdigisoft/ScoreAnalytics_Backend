namespace AssessmentPlatform.Dtos.AiDto
{
    public class ChangedAiCityEvaluationStatusDto
    {
        public int CityID { get; set; }
        public bool IsVerified { get; set; }
    }

    public class RegenerateAiSearchDto
    {
        public int CityID { get; set; }
        public bool CityEnable { get; set; }
        public bool PillarEnable { get; set; }
        public bool QuestionEnable { get; set; }
        public List<int> ViewerUserIDs { get; set; } = new();
    }
    public class AddCommentDto
    {
        public int CityID { get; set; }
        public string Comment { get; set; }

    }
}
