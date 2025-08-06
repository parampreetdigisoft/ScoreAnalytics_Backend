namespace AssessmentPlatform.Common.Interface
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string toEmail, string subject, string viewNamePath, object model);
        
    }
}
