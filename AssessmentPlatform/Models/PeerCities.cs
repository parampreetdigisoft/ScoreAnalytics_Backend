namespace AssessmentPlatform.Models
{
    public class CityPeer
    {
        public int CityPeerID { get; set; }
        public int CityID { get; set; }
        public int PeerCityID { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime? UpdatedDate { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}
