namespace AssessmentPlatform.Common.Models.settings
{
    public class AppSettings
    {
        public string ApplicationUrl { get; init; }
        public string PublicApplicationUrl { get; init; }
        public string Host { get; init; }
        public bool EnableSsl { get; init; }
        public string UserName { get; init; }
        public string Password { get; init; }
        public int Port { get; init; }
        public string Secret { get; init; }
        public string ApplicationPath { get; set; }
        public int LinkValidHours { get; set; }
        public string AdminEmail { get; init; }
        public string ApiUrl { get; set; }
        public string ApplicationInfoMail { get; set; }
        public int OTPExpiryValidMinutes { get; set; }
    }
}
