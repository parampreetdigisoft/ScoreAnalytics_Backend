using AssessmentPlatform.Enums;


namespace AssessmentPlatform.Models
{
    public enum PaymentStatus:byte { Pending = 0, Succeeded = 1, Failed = 2 }

    public class PaymentRecord
    {
        public Guid PaymentRecordID { get; set; }
        public string CheckoutSessionId { get; set; } = default!;
        public int UserID { get; set; }
        public double? Amount { get; set; }
        public TieredAccessPlan Tier { get; set; } = TieredAccessPlan.Pending;
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime ExpiredAt { get; set; } = DateTime.Now.AddYears(1);
        public User User { get; set; }
    }
}
