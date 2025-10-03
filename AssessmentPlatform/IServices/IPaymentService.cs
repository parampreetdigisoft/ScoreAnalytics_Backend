using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Dtos.PaymentDto;

namespace AssessmentPlatform.IServices
{
    public interface IPaymentService
    {
        Task<ResultResponseDto<CheckoutSessionResponse>> CreateCheckoutSession(CreateCheckoutSessionDto request);
        Task<ResultResponseDto<VerifySessionResponse>> VerifySession(VerifySessionDto request);
        Task<ResultResponseDto<string>> StripeWebhook();
    }
}
