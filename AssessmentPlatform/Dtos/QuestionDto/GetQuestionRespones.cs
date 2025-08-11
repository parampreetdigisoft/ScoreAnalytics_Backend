using AssessmentPlatform.Models;

namespace AssessmentPlatform.Dtos.QuestionDto
{
    public class GetQuestionRespones
    {
        public int QuestionID { get; set; }
        public int PillarID { get; set; }
        public int CityID { get; set; }
        public string QuestionText { get; set; }
        public int DisplayOrder { get; set; }
        public string PillarName { get; set; }
        public string CityName { get; set; }
        public string State { get; set; }
    }
}
