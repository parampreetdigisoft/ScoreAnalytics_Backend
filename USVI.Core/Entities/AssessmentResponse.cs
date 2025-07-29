
namespace USVI.Core.Entities
{
    public class AssessmentResponse
    {
        public int ResponseID { get; set; } //INT PRIMARY KEY AUTO_INCREMENT, 
        public int AssessmentID { get; set; } //INT NOT NULL,
        public int QuestionID { get; set; } //INT NOT NULL, 
        public int Score { get; set; } //ENUM('4', '3', '2', '1', '0', 'N/A', 'Unknown') NOT NULL,
        public string Justification { get; set; } = null!; //TEXT NOT NULL, 
        public DateTime CreatedAt { get; set; } //DATETIME DEFAULT CURRENT_TIMESTAMP,
                                                
        //FOREIGN KEY(AssessmentID) REFERENCES CityAssessments(AssessmentID),                                         
        //FOREIGN KEY(QuestionID) REFERENCES PillarQuestions(QuestionID)
    }
}