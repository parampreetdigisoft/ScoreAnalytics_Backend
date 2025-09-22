namespace AssessmentPlatform.Models
{
        public class AppLogs
        {
            public int Id { get; set; }
            public string Level { get; set; } = "Info";
            public string Message { get; set; }
            public string Exception { get; set; }
            public DateTime CreatedAt { get; set; } = DateTime.Now;
        }
}
