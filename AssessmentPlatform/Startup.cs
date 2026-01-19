using AssessmentPlatform.Common.DI;
using AssessmentPlatform.Common.Implementation;
using AssessmentPlatform.Common.Middlware;
using AssessmentPlatform.Common.Models.settings;
using AssessmentPlatform.Data;
using AssessmentPlatform.Enums;
using AssessmentPlatform.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;
using System.Text;

namespace AssessmentPlatform
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // Controllers
            services.AddControllers();

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(
                    Configuration.GetConnectionString("DefaultConnection"),
                    sqlOptions =>
                    {
                        sqlOptions.CommandTimeout(180);
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,             // retry 3 times
                            maxRetryDelay: TimeSpan.FromSeconds(5),
                            errorNumbersToAdd: null
                        );
                    }
                )
            );

            // Dependency Injection for Services
            services.AddHttpContextAccessor();

            services.AddHttpClient<HttpService>(client =>
            {
                client.Timeout = TimeSpan.FromHours(3); // supports very long API calls
            });

               ServiceRegistration.AddDependencyInjection(services);     

            services.AddCors(options =>
            {
                options.AddPolicy("AllowAngularApp", builder =>
                {
                    builder.WithOrigins(
                        "http://localhost:4200",
                        "http://veridianurbansystems.com",
                        "https://veridianurbansystems.com",
                        "http://portal.veridianurbansystems.com",
                        "https://portal.veridianurbansystems.com"
                        )
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });
            var appSettingsSection = Configuration.GetSection("AppSettings");
            services.Configure<AppSettings>(appSettingsSection);

            var mailSettingsSection = Configuration.GetSection("Mail");
            services.Configure<Mailsetting>(mailSettingsSection);

            var jwtSettingSection = Configuration.GetSection("Jwt");
            services.Configure<JwtSetting>(jwtSettingSection);
            var stripeSettingSection = Configuration.GetSection("Stripe");
            services.Configure<StripeSetting>(stripeSettingSection);

            var jwtSetting = jwtSettingSection.Get<JwtSetting>();
            var key = Encoding.ASCII.GetBytes(jwtSetting.Key);

            // Swagger Configuration
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = "AssessmentPlatformApi",
                    Version = "v1"
                });

                c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                    Description = "Enter 'Bearer' [space] and then your valid token.\nExample: Bearer abc123xyz"
                });

                c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
                {
                    {
                        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                        {
                            Reference = new Microsoft.OpenApi.Models.OpenApiReference
                            {
                                Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        new string[] { }
                    }
                });
            });
            QuestPDF.Settings.License = LicenseType.Community;

            services.AddControllersWithViews();
            services.AddMvc().AddSessionStateTempDataProvider();
            // JWT Authentication
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(x =>
                {
                    x.RequireHttpsMetadata = false;
                    x.SaveToken = true;
                    x.TokenValidationParameters = new TokenValidationParameters
                    {
                        NameClaimType = System.Security.Claims.ClaimTypes.Name,
                        RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidIssuer = jwtSetting.Issuer,
                        ValidAudience = jwtSetting.Audience,
                        ClockSkew = TimeSpan.Zero
                    };
                });

            services.AddAuthorization(options =>
            {
                // CityUser with Standard or higher
                options.AddPolicy("PaidCityUserOnly", policy =>
                {
                    policy.RequireRole("CityUser");
                    policy.RequireAssertion(context =>
                    {
                        var tier = context.User.FindFirst("Tier")?.Value;

                        return tier == TieredAccessPlan.Standard.ToString() ||
                               tier == TieredAccessPlan.Premium.ToString() || tier == TieredAccessPlan.Basic.ToString();
                    });
                });

                options.AddPolicy("AdminOnly", policy =>
                    policy.RequireRole("Admin"));

                options.AddPolicy("StaffOnly", policy =>
                    policy.RequireRole(UserRole.Admin.ToString(),UserRole.Analyst.ToString(), UserRole.Evaluator.ToString()));
            });

        }


        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseMiddleware<ErrorLoggingMiddleware>();
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Enable middleware to serve generated Swagger as JSON endpoint.
            app.UseSwagger();
            app.UseStaticFiles();

            // Enable middleware to serve Swagger UI (HTML, JS, CSS, etc.)
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "AssessmentPlatformApi v1");
                c.RoutePrefix = string.Empty; // Swagger UI at root URL (e.g. https://localhost:5001/)
            });
            app.UseCors("AllowAngularApp");

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

    }
}
