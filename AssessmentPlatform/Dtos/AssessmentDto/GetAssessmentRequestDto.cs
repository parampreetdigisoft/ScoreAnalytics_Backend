﻿using AssessmentPlatform.Dtos.CommonDto;
using AssessmentPlatform.Models;

namespace AssessmentPlatform.Dtos.AssessmentDto
{
    public class GetAssessmentRequestDto : PaginationRequest
    {
        public int? SubUserID { get; set; } //Means admin or analyst can see result of a user that they has permission
        public int? CityID { get; set; }
        public UserRole? Role { get; set; }
    }
}
