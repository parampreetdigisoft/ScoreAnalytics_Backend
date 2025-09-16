namespace AssessmentPlatform.Views.EmailModels
{
    public class EmailInvitationSendRequestDto
    {
        public string Title { get; set; }
        public string ResetPasswordUrl { get; set; }
        public string ApiUrl { get; set; }
        public string MsgText { get; set; }
        public string ApplicationUrl { get; set; }
    }
}
