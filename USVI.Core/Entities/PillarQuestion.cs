
namespace USVI.Core.Entities
{
    public class PillarQuestion
    {
        public int QuestionID { get; set; } //INT PRIMARY KEY AUTO_INCREMENT, 
        public int PillarID { get; set; } //INT NOT NULL,
        public string QuestionText { get; set; } = null!; //TEXT NOT NULL, 
        public int DisplayOrder { get; set; } //INT,
        
        //FOREIGN KEY(PillarID) REFERENCES Pillars(PillarID)
    }
}
