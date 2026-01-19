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
        public bool CityEnable { get; set; }
        public bool PillarEnable { get; set; }
        public bool QuestionEnable { get; set; }
        public string InsertAnalyticalLayerResults(int cityID = 0)
        {
            CityID = cityID;
            Type = "InsertAnalyticalLayerResults";
            channelService.Write(this);
            return "Execution has been started";
        }

        public Task AiResearchByCityId(int cityID , bool cityEnable,bool pillarEnable, bool questionEnable)
        {
            this.CityID = cityID;
            this.CityEnable = cityEnable;
            this.PillarEnable = pillarEnable;
            this.QuestionEnable = questionEnable;
            Type = "AiResearchByCityId";
            channelService.Write(this);
            return Task.CompletedTask;
        }
    }
}
