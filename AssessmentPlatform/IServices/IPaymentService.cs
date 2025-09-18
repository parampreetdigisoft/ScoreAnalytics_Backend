using AssessmentPlatform.Common.Models;

namespace AssessmentPlatform.IServices
{
    public interface IPaymentService
    {
        Task<ResultResponseDto<string>> MakePayment(int userId);
    }
}
