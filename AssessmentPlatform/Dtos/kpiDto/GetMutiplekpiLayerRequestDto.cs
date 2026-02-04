namespace AssessmentPlatform.Dtos.kpiDto
{
    public class GetMutiplekpiLayerRequestDto
    {
        public int LayerID { get; set; }
        public List<int> CityIDs { get; set; } 
        public int Year { get; set; } = DateTime.Now.Year;
    }
}
