namespace AssessmentPlatform.Dtos.chatDto
{
    public class PillarLiveSignalCard
    {
        public int PillarId { get; set; }

        public string ImagePath { get; set; } = string.Empty;

        public string PillarName { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string Urgency { get; set; } = string.Empty;

        public string Color { get; set; } = string.Empty;

        public string SourceUrl { get; set; } = string.Empty;
    }

    public class PillarLiveSignalsResult
    {
        public string UpdatedAt { get; set; } = string.Empty;

        public string Headline { get; set; } = string.Empty;

        public string SubHeadline { get; set; } = string.Empty;

        public List<PillarLiveSignalCard> Pillars { get; set; } = new();
    }

    public class ChatPillarLiveSignalsResponse
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public PillarLiveSignalsResult Result { get; set; } = new();
    }
}
