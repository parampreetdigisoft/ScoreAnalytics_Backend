
namespace USVI.Core.Entities
{
    public class User
    {
        public int UserID { get; set; } //INT PRIMARY KEY AUTO_INCREMENT
        public string FullName { get; set; } = null!; //VARCHAR(150) NOT NULL,
        public string Email { get; set; } = null!; //VARCHAR(150) NOT NULL UNIQUE, 
        public string PasswordHash { get; set; } = null!; //VARCHAR(255) NOT NULL,
        public int Role { get; set; } //ENUM('Admin', 'CityUser') NOT NULL,
        public int CityID { get; set; } //INT, 
        public string Country { get; set; } = null!; //VARCHAR(100), 
        public DateTime CreatedAt { get; set; } //DATETIME DEFAULT CURRENT_TIMESTAMP,
        public DateTime LastLogin { get; set; } //DATETIME, 
        
        //FOREIGN KEY(CityID) REFERENCES Cities(CityID)
    }
}
