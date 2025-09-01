namespace AssessmentPlatform.IServices
{
    public interface IAppLogger
    {
        Task LogAsync(string level, string message, Exception ex = null);
    }
}
