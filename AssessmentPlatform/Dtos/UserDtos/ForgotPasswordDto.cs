namespace AssessmentPlatform.Dtos.UserDtos
{
    public class ForgotPasswordDto
    {
        public string Email { get; set; }
    }
    public class ChangedPasswordDto
    {
        public string PasswordToken { get; set; }
        public string Password { get; set; }
    }
}
