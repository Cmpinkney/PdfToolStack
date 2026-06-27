using Microsoft.Extensions.Options;
using PdfToolStack.API.Configuration;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;

namespace PdfToolStack.API.Middleware
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ProcessingOptions _options;
        private readonly ILogger<RateLimitingMiddleware> _logger;

        // Keyed by "user:{userId}" or "ip:{ipAddress}"
        private static readonly ConcurrentDictionary<string,
            (int Count, DateTime WindowStart)> _pdfCounts = new();
        private static readonly ConcurrentDictionary<string,
            (int Count, DateTime WindowStart)> _aiCounts = new();

        private static readonly Timer _evictionTimer = new Timer(
            EvictStaleBuckets, null,
            TimeSpan.FromHours(1),
            TimeSpan.FromHours(1));

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
            // Never rate-limit CORS preflight requests
            if (HttpMethods.IsOptions(context.Request.Method))
            {
                await _next(context);
                return;
            }

            var path = context.Request.Path;

            // Build a stable key — prefer userId over IP
            var userId = context.User?.FindFirst("sub")?.Value
                ?? context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var rateLimitKey = !string.IsNullOrEmpty(userId)
                ? $"user:{userId}"
                : $"ip:{context.Connection.RemoteIpAddress ?? IPAddress.Loopback}";

            // Authenticated users get higher limits
            bool isAuthenticated = !string.IsNullOrEmpty(userId);
            int pdfLimit = isAuthenticated
                ? _options.AuthenticatedMaxRequestsPerHour
                : _options.MaxRequestsPerHour;
            int aiLimit = isAuthenticated
                ? _options.AuthenticatedAiMaxRequestsPerHour
                : _options.AiMaxRequestsPerHour;

            if (path.StartsWithSegments("/api/pdf"))
            {
                if (IsRateLimited(_pdfCounts, rateLimitKey, pdfLimit))
                {
                    await TooManyRequests(context, rateLimitKey, "PDF");
                    return;
                }
            }
            else if (path.StartsWithSegments("/api/ai") ||
                     path.StartsWithSegments("/api/excel-ai"))
            {
                if (IsRateLimited(_aiCounts, rateLimitKey, aiLimit))
                {
                    await TooManyRequests(context, rateLimitKey, "AI");
                    return;
                }
            }

            await _next(context);
        }

        private static bool IsRateLimited(
            ConcurrentDictionary<string,
                (int Count, DateTime WindowStart)> bucket,
            string key,
            int maxPerHour)
        {
            var now = DateTime.UtcNow;

            bucket.AddOrUpdate(
                key,
                (1, now),
                (_, existing) =>
                {
                    if ((now - existing.WindowStart).TotalHours >= 1)
                        return (1, now);
                    return (existing.Count + 1, existing.WindowStart);
                });

            return bucket[key].Count > maxPerHour;
        }

        private async Task TooManyRequests(
            HttpContext context, string key, string endpoint)
        {
            _logger.LogWarning(
                "Rate limit exceeded for {Key} on {Endpoint} endpoint",
                key, endpoint);

            context.Response.StatusCode =
                (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers.Append("Retry-After", "3600");

            await context.Response.WriteAsJsonAsync(new
            {
                error = "Too many requests. Please try again in an hour.",
                statusCode = 429
            });
        }

        private static void EvictStaleBuckets(object? state)
        {
            var cutoff = DateTime.UtcNow.AddHours(-2);

            foreach (var key in _pdfCounts.Keys)
                if (_pdfCounts.TryGetValue(key, out var val) && val.WindowStart < cutoff)
                    _pdfCounts.TryRemove(key, out _);

            foreach (var key in _aiCounts.Keys)
                if (_aiCounts.TryGetValue(key, out var val) && val.WindowStart < cutoff)
                    _aiCounts.TryRemove(key, out _);
        }
    }
}