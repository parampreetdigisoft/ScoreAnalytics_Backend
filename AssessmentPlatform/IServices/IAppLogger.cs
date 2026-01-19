namespace AssessmentPlatform.IServices
{
    public interface IAppLogger
    {
        //Task LogAsync(string message, Exception ex = null);
        Task LogAsync(string message, Exception? ex = null);
        void LogWarning(string message);
        void LogInfo(string message);
    }
}
