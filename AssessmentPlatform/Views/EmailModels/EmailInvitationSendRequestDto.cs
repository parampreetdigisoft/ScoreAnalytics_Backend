namespace AssessmentPlatform.Views.EmailModels
{
    public class EmailInvitationSendRequestDto
    {
        public EmailInvitationSendRequestDto()
        {
            Title = "";
            ResetPasswordUrl = "";
            ApiUrl = "";
            MsgText = "";
            ApplicationUrl = "";
            DescriptionAboutBtnText =  "If you'd like to change your password, please click the button below. \nOtherwise, you can safely ignore this email and log in directly to continue using the app.";
            BtnText = "Reset Password";
            IsShowBtnText = true;
            IsLoginBtn = true;
            Mail = "";
        }
        public string Title { get; set; }
        public string ResetPasswordUrl { get; set; }
        public string ApiUrl { get; set; }
        public string MsgText { get; set; }
        public string ApplicationUrl { get; set; }
        public string DescriptionAboutBtnText { get; set; }
        public string BtnText { get; set; }
        public bool IsShowBtnText { get; set; } = true;
        public bool IsLoginBtn { get; set; } = true;
        public string Mail { get; set; }
    }
}
