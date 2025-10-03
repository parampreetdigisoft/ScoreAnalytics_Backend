using AssessmentPlatform.Common.Models;
using AssessmentPlatform.Common.Models.settings;
using AssessmentPlatform.Data;
using AssessmentPlatform.Dtos.PaymentDto;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
namespace AssessmentPlatform.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly AppSettings _appSettings;
        private readonly StripeSetting _stripeSetting;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PaymentService(ApplicationDbContext context, IOptions<AppSettings> appSettings, IOptions<StripeSetting> stripeSetting, IAppLogger appLogger, IConfiguration config, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _appLogger = appLogger;
            _stripeSetting = stripeSetting.Value;
             StripeConfiguration.ApiKey = stripeSetting.Value.SecretKey;
            _appSettings = appSettings.Value;
            _httpContextAccessor = httpContextAccessor;
        }
        public async Task<ResultResponseDto<CheckoutSessionResponse>> CreateCheckoutSession(CreateCheckoutSessionDto request)
        {
            try
            {
                var user = await _context.Users.FindAsync(request.UserID);
                if (user == null || user?.Role != UserRole.CityUser) return ResultResponseDto<CheckoutSessionResponse>.Failure(new string[] { "Invalid user" });

                await CancelSessionIfPaymentNotCompelete(request);
                var payment = new PaymentRecord
                {
                    PaymentRecordID = new Guid(),
                    UserID = request.UserID,
                    Tier = request.Tier,
                    PaymentStatus = PaymentStatus.Pending,
                    User = user,
                    ExpiredAt = DateTime.Now.AddYears(1),
                    Amount = request.Amount
                };
                _context.PaymentRecords.Add(payment);
                await _context.SaveChangesAsync();

                var options = new SessionCreateOptions
                {
                    PaymentMethodTypes = new List<string> { "card" },
                    Mode = "payment", // or "payment" for one-time
                    LineItems = new List<SessionLineItemOptions>
                    {
                        new ()
                        {
                            PriceData = new SessionLineItemPriceDataOptions
                            {
                                Currency = "usd",
                                UnitAmount = request.Amount * 100,
                                ProductData = new SessionLineItemPriceDataProductDataOptions
                                {
                                    Name = "UVI App Payment Please don't close the window after clicking on pay button",
                                    Metadata = new Dictionary<string, string>
                                    {
                                        {request.Tier.ToString(), $"Provide {request.Tier.ToString()} Paid subsciption" }
                                    }
                                }
                            },
                            Quantity = 1
                        }
                    },
                    CustomerEmail = user.Email,
                    SuccessUrl = $"{_appSettings.CityUserApplicationUrl}/cityuser/payment/payment-success?session_id={{CHECKOUT_SESSION_ID}}",
                    CancelUrl = $"{_appSettings.CityUserApplicationUrl}/cityuser/payment/payment-cancel",
                    Metadata = new Dictionary<string, string>
                    {
                        { "UserId", user.UserID.ToString() },
                        { "PaymentRecordId", payment.PaymentRecordID.ToString() }
                    }
                };

                var service = new SessionService();
                var session = await service.CreateAsync(options);

                // Save session id on user and payment record for reconciliation
                payment.CheckoutSessionId = session.Id;
                _context.PaymentRecords.Update(payment);
                await _context.SaveChangesAsync();

                var result = new CheckoutSessionResponse
                {
                    SessionId= session.Id,
                    Created = session.Created,
                    AmountTotal = session.AmountTotal ?? 0,
                    Currency = session.Currency,
                    CustomerId = session.CustomerId
                };

                return ResultResponseDto<CheckoutSessionResponse>.Success(result, new string[] { "session id created successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in Create Checkout Session", ex);
                return ResultResponseDto<CheckoutSessionResponse>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task<ResultResponseDto<VerifySessionResponse>> VerifySession(VerifySessionDto request)
        {
            try
            {
                var sessionService = new SessionService();
                var session = await sessionService.GetAsync(request.SessionId, new SessionGetOptions { Expand = new List<string> { "subscription", "payment_intent" } });

                // find payment record
                var payment = await _context.PaymentRecords.FirstOrDefaultAsync(p => p.CheckoutSessionId == request.SessionId);
                if (payment == null)
                {
                    await _appLogger.LogAsync($"Payment record not found for session {request.SessionId}");
                    return ResultResponseDto<VerifySessionResponse>.Failure(new string[] { $"Payment record not found for session {request.SessionId}" });
                }

                // If stripe says paid or subscription active, mark succeeded (idempotent)
                var paid = session.PaymentStatus == "paid" || session.Status == "complete";
                if (!paid)
                {
                    return ResultResponseDto<VerifySessionResponse>.Failure(new string[] { $"Payment record is not succeded, current status is {session.PaymentStatus}" });
                }

                // idempotent update: only change if pending
                payment.PaymentStatus = PaymentStatus.Succeeded;
                var user = await _context.Users.FindAsync(request.UserID);
                if (user != null && user.Role == UserRole.CityUser)
                {
                    user.Tier = payment.Tier;
                    _context.Users.Update(user);
                }
                else
                {
                    return ResultResponseDto<VerifySessionResponse>.Failure(new string[] { $"Invalid user" });
                }
                _context.PaymentRecords.Update(payment);

                await _context.SaveChangesAsync();

                var result = new VerifySessionResponse
                {
                    PaymentStatus = payment.PaymentStatus,
                    AmountTotal = payment.Amount.GetValueOrDefault(),
                    Currency ="USD",
                    SessionId = request.SessionId,
                };

                return ResultResponseDto<VerifySessionResponse>.Success(result, new string[] { $"Payment successfully" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in VerifySession", ex);
                return ResultResponseDto<VerifySessionResponse>.Failure(new string[] { "There is an error please try later" });
            }
        }
        public async Task CancelSessionIfPaymentNotCompelete(CreateCheckoutSessionDto request)
        {
            try
            {
                // 🔎 1. Check for existing pending session
                var existingPayment = await _context.PaymentRecords
                    .Where(p => p.UserID == request.UserID && p.PaymentStatus == PaymentStatus.Pending)
                    .OrderByDescending(p => p.CreatedAt) // assuming you have CreatedAt column
                    .FirstOrDefaultAsync();

                if (existingPayment != null && !string.IsNullOrEmpty(existingPayment.CheckoutSessionId))
                {
                    var sessionService = new SessionService();
                    var oldSession = await sessionService.GetAsync(existingPayment.CheckoutSessionId,
                        new SessionGetOptions { Expand = new List<string> { "payment_intent" } });

                    // If PaymentIntent exists → cancel it on Stripe
                    if (oldSession?.PaymentIntentId != null)
                    {
                        var piService = new PaymentIntentService();
                        await piService.CancelAsync(oldSession.PaymentIntentId);
                    }

                    // Update old record as Cancelled
                    existingPayment.PaymentStatus = PaymentStatus.Failed;
                    _context.PaymentRecords.Update(existingPayment);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in CancelSessionIfPaymentNotCompelete", ex);
            }
        }

        public async Task<ResultResponseDto<string>> StripeWebhook()
        {
            try
            {
                var request = _httpContextAccessor.HttpContext?.Request;
                var webhookSecret = _stripeSetting.WebhookSecret;

                if (request == null)
                    return ResultResponseDto<string>.Failure(new[] { "Request body not found" });

                var json = await new StreamReader(request.Body).ReadToEndAsync();

                // Construct the event with signature validation
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    request.Headers["Stripe-Signature"],
                    webhookSecret
                );
                switch (stripeEvent.Type)
                {
                    case EventTypes.CheckoutSessionCompleted: // "checkout.session.completed"
                    case EventTypes.CheckoutSessionAsyncPaymentSucceeded: // "checkout.session.async_payment_succeeded"
                    {
                        var session = stripeEvent.Data.Object as Session;
                        if (session != null &&
                            session.Metadata != null &&
                            session.Metadata.TryGetValue("PaymentRecordId", out var paymentRecordIdStr) &&
                            Guid.TryParse(paymentRecordIdStr, out var paymentId))
                        {
                            var payment = await _context.PaymentRecords
                                .FirstOrDefaultAsync(p => p.PaymentRecordID == paymentId);

                            if (payment != null && payment.PaymentStatus != PaymentStatus.Succeeded)
                            {
                                payment.PaymentStatus = PaymentStatus.Succeeded;

                                var user = await _context.Users.FindAsync(payment.UserID);
                                if (user != null)
                                {
                                    user.Tier = payment.Tier;
                                }
                                await _context.SaveChangesAsync();

                                // TODO: Send confirmation email or perform async background task
                            }
                        }
                        break;
                    }
                }
            }
            catch (StripeException ex)
            {
                await _appLogger.LogAsync("Stripe exception in StripeWebhook", ex);
                return ResultResponseDto<string>.Failure(new[] { "Invalid Stripe payload" });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in StripeWebhook", ex);
                return ResultResponseDto<string>.Failure(new[] { "There was an error, please try later" });
            }
            return ResultResponseDto<string>.Success();
        }
    }
}
