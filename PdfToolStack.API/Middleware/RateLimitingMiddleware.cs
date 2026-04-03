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

        // Separate buckets for PDF and AI endpoints
        private static readonly ConcurrentDictionary<string,
            (int Count, DateTime WindowStart)> _pdfCounts = new();
        private static readonly ConcurrentDictionary<string,
            (int Count, DateTime WindowStart)> _aiCounts = new();

        // AI endpoints are much stricter — expensive Claude API calls

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
            var path = context.Request.Path;
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            if (path.StartsWithSegments("/api/pdf"))
            {
                if (IsRateLimited(_pdfCounts, ip, _options.MaxRequestsPerHour))
                {
                    await TooManyRequests(context, ip, "PDF");
                    return;
                }
            }
            else if (path.StartsWithSegments("/api/ai"))
            {
                if (IsRateLimited(_aiCounts, ip, _options.AiMaxRequestsPerHour))
                {
                    await TooManyRequests(context, ip, "AI");
                    return;
                }
            }

            await _next(context);
        }

        private static bool IsRateLimited(
            ConcurrentDictionary<string, (int Count, DateTime WindowStart)> bucket,
            string ip,
            int maxPerHour)
        {
            var now = DateTime.UtcNow;

            bucket.AddOrUpdate(
                ip,
                (1, now),
                (_, existing) =>
                {
                    if ((now - existing.WindowStart).TotalHours >= 1)
                        return (1, now);
                    return (existing.Count + 1, existing.WindowStart);
                });

            return bucket[ip].Count > maxPerHour;
        }

        private async Task TooManyRequests(
            HttpContext context, string ip, string endpoint)
        {
            _logger.LogWarning(
                "Rate limit exceeded for IP {Ip} on {Endpoint} endpoint", ip, endpoint);

            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers.Append("Retry-After", "3600");

            await context.Response.WriteAsJsonAsync(new
            {
                error = "Too many requests. Please try again in an hour.",
                statusCode = 429
            });
        }
    }
}