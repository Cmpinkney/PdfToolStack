using Microsoft.Extensions.Options;
using PdfToolStack.API.Configuration;
using System.Collections.Concurrent;
using System.Net;

namespace PdfToolStack.API.Middleware
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ProcessingOptions _options;
        private readonly ILogger<RateLimitingMiddleware> _logger;

        // Thread-safe dictionary tracking requests per IP
        private static readonly ConcurrentDictionary<string,
            (int Count, DateTime WindowStart)> _requestCounts = new();

        public RateLimitingMiddleware(
            RequestDelegate next,
            IOptions<ProcessingOptions> options,
            ILogger<RateLimitingMiddleware> logger)
        {
            _next = next;
            _options = options.Value;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Only rate limit PDF processing endpoints
            if (!context.Request.Path.StartsWithSegments("/api/pdf"))
            {
                await _next(context);
                return;
            }

            var ipAddress = context.Connection
                .RemoteIpAddress?.ToString() ?? "unknown";

            if (IsRateLimited(ipAddress))
            {
                _logger.LogWarning(
                    "Rate limit exceeded for IP {IpAddress}",
                    ipAddress);

                context.Response.StatusCode =
                    (int)HttpStatusCode.TooManyRequests;

                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Too many requests. " +
                            "Please try again in an hour.",
                    statusCode = 429
                });
                return;
            }

            await _next(context);
        }

        private bool IsRateLimited(string ipAddress)
        {
            var now = DateTime.UtcNow;

            _requestCounts.AddOrUpdate(
                ipAddress,
                (1, now),
                (key, existing) =>
                {
                    // Reset window if an hour has passed
                    if ((now - existing.WindowStart).TotalHours >= 1)
                        return (1, now);

                    return (existing.Count + 1, existing.WindowStart);
                });

            var current = _requestCounts[ipAddress];
            return current.Count > _options.MaxRequestsPerHour;
        }
    }
}
