
namespace USVI.Core.Entities
{
    class Subscription
    {
        public int SubscriptionID { get; set; } //INT PRIMARY KEY AUTO_INCREMENT, 
        public int CityID { get; set; } //INT NOT NULL,
        public int Tier { get; set; } //ENUM('Basic', 'Standard', 'Premium') NOT NULL,
        public DateTime StartDate { get; set; } //DATE NOT NULL, 
        public DateTime EndDate { get; set; }
    }
}