using DocumentFormat.OpenXml.Bibliography;

namespace PeaceEnablers.Models
{
    public class CityDocument
    {
        public int CityDocumentID { get; set; }
        public int CityID { get; set; }
        public int? PillarID { get; set; }
        public string FileName { get; set; }
        public string StoredFileName { get; set; }
        public string FilePath { get; set; }
        public string FileType { get; set; }
        public long? FileSize { get; set; }
        public DocumentProcessingStatus ProcessingStatus { get; set; } = DocumentProcessingStatus.Pending;
        public int? UploadedByUserID { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
    }

    public enum DocumentProcessingStatus
    {
        Pending = 0,
        Processing = 1,
        Completed = 2,
        Failed = 3
    }
}
