using Microsoft.Data.SqlClient;

namespace AssessmentPlatform.Common.Middlware
{
    public class ErrorLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorLoggingMiddleware> _logger;
        private readonly IConfiguration _configuration;

        public ErrorLoggingMiddleware(RequestDelegate next, ILogger<ErrorLoggingMiddleware> logger, IConfiguration configuration)
        {
            _next = next;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context); // continue pipeline
            }
            catch (Exception ex)
            {
                // 1️⃣ Log to console / default provider
                _logger.LogError(ex, "Unhandled exception at {Path}", context.Request.Path);

                // 2️⃣ Log to database
                await LogToDatabaseAsync(ex, context);

                // 3️⃣ Send response
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Internal Server Error",
                    details = ex.Message
                });
            }
        }
        private async Task LogToDatabaseAsync(Exception ex, HttpContext context)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            var sql = @"INSERT INTO AppLogs (Level, Message, Exception, Path, CreatedAt)
                    VALUES (@Level, @Message, @Exception, @Path, GETUTCDATE());";

            using var conn = new SqlConnection(connectionString);
            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@Level", "Error");
            cmd.Parameters.AddWithValue("@Message", ex.Message ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Exception", ex.ToString());
            cmd.Parameters.AddWithValue("@Path", context.Request.Path.Value ?? "");

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
