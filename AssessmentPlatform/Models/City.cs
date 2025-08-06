namespace AssessmentPlatform.Models
{
    public class City
    {
        public int CityID { get; set; }           
        public string CityName { get; set; }      
        public string PostalCode { get; set; }      
        public string Region { get; set; }       
        public string PerformanceTier { get; set; } 
        public bool IsActive { get; set; }  = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? UpdatedDate { get; set; } 

    }
}
