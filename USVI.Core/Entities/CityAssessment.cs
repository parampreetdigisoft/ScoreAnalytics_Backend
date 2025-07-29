
namespace USVI.Core.Entities
{
    public class CityAssessment
    {
        public int AssessmentID { get; set; } //INT PRIMARY KEY AUTO_INCREMENT, 
        public int CityID { get; set; } //INT NOT NULL,
        public int AssessmentYear { get; set; } //INT NOT NULL, 
        public int CreatedBy { get; set; } //INT,
        public int SubscriptionTier { get; set; } //ENUM('Basic', 'Standard', 'Premium') NOT NULL,
        public DateTime CreatedAt { get; set; } //DATETIME DEFAULT CURRENT_TIMESTAMP, 

        //FOREIGN KEY(CityID) REFERENCES Cities(CityID), 
        //    FOREIGN KEY(CreatedBy) REFERENCES Users(UserID)
    }
}
