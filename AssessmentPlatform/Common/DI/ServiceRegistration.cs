using AssessmentPlatform.Backgroundjob;
using AssessmentPlatform.Backgroundjob.logging;
using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Common.Interface;
using AssessmentPlatform.IServices;
using AssessmentPlatform.Services;

namespace AssessmentPlatform.Common.DI
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddDependencyInjection(this IServiceCollection services)
        {
            services.AddHostedService<ChannelWorker>();
            //services.AddHostedService<AiJobService>();
            services.AddScoped<Download>();
            services.AddHostedService<LogWorker>();
            // Channels
            services.AddSingleton<ChannelService>();
            services.AddSingleton<LogChannelService>();
            services.AddScoped<IAppLogger, AppLogger>();


            services.AddScoped<IAIAnalyzeService, AIAnalyzeService>();
            services.AddScoped<IQuestionService, QuestionService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IPillarService, PillarService>();
            services.AddScoped<IAssessmentResponseService, AssessmentResponseService>();
            services.AddScoped<ICityService, CityService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ICityUserService, CityUserService>();
            services.AddScoped<IPaymentService, PaymentService>();
            services.AddScoped<IPublicService, PublicService>();
            services.AddScoped<IKpiService, KpiService>();
            services.AddScoped<IAIComputationService, AIComputationService>();
            services.AddScoped<ICommonService, CommonService>();
            services.AddScoped<IBlogService, BlogService>();
            return services;
        }
    }
}
