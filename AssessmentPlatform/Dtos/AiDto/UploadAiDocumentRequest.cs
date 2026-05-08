namespace AssessmentPlatform.Dtos.AiDto
{
    public class UploadAiDocumentRequest
    {
        public int? CityID { get; set; }
        public List<IFormFile> Files { get; set; }
        public List<int> PillarIDs { get; set; } 
    }


}
