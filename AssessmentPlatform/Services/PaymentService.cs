using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Data;
using AssessmentPlatform.IServices;

namespace AssessmentPlatform.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        public PaymentService(ApplicationDbContext context, IAppLogger appLogger)
        {
            _context = context;
            _appLogger = appLogger;
        }
        public async Task<ResultResponseDto<string>> MakePayment(int userId)
        {
            try
            {

            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in AddCityAsync", ex);
                return ResultResponseDto<string>.Failure(new string[] { "There is an error please try later" });
            }
            return ResultResponseDto<string>.Success();
        }
    }
}
