using AssessmentPlatform.Models;

namespace AssessmentPlatform.Dtos.PaymentDto
{
    public class VerifySessionResponse
    {
        public string SessionId { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public string Currency { get; set; }
        public double AmountTotal { get; set; }
    }
}
