
namespace USVI.Core.Entities
{
    public class Pillar
    {
        public int PillarID { get; set; } //INT PRIMARY KEY AUTO_INCREMENT, 
        public string PillarName { get; set; } = null!; //VARCHAR(150) NOT NULL,
        public string Description { get; set; } = null!; //TEXT, 
        public int DisplayOrder { get; set; } //INT
    }
}
