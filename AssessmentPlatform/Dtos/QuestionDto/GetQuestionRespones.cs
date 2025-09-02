using AssessmentPlatform.Models;

namespace AssessmentPlatform.Dtos.QuestionDto
{
    public class GetQuestionRespones : AddUpdateQuestionDto
    {
        public int DisplayOrder { get; set; }
        public string PillarName { get; set; }
    }
    public class GetQuestionByCityRespones : GetQuestionRespones
    {
        public int AssessmentID { get; set; }
        public int PillarDisplayOrder { get; set; }
    }
    public class GetPillarQuestionByCityRespones 
    {
        public int AssessmentID { get; set; }
        public int UserCityMappingID { get; set; }
        public int PillarDisplayOrder { get; set; }
        public int PillarID { get; set; }
        public string PillarName { get; set; }
        public string Description { get; set; }
        public List<AddUpdateQuestionDto> Questions { get; set; }
    }
}
