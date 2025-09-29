using AssessmentPlatform.Enums;

namespace AssessmentPlatform.Dtos.PaymentDto
{
    public class CreateCheckoutSessionDto
    {
        public int UserID { get; set; }
        public TieredAccessPlan Tier { get; set; }
        public int Amount { get; set; }
    }
}
