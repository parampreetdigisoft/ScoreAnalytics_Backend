using AssessmentPlatform.Common.Interface;
using AssessmentPlatform.Common.Models.settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace AssessmentPlatform.Common.Implementation
{
    public class EmailService : IEmailService
    {
        private readonly Mailsetting _smtpSettings;
        private readonly IRazorViewEngine _razorViewEngine;
        private readonly ITempDataProvider _tempDataProvider;
        private readonly IServiceProvider _serviceProvider;

        public EmailService(IOptions<Mailsetting> smtpSettings, ITempDataProvider tempDataProvider, IRazorViewEngine razorViewEngine, IServiceProvider serviceProvider)
        {
            _smtpSettings = smtpSettings.Value;
            _tempDataProvider = tempDataProvider;
            _razorViewEngine = razorViewEngine;
            _serviceProvider = serviceProvider;
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string viewNamePath, object model)
        {
            try
            {
                using var client = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port)
                {
                    UseDefaultCredentials=false,
                    Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password),
                    EnableSsl = _smtpSettings.EnableSsl
                };
                var htmlContent = await RenderRazorViewToStringAsync(viewNamePath, model);
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_smtpSettings.SenderEmail, _smtpSettings.SenderName),
                    Subject = subject,
                    Body = htmlContent,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);

                await Task.Run(() => client.Send(mailMessage));

                return true; 
            }
            catch (Exception ex)
            {
                return false; 
            }
        }
        private async Task<string> RenderRazorViewToStringAsync(string viewName, object model)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.RequestServices = _serviceProvider;

            var actionContext = new ActionContext(
                httpContext,
                new RouteData(),
                new ControllerActionDescriptor()
            );

            var viewResult = _razorViewEngine.GetView(executingFilePath: null, viewName, isMainPage: true);

            if (!viewResult.Success)
                throw new InvalidOperationException($"View {viewName} not found.");

            var viewDictionary = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
                Model = model
            };


            using var stringWriter = new StringWriter();
            var viewContext = new ViewContext(
                actionContext,
                viewResult.View,
                viewDictionary,
                new TempDataDictionary(actionContext.HttpContext, _tempDataProvider),
                stringWriter,
                new HtmlHelperOptions()
            );

            await viewResult.View.RenderAsync(viewContext);
            return stringWriter.ToString();
        }


    }
}
