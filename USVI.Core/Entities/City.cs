
namespace USVI.Core.Entities
{
    public class City
    {
        public int CityID { get; set; }//INT PRIMARY KEY AUTO_INCREMENT, 
        public string CityName { get; set; } = null!; //VARCHAR(150) NOT NULL,
        public string Country { get; set; } = null!; //VARCHAR(100) NOT NULL,
        public int Population { get; set; } //INT, 
        public string Region { get; set; } = null!;//VARCHAR(100), 
        public string MapURL { get; set; } = null!; //TEXT,
        public string LogoURL { get; set; } = null!; //TEXT, 
        public DateTime CreatedAt { get; set; } //DATETIME DEFAULT CURRENT_TIMESTAMP

    }
}
