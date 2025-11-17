using AssessmentPlatform.Models;
using DocumentFormat.OpenXml.Office2010.Excel;

namespace AssessmentPlatform.Backgroundjob
{
    public class Download
    {
        private readonly ChannelService channelService;
        public Download(ChannelService channelService) 
        {
            this.channelService = channelService;
        }
        public string Type { get; set; } = string.Empty;
        public int? UserID { get; set; }
        public int? CityID { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Exception { get; set; } = string.Empty;

        public string InsertAnalyticalLayerResults()
        {
            Type = "InsertAnalyticalLayerResults";
            channelService.Write(this);
            return "Execution has been started";
        }

        public Task LogException(string level, string message,string exception)
        {
            Level = level;
            Message = message;
            Exception = exception;
            Type = "LogException";
            channelService.Write(this);
            return Task.CompletedTask;
        }
    }
}
