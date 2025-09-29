namespace AssessmentPlatform.Dtos.PaymentDto
{
    public class CheckoutSessionResponse
    {
        public string SessionId { get; set; }
        public string CustomerId { get; set; }
        public string Currency { get; set; }
        public decimal AmountTotal { get; set; }
        public DateTime Created { get; set; }
    }
}
