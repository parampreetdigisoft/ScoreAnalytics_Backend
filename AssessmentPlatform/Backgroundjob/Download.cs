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

        public string InsertAnalyticalLayerResults()
        {
            Type = "InsertAnalyticalLayerResults";
            channelService.Write(this);
            return "Worked has been started";
        }
    }
}
