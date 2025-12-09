namespace AssessmentPlatform.Common.Models.settings
{
    public class AppSettings
    {
        public string ApplicationUrl { get; init; }
        public string PublicApplicationUrl { get; init; }
        public string ApplicationPath { get; set; }
        public int LinkValidHours { get; set; }
        public string AdminMail { get; init; }
        public string ApiUrl { get; set; }
        public string ApplicationInfoMail { get; set; }
        public int OTPExpiryValidMinutes { get; set; }
        public string AiUrl { get; set; }
    }
}
