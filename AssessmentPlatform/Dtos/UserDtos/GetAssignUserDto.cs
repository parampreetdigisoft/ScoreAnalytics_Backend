namespace AssessmentPlatform.Dtos.UserDtos
{
    public class GetAssignUserDto : UserIdDto
    {
        public int? SearchedUserID { get; set; }
    }
    public class UserIdDto
    {
        public int UserID { get; set; }
    }
}
