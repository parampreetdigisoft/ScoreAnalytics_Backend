using AssessmentPlatform.Dtos.PaymentDto;
using AssessmentPlatform.IServices;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace AssessmentPlatform.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PaymentController> _logger;
        private readonly string _clientDomain = "https://your-angular-app.com"; // update
        public PaymentController(IPaymentService paymentService, ILogger<PaymentController> logger, IConfiguration config)
        {
            _paymentService = paymentService;
            _logger = logger;
            _clientDomain = config["ClientDomain"] ?? _clientDomain;
            StripeConfiguration.ApiKey = config["Stripe:SecretKey"];
        }

        [HttpPost]
        [Route("MakePayment")]
        public async Task<IActionResult> MakePayment([FromBody] int userID)
        {
            var res = await _paymentService.MakePayment(userID);
            return Ok(res);
        }

        // 2) Create checkout session
        [HttpPost("create-checkout-session")]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] CreateCheckoutSessionDto req)
        {
            var res = await _paymentService.CreateCheckoutSession(req);
            return Ok(res);
        }

        // 3) Verify endpoint (used by frontend success page)
        [HttpPost("verify-session")]
        public async Task<IActionResult> VerifySession([FromBody] VerifySessionDto request)
        {
            if (string.IsNullOrEmpty(request.SessionId)) return BadRequest("sessionId required");

            var res = await _paymentService.VerifySession(request);
            return Ok(res);
        }

        // 4) Stripe webhook endpoint
        [HttpPost("webhook")]
        public async Task<IActionResult> StripeWebhook()
        {
            var res = await _paymentService.StripeWebhook();
            return Ok(res);
        }
    }
}
