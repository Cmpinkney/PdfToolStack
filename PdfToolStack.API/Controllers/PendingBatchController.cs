using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PdfToolStack.Application.DTOs;
using PdfToolStack.Application.Services;
using PdfToolStack.Domain.Entities;
using PdfToolStack.Domain.Enums;
using PdfToolStack.Infrastructure.Data;
using PdfToolStack.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace PdfToolStack.API.Controllers
{
    [ApiController]
    [Route("api/pending-batch")]
    public class PendingBatchController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IBlobStorageService _blobStorage;
        private readonly IPdfService _pdfService;
        private readonly ILogger<PendingBatchController> _logger;

        public PendingBatchController(
            AppDbContext db,
            IBlobStorageService blobStorage,
            IPdfService pdfService,
            ILogger<PendingBatchController> logger)
        {
            _db = db;
            _blobStorage = blobStorage;
            _pdfService = pdfService;
            _logger = logger;
        }

        [HttpPost]
        [AllowAnonymous]
        [RequestSizeLimit(524288000)]
        public async Task<ActionResult<PendingBatchCreateResponse>> Create(
            [FromForm] List<IFormFile> files,
            [FromQuery] string toolType,
            CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            _logger.LogInformation(
                "Create pending batch request received. ToolType={ToolType}. FileCount={FileCount}. Files={Files}",
                toolType,
                files?.Count ?? 0,
                files == null
                    ? string.Empty
                    : string.Join(", ", files.Select(f => $"{f.FileName} ({f.Length} bytes)")));

            if (files == null || files.Count == 0)
                return BadRequest(new { error = "No files provided." });

            if (files.Count > 20)
                return BadRequest(new { error = "Maximum 20 files per batch." });

            if (!Enum.TryParse<ToolType>(toolType, ignoreCase: true, out var parsedToolType))
                return BadRequest(new { error = $"Invalid tool type: {toolType}" });

            var fileNames = new List<string>();
            var blobReferences = new List<string>();

            try
            {
                foreach (var file in files)
                {
                    if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
                        return BadRequest(new { error = "Only PDF files are supported." });

                    using var stream = new MemoryStream();
                    await file.CopyToAsync(stream, cancellationToken);
                    var blobUrl = await _blobStorage.UploadAsync(
                        stream.ToArray(),
                        $"pending-batches/{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}",
                        cancellationToken);

                    fileNames.Add(file.FileName);
                    blobReferences.Add(blobUrl);
                }

                var pending = new PendingBatchJob
                {
                    UserId = userId,
                    PendingAccessToken = GenerateAccessToken(),
                    ToolType = parsedToolType,
                    FileCount = files.Count,
                    OriginalFileNames = JsonSerializer.Serialize(fileNames),
                    StoredFileReferences = JsonSerializer.Serialize(blobReferences),
                    Status = PendingBatchStatus.PendingPayment,
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
                };

                _db.PendingBatchJobs.Add(pending);
                await _db.SaveChangesAsync(cancellationToken);

                return Ok(new PendingBatchCreateResponse
                {
                    PendingBatchId = pending.PendingBatchId,
                    PendingAccessToken = pending.PendingAccessToken,
                    FileCount = pending.FileCount,
                    ExpiresAtUtc = pending.ExpiresAtUtc
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(
                    ex,
                    "Pending batch create failed because required infrastructure is not available. ToolType={ToolType}. FileCount={FileCount}",
                    toolType,
                    files.Count);

                foreach (var blobReference in blobReferences)
                {
                    await _blobStorage.DeleteAsync(blobReference, cancellationToken);
                }

                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    error = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Pending batch create failed. ToolType={ToolType}. FileCount={FileCount}",
                    toolType,
                    files.Count);

                foreach (var blobReference in blobReferences)
                {
                    await _blobStorage.DeleteAsync(blobReference, cancellationToken);
                }

                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "Pending batch could not be saved.",
                    detail = ex.Message
                });
            }
        }

        [HttpGet("{pendingBatchId:guid}")]
        [AllowAnonymous]
        public async Task<ActionResult<PendingBatchStatusResponse>> Get(
            Guid pendingBatchId,
            [FromQuery] string? token,
            CancellationToken cancellationToken)
        {
            var pending = await GetAccessiblePendingBatchAsync(pendingBatchId, token, cancellationToken);
            if (pending == null)
                return NotFound();

            ExpireIfNeeded(pending);
            await ClaimForCurrentUserIfPossibleAsync(pending, token);
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(await ToStatusResponseAsync(pending, cancellationToken));
        }

        [HttpPost("{pendingBatchId:guid}/claim")]
        public async Task<ActionResult<PendingBatchStatusResponse>> Claim(
            Guid pendingBatchId,
            [FromQuery] string token,
            [FromQuery] string? userId,
            CancellationToken cancellationToken)
        {
            var pending = await GetAccessiblePendingBatchAsync(pendingBatchId, token, cancellationToken);
            if (pending == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(GetUserId()))
                return Unauthorized();

            ExpireIfNeeded(pending);
            await ClaimForCurrentUserIfPossibleAsync(pending, token);
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(await ToStatusResponseAsync(pending, cancellationToken));
        }

        [HttpPost("{pendingBatchId:guid}/process")]
        public async Task<IActionResult> Process(
            Guid pendingBatchId,
            [FromQuery] string? token,
            CancellationToken cancellationToken)
        {
            var pending = await GetAccessiblePendingBatchAsync(pendingBatchId, token, cancellationToken);
            if (pending == null)
                return NotFound();

            ExpireIfNeeded(pending);
            await ClaimForCurrentUserIfPossibleAsync(pending, token);

            if (IsBlockedFromProcessing(pending))
            {
                await _db.SaveChangesAsync(cancellationToken);
                return Conflict(new
                {
                    error = "This pending batch can no longer be processed.",
                    status = pending.Status.ToString()
                });
            }

            var isAuthorized = await IsAuthorizedForProcessingAsync(pending, cancellationToken);

            if (!isAuthorized)
            {
                await _db.SaveChangesAsync(cancellationToken);
                return StatusCode(StatusCodes.Status402PaymentRequired, new
                {
                    error = "Batch unlock payment or Pro subscription has not been verified."
                });
            }

            if (!await TryMarkProcessingAsync(pending, cancellationToken))
            {
                await _db.Entry(pending).ReloadAsync(cancellationToken);
                return Conflict(new
                {
                    error = "This pending batch is already being processed or can no longer be processed.",
                    status = pending.Status.ToString()
                });
            }

            var fileNames = ReadJsonList(pending.OriginalFileNames);
            var blobReferences = ReadJsonList(pending.StoredFileReferences);
            var results = new List<(string FileName, byte[] Bytes, string? Error)>();

            try
            {
                for (var i = 0; i < blobReferences.Count; i++)
                {
                    var fileName = i < fileNames.Count ? fileNames[i] : $"file-{i + 1}.pdf";
                    var bytes = await _blobStorage.DownloadAsync(blobReferences[i], cancellationToken);
                    if (bytes == null || bytes.Length == 0)
                    {
                        results.Add((fileName, Array.Empty<byte>(), "Stored file could not be loaded."));
                        continue;
                    }

                    var response = await _pdfService.ProcessAsync(new ProcessRequest
                    {
                        ToolType = pending.ToolType,
                        FileBytes = bytes,
                        FileName = fileName,
                        FileSizeBytes = bytes.Length
                    }, cancellationToken);

                    results.Add(response.IsSuccess
                        ? (fileName, response.OutputBytes!, null)
                        : (fileName, Array.Empty<byte>(), response.ErrorMessage));
                }

                var zipBytes = BuildZip(results);

                pending.Status = PendingBatchStatus.Completed;
                pending.IsUsed = true;
                pending.CompletedAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);

                foreach (var blobReference in blobReferences)
                {
                    await _blobStorage.DeleteAsync(blobReference, cancellationToken);
                }

                return File(zipBytes, "application/zip",
                    $"pdftoolstack_batch_{pending.ToolType.ToString().ToLower()}.zip");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pending batch processing failed for {PendingBatchId}", pendingBatchId);
                pending.Status = PendingBatchStatus.Failed;
                pending.ErrorMessage = GetErrorMessage(ex);
                await _db.SaveChangesAsync(cancellationToken);
                return UnprocessableEntity(new { error = pending.ErrorMessage });
            }
        }

        private async Task<PendingBatchJob?> GetAccessiblePendingBatchAsync(
            Guid pendingBatchId,
            string? token,
            CancellationToken cancellationToken)
        {
            var userId = GetUserId();

            var pending = await _db.PendingBatchJobs
                .FirstOrDefaultAsync(x => x.PendingBatchId == pendingBatchId, cancellationToken);

            if (pending == null)
                return null;

            if (!string.IsNullOrWhiteSpace(userId) &&
                !string.IsNullOrWhiteSpace(pending.UserId) &&
                pending.UserId == userId)
                return pending;

            if (!string.IsNullOrWhiteSpace(token) && TokensEqual(pending.PendingAccessToken, token))
            {
                return pending;
            }

            return null;
        }

        private string GetUserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
            User.FindFirst("sub")?.Value ??
            string.Empty;

        private static void ExpireIfNeeded(PendingBatchJob pending)
        {
            if (pending.Status is PendingBatchStatus.Completed or PendingBatchStatus.Failed)
                return;

            if (pending.ExpiresAtUtc <= DateTime.UtcNow)
                pending.Status = PendingBatchStatus.Expired;
        }

        private async Task ClaimForCurrentUserIfPossibleAsync(PendingBatchJob pending, string? token)
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId) ||
                !string.IsNullOrWhiteSpace(pending.UserId) ||
                string.IsNullOrWhiteSpace(token) ||
                !TokensEqual(pending.PendingAccessToken, token))
                return;

            pending.UserId = userId;
            await Task.CompletedTask;
        }

        private static bool IsBlockedFromProcessing(PendingBatchJob pending) =>
            pending.IsUsed ||
            pending.Status is PendingBatchStatus.Expired
                or PendingBatchStatus.Failed
                or PendingBatchStatus.Completed
                or PendingBatchStatus.Processing;

        private async Task<bool> TryMarkProcessingAsync(
            PendingBatchJob pending,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var updated = await _db.PendingBatchJobs
                .Where(x =>
                    x.PendingBatchId == pending.PendingBatchId &&
                    !x.IsUsed &&
                    x.ExpiresAtUtc > now &&
                    (x.Status == PendingBatchStatus.PendingPayment ||
                     x.Status == PendingBatchStatus.Paid))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, PendingBatchStatus.Processing),
                    cancellationToken);

            if (updated != 1)
                return false;

            pending.Status = PendingBatchStatus.Processing;
            return true;
        }

        private static string GetErrorMessage(Exception ex)
        {
            var message = string.IsNullOrWhiteSpace(ex.Message)
                ? "Pending batch processing failed."
                : ex.Message;

            return message.Length <= 2000
                ? message
                : message[..2000];
        }

        private async Task<bool> IsAuthorizedForProcessingAsync(
            PendingBatchJob pending,
            CancellationToken cancellationToken)
        {
            if (pending.Status == PendingBatchStatus.Paid)
                return true;

            if (string.IsNullOrWhiteSpace(pending.UserId))
                return false;

            return await _db.UserSubscriptions.AnyAsync(s =>
                s.UserId == pending.UserId &&
                (s.PlanType == "monthly" || s.PlanType == "yearly" || s.PlanType == "teams") &&
                (s.Status == "active" || s.Status == "trialing"),
                cancellationToken);
        }

        private async Task<PendingBatchStatusResponse> ToStatusResponseAsync(
            PendingBatchJob pending,
            CancellationToken cancellationToken) =>
            new()
            {
                PendingBatchId = pending.PendingBatchId,
                Status = pending.Status,
                ToolType = pending.ToolType,
                FileCount = pending.FileCount,
                FileNames = ReadJsonList(pending.OriginalFileNames),
                IsPaid = pending.Status == PendingBatchStatus.Paid,
                IsAuthorized = await IsAuthorizedForProcessingAsync(pending, cancellationToken),
                IsUsed = pending.IsUsed,
                ErrorMessage = pending.ErrorMessage
            };

        private static string GenerateAccessToken()
        {
            Span<byte> bytes = stackalloc byte[32];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-", StringComparison.Ordinal)
                .Replace("/", "_", StringComparison.Ordinal)
                .TrimEnd('=');
        }

        private static bool TokensEqual(string expected, string actual)
        {
            var expectedBytes = System.Text.Encoding.UTF8.GetBytes(expected);
            var actualBytes = System.Text.Encoding.UTF8.GetBytes(actual);
            return expectedBytes.Length == actualBytes.Length &&
                   CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
        }

        private static List<string> ReadJsonList(string json) =>
            string.IsNullOrWhiteSpace(json)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();

        private static byte[] BuildZip(List<(string FileName, byte[] Bytes, string? Error)> results)
        {
            using var zipStream = new MemoryStream();
            using (var writer = new SharpCompress.Writers.Zip.ZipWriter(
                zipStream,
                new SharpCompress.Writers.Zip.ZipWriterOptions(
                    SharpCompress.Common.CompressionType.Deflate)
                {
                    LeaveStreamOpen = true
                }))
            {
                foreach (var (fileName, bytes, error) in results)
                {
                    if (bytes.Length == 0) continue;
                    var outName = $"processed_{Path.GetFileNameWithoutExtension(fileName)}.pdf";
                    using var entryStream = new MemoryStream(bytes);
                    writer.Write(outName, entryStream, DateTime.UtcNow);
                }

                var failed = results.Where(r => r.Error != null).ToList();
                if (failed.Any())
                {
                    var errorText = string.Join("\n",
                        failed.Select(f => $"{f.FileName}: {f.Error}"));
                    var errorBytes = System.Text.Encoding.UTF8.GetBytes(errorText);
                    using var errorStream = new MemoryStream(errorBytes);
                    writer.Write("batch_errors.txt", errorStream, DateTime.UtcNow);
                }
            }

            return zipStream.ToArray();
        }
    }
}
