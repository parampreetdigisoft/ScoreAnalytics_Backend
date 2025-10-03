namespace AssessmentPlatform.Dtos.UserDtos
{
    public class ForgotPasswordDto
    {
        public string Email { get; set; }
    }
    public class ChangedPasswordDto : ConfirmMailDto
    {
        public string Password { get; set; }
    }

    public class ConfirmMailDto
    {
        public string PasswordToken { get; set; }
    }
}
