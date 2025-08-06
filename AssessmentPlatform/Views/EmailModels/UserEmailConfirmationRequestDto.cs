namespace API.Views.EmailModels
{
  public class UserEmailConfirmationRequestDto
  {
    public string Name { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    public string EmailConfirmationUrl { get; set; }
  }
}
