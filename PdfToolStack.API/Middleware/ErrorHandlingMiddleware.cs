using System.Net;
using System.Text.Json;

namespace PdfToolStack.API.Middleware
{
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;

        public ErrorHandlingMiddleware(
            RequestDelegate next,
            ILogger<ErrorHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unhandled exception for request {Method} {Path}. ExceptionType: {ExceptionType}, ExceptionMessage: {ExceptionMessage}, InnerException: {InnerException}, StackTrace: {StackTrace}",
                    context.Request.Method,
                    context.Request.Path,
                    ex.GetType().FullName,
                    ex.Message,
                    ex.InnerException?.ToString(),
                    ex.StackTrace);

                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(
            HttpContext context,
            Exception exception)
        {
            context.Response.ContentType = "application/json";

            var (statusCode, message) = exception switch
            {
                NotSupportedException => (
                    HttpStatusCode.BadRequest,
                    "The requested operation is not supported."),
                OperationCanceledException => (
                    HttpStatusCode.RequestTimeout,
                    "The request timed out."),
                _ => (
                    HttpStatusCode.InternalServerError,
                    "An unexpected error occurred.")
            };

            context.Response.StatusCode = (int)statusCode;

            var response = new
            {
                error = message,
                statusCode = (int)statusCode
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(response));
        }
    }
}
