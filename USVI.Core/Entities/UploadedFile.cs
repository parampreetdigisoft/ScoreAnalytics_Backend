
namespace USVI.Core.Entities
{
    class UploadedFile
    {
        public int FileID { get; set; } //INT PRIMARY KEY AUTO_INCREMENT, 
        public int AssessmentID { get; set; } //INT NOT NULL,
        public string FileName { get; set; } = null!; //VARCHAR(255) NOT NULL,
        public string FilePath { get; set; } = null!; //TEXT NOT NULL, 
        public DateTime UploadedAt { get; set; } //DATETIME DEFAULT CURRENT_TIMESTAMP,
                                                 
        //FOREIGN KEY(AssessmentID) REFERENCES CityAssessments(AssessmentID)
    }
}