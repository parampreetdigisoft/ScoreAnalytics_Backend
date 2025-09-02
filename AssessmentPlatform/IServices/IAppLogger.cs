namespace AssessmentPlatform.IServices
{
    public interface IAppLogger
    {
        Task LogAsync(string message, Exception ex = null);
    }
}
