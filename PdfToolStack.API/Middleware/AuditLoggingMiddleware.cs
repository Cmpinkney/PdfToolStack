using System.Diagnostics;
using System.Security.Claims;

namespace PdfToolStack.API.Middleware
{
    public class AuditLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuditLoggingMiddleware> _logger;

        // Map API paths to human-readable tool names
        private static readonly Dictionary<string, string> _toolNames = new()
        {
            ["/api/pdf/process"] = "Process",
            ["/api/pdf/sign"] = "Sign PDF",
            ["/api/pdf/merge"] = "Merge PDF",
            ["/api/pdf/split"] = "Split PDF",
            ["/api/pdf/compress"] = "Compress PDF",
            ["/api/pdf/rotate"] = "Rotate PDF",
            ["/api/pdf/watermark"] = "Watermark PDF",
            ["/api/pdf/flatten"] = "Flatten PDF",
            ["/api/pdf/protect"] = "Protect PDF",
            ["/api/pdf/unlock"] = "Unlock PDF",
            ["/api/pdf/redact"] = "Redact PDF",
            ["/api/pdf/edit"] = "Edit PDF",
            ["/api/pdf/annotate"] = "Annotate PDF",
            ["/api/pdf/compare"] = "Compare PDF",
            ["/api/pdf/number-pages"] = "Number Pages",
            ["/api/pdf/delete-pages"] = "Delete Pages",
            ["/api/pdf/extract-pages"] = "Extract Pages",
            ["/api/pdf/organize"] = "Organize PDF",
            ["/api/pdf/fill-form"] = "Fill Form",
            ["/api/pdf/jpg-to-pdf"] = "JPG to PDF",
            ["/api/pdf/word-to-pdf"] = "Word to PDF",
            ["/api/pdf/ppt-to-pdf"] = "PPT to PDF",
            ["/api/pdf/excel-to-pdf"] = "Excel to PDF",
            ["/api/pdf/batch"] = "Batch Process",
            ["/api/ai/extract-invoice"] = "AI Invoice Extractor",
            ["/api/ai/review-contract"] = "AI Contract Reviewer",
            ["/api/ai/summarize"] = "AI Summarizer",
            ["/api/ai/extract-data"] = "AI Data Extractor",
            ["/api/excel-ai/extract-invoice"] = "AI Invoice Extractor (ExcelToolStack)",
        };

        public AuditLoggingMiddleware(
            RequestDelegate next,
            ILogger<AuditLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Only audit PDF and AI tool endpoints
            var path = context.Request.Path.Value?.ToLowerInvariant()
                ?? string.Empty;

            bool isToolRequest =
                path.StartsWith("/api/pdf/") ||
                path.StartsWith("/api/ai/") ||
                path.StartsWith("/api/excel-ai/");

            // Skip OPTIONS, health checks, page-count, status
            bool isSkipped =
                HttpMethods.IsOptions(context.Request.Method) ||
                path.Contains("health") ||
                path.Contains("page-count") ||
                path.Contains("status") ||
                path.Contains("plans");

            if (!isToolRequest || isSkipped)
            {
                await _next(context);
                return;
            }

            var sw = Stopwatch.StartNew();
            var userId = context.User?
                .FindFirst("sub")?.Value
                ?? context.User?
                .FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? "anonymous";

            var toolName = _toolNames.TryGetValue(path, out var name)
                ? name : path;

            var fileSizeBytes = context.Request.ContentLength ?? 0;

            try
            {
                await _next(context);
                sw.Stop();

                var statusCode = context.Response.StatusCode;
                var success = statusCode >= 200 && statusCode < 300;

                if (success)
                {
                    _logger.LogInformation(
                        "[AUDIT] Tool={Tool} User={UserId} " +
                        "FileSize={FileSizeKb}KB " +
                        "Duration={DurationMs}ms " +
                        "Status={StatusCode}",
                        toolName,
                        userId,
                        fileSizeBytes / 1024,
                        sw.ElapsedMilliseconds,
                        statusCode);
                }
                else
                {
                    _logger.LogWarning(
                        "[AUDIT] Tool={Tool} User={UserId} " +
                        "FileSize={FileSizeKb}KB " +
                        "Duration={DurationMs}ms " +
                        "Status={StatusCode} Failed",
                        toolName,
                        userId,
                        fileSizeBytes / 1024,
                        sw.ElapsedMilliseconds,
                        statusCode);
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "[AUDIT] Tool={Tool} User={UserId} " +
                    "Duration={DurationMs}ms Error={Error}",
                    toolName,
                    userId,
                    sw.ElapsedMilliseconds,
                    ex.Message);
                throw;
            }
        }
    }
}