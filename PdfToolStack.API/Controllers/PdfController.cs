using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;
using PdfToolStack.API.Configuration;
using PdfToolStack.API.Services;
using PdfToolStack.Application.DTOs;
using PdfToolStack.Application.Interfaces;
using PdfToolStack.Application.Services;
using PdfToolStack.Domain.Entities;
using PdfToolStack.Domain.Enums;
using PdfToolStack.Infrastructure.Processors;
using PdfToolStack.Infrastructure.Services;
using System.IO.Compression;
using System.Security.Claims;
using SharpCompress.Writers.Zip;
using SharpCompress.Common;
using DomainRedactionRegion = PdfToolStack.Domain.Entities.RedactionRegion;

namespace PdfToolStack.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PdfController : ControllerBase
    {
        private const int FreeBatchFileLimit = 3;

        private const string MissingDocxMessage =
            "The DOCX file could not be generated. Please try again.";

        private readonly IPdfService _pdfService;
        private readonly ProcessingOptions _options;
        private readonly ILogger<PdfController> _logger;
        private readonly IFileValidationService _fileValidationService;
        private readonly IFeatureAccessService _featureAccessService;
        private readonly IAiUsageService _aiUsageService;
        private readonly FileLimit _fileLimits;
        private readonly SubscriptionService? _subscriptionService;

        public PdfController(
            IPdfService pdfService,
            IOptions<ProcessingOptions> options,
            IOptions<FileLimit> fileLimits,
            ILogger<PdfController> logger,
            IFileValidationService fileValidationService,
            IFeatureAccessService featureAccessService,
            IAiUsageService aiUsageService,
            SubscriptionService? subscriptionService = null)
        {
            _pdfService = pdfService;
            _options = options.Value;
            _fileLimits = fileLimits.Value;
            _logger = logger;
            _fileValidationService = fileValidationService;
            _featureAccessService = featureAccessService;
            _aiUsageService = aiUsageService;
            _subscriptionService = subscriptionService;
        }

        // POST api/pdf/process
        [HttpPost("process")]
        [RequestSizeLimit(52428800)] // 50MB
        public async Task<IActionResult> Process(
            IFormFile file,
            [FromQuery] string toolType,
            [FromQuery] string? compressionProfile = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Process endpoint hit. ToolType: {ToolType}", toolType);

            if (!Enum.TryParse<ToolType>(toolType, ignoreCase: true, out var parsedToolType))
            {
                _logger.LogWarning("Invalid tool type: {ToolType}", toolType);
                return BadRequest(new { error = $"Invalid tool type: {toolType}" });
            }

            var validationResult = await ValidateRequiredPdfAsync(file, cancellationToken);
            if (validationResult != null)
                return validationResult;

            _logger.LogInformation("File received: {FileName}, Size: {Size}", file!.FileName, file.Length);

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream, cancellationToken);

            var request = new ProcessRequest
            {
                ToolType = parsedToolType,
                FileBytes = memoryStream.ToArray(),
                FileName = file.FileName,
                FileSizeBytes = file.Length
            };

            if (parsedToolType == ToolType.CompressPdf &&
                Enum.TryParse<CompressionProfile>(compressionProfile, ignoreCase: true, out var parsedProfile))
            {
                request.CompressionProfile = parsedProfile;
            }

            _logger.LogInformation("Calling PdfService.ProcessAsync for job {JobId}", request.JobId);

            try
            {
                var response = await _pdfService.ProcessAsync(request, cancellationToken);

                _logger.LogInformation(
                    "PdfService returned. IsSuccess: {IsSuccess}, Error: {Error}",
                    response.IsSuccess,
                    response.ErrorMessage ?? "none");

                if (!response.IsSuccess)
                {
                    _logger.LogWarning(
                        "Processing failed for job {JobId}: {Error}",
                        response.JobId,
                        response.ErrorMessage);

                    return UnprocessableEntity(new { error = response.ErrorMessage });
                }

                if (parsedToolType == ToolType.PdfToWord)
                {
                    var outputBytesLength = response.OutputBytes?.Length ?? 0;
                    _logger.LogDebug(
                        "[PdfToWordDiag] Controller response job={JobId} success={IsSuccess} outputSizeBytes={OutputSizeBytes} outputBytesLength={OutputBytesLength}",
                        response.JobId,
                        response.IsSuccess,
                        response.OutputSizeBytes,
                        outputBytesLength);

                    if (outputBytesLength == 0)
                    {
                        _logger.LogError(
                            "PDF to Word job {JobId} completed without DOCX bytes. Returning 422 instead of 200.",
                            response.JobId);

                        return UnprocessableEntity(new { error = MissingDocxMessage });
                    }
                }

                _logger.LogInformation(
                    "Job {JobId} completed. Compression ratio: {Ratio}%",
                    response.JobId,
                    response.CompressionRatio);

                if (_subscriptionService != null)
                {
                    var userId = User.FindFirst("sub")?.Value;
                    if (!string.IsNullOrEmpty(userId))
                    {
                        _ = _subscriptionService.TrackDownloadAsync(
                            userId, file.FileName, toolType, file.Length);
                    }
                }

                await ConsumeLargeFileUnlockIfNeededAsync(
                    file,
                    parsedToolType.ToString(),
                    cancellationToken);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in Process endpoint: {Message}", ex.Message);

                return StatusCode(500, new
                {
                    error = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }

        // POST api/pdf/batch
        [HttpPost("batch")]
        [RequestSizeLimit(524288000)] // 500MB total
        public async Task<IActionResult> Batch(
            List<IFormFile> files,
            [FromQuery] string toolType,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Batch endpoint hit. ToolType: {ToolType}, FileCount: {Count}",
                toolType, files?.Count ?? 0);

            if (files == null || files.Count == 0)
                return BadRequest(new { error = "No files provided." });

            if (files.Count > 20)
                return BadRequest(new { error = "Maximum 20 files per batch." });

            if (!Enum.TryParse<ToolType>(toolType, ignoreCase: true, out var parsedToolType))
                return BadRequest(new { error = $"Invalid tool type: {toolType}" });

            var batchAccess = await GetBatchAccessAsync(files.Count);
            if (!batchAccess.IsAllowed)
            {
                return StatusCode(StatusCodes.Status402PaymentRequired, new
                {
                    error = $"Free batch jobs support up to {FreeBatchFileLimit} files. Upgrade or unlock batch processing for larger jobs."
                });
            }

            var results = new List<(string FileName, byte[] Bytes, string? Error)>();

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var validation = await ValidateRequiredPdfAsync(file, cancellationToken);
                if (validation != null)
                {
                    results.Add((file.FileName, Array.Empty<byte>(),
                        $"Skipped: validation failed"));
                    continue;
                }

                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, cancellationToken);

                var request = new ProcessRequest
                {
                    ToolType = parsedToolType,
                    FileBytes = ms.ToArray(),
                    FileName = file.FileName,
                    FileSizeBytes = file.Length
                };

                try
                {
                    var response = await _pdfService.ProcessAsync(
                        request, cancellationToken);

                    if (response.IsSuccess)
                    {
                        await ConsumeLargeFileUnlockIfNeededAsync(
                            file,
                            parsedToolType.ToString(),
                            cancellationToken);

                        results.Add((file.FileName, response.OutputBytes!, null));
                    }
                    else
                    {
                        results.Add((file.FileName, Array.Empty<byte>(),
                            response.ErrorMessage));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Batch item failed: {FileName}", file.FileName);
                    results.Add((file.FileName, Array.Empty<byte>(), ex.Message));
                }
            }

            // Build ZIP using SharpCompress for better compression
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

            zipStream.Position = 0;
            var toolLabel = parsedToolType.ToString().ToLower();

            if (batchAccess.ShouldConsumeBatchUnlock &&
                !string.IsNullOrWhiteSpace(batchAccess.UserId))
            {
                await _featureAccessService.ConsumeBatchUnlockAsync(batchAccess.UserId);
            }

            return File(zipStream.ToArray(), "application/zip",
                $"pdftoolstack_batch_{toolLabel}.zip");
        }

        private async Task<BatchAccessResult> GetBatchAccessAsync(int fileCount)
        {
            if (fileCount <= FreeBatchFileLimit)
                return new BatchAccessResult(true, false, string.Empty);

            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return new BatchAccessResult(false, false, string.Empty);

            if (_subscriptionService != null)
            {
                var status = await _subscriptionService.GetStatusAsync(userId);
                if (status.HasPro || status.HasTeams)
                    return new BatchAccessResult(true, false, userId);
            }

            if (await _featureAccessService.HasBatchUnlockAsync(userId))
                return new BatchAccessResult(true, true, userId);

            return new BatchAccessResult(false, false, userId);
        }

        private string GetUserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
            User.FindFirst("sub")?.Value ??
            string.Empty;

        private sealed record BatchAccessResult(
            bool IsAllowed,
            bool ShouldConsumeBatchUnlock,
            string UserId);

        // POST api/pdf/merge
        [HttpPost("merge")]
        [RequestSizeLimit(209715200)] // 200MB total
        public async Task<IActionResult> Merge(
            List<IFormFile> files,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Merge endpoint hit. Count: {Count}", files?.Count ?? 0);

            if (files == null || files.Count < 2)
            {
                return BadRequest(new { error = "Please upload at least 2 PDF files." });
            }

            if (files.Count > 10)
            {
                return BadRequest(new { error = "Maximum 10 files can be merged at once." });
            }

            var fileBytesList = new List<byte[]>();

            foreach (var file in files)
            {
                var validationResult = await ValidateRequiredPdfAsync(file, cancellationToken);
                if (validationResult != null)
                    return validationResult;

                using var ms = new MemoryStream();
                await file!.CopyToAsync(ms, cancellationToken);
                fileBytesList.Add(ms.ToArray());
            }

            var request = new ProcessRequest
            {
                ToolType = ToolType.MergePdf,
                FileBytes = fileBytesList[0],
                FileName = files[0].FileName,
                FileSizeBytes = files[0].Length,
                AdditionalFiles = fileBytesList.Skip(1).ToList(),
                AdditionalFileNames = files.Skip(1).Select(f => f.FileName).ToList()
            };

            try
            {
                var merger = new PdfMerger(request.AdditionalFiles);
                var outputBytes = await merger.ProcessAsync(request.FileBytes, cancellationToken);

                await ConsumeLargeFileUnlocksIfNeededAsync(
                    files,
                    ToolType.MergePdf.ToString(),
                    cancellationToken);

                return File(outputBytes, "application/pdf", "merged.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Merge failed: {Message}", ex.Message);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // POST api/pdf/detect-fields
        [HttpPost("detect-fields")]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> DetectFields(
            IFormFile file,
            CancellationToken cancellationToken)
        {
            var validationResult = await ValidateRequiredPdfAsync(file, cancellationToken);
            if (validationResult != null)
                return validationResult;

            using var ms = new MemoryStream();
            await file!.CopyToAsync(ms, cancellationToken);

            var filler = new PdfFormFiller();
            var fields = filler.DetectFields(ms.ToArray());

            return Ok(new DetectFieldsResponse
            {
                HasFields = fields.Count > 0,
                Fields = fields
            });
        }

        // POST api/pdf/fill-form
        [HttpPost("fill-form")]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> FillForm(
            IFormFile file,
            [FromForm] string fieldsJson,
            CancellationToken cancellationToken)
        {
            var validationResult = await ValidateRequiredPdfAsync(file, cancellationToken);
            if (validationResult != null)
                return validationResult;

            var fieldValues = System.Text.Json.JsonSerializer
                .Deserialize<Dictionary<string, string>>(fieldsJson)
                ?? new Dictionary<string, string>();

            using var ms = new MemoryStream();
            await file!.CopyToAsync(ms, cancellationToken);

            var filler = new PdfFormFiller(fieldValues);
            var outputBytes = await filler.ProcessAsync(ms.ToArray(), cancellationToken);

            await ConsumeLargeFileUnlockIfNeededAsync(
                file!,
                ToolType.FillPdfForm.ToString(),
                cancellationToken);

            return File(outputBytes, "application/pdf", "filled_form.pdf");
        }

        // POST api/pdf/redact
        [HttpPost("redact")]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> Redact(
            IFormFile file,
            [FromForm] string regionsJson,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Redact endpoint hit");

            var validationResult = await ValidateRequiredPdfAsync(file, cancellationToken);
            if (validationResult != null)
                return validationResult;

            var domainRegions = System.Text.Json.JsonSerializer
                .Deserialize<List<DomainRedactionRegion>>(regionsJson)
                ?? new List<DomainRedactionRegion>();

            using var ms = new MemoryStream();
            await file!.CopyToAsync(ms, cancellationToken);

            var processorRegions = domainRegions.Select(r =>
                new PdfToolStack.Infrastructure.Processors.RedactionRegion
                {
                    X1 = r.X1,
                    Y1 = r.Y1,
                    X2 = r.X2,
                    Y2 = r.Y2,
                    PageNumber = r.PageNumber
                });

            var redactor = new PdfRedactor(processorRegions);
            var outputBytes = await redactor.ProcessAsync(ms.ToArray(), cancellationToken);

            await ConsumeLargeFileUnlockIfNeededAsync(
                file!,
                ToolType.RedactPdf.ToString(),
                cancellationToken);

            return File(outputBytes, "application/pdf", "redacted.pdf");
        }

        // POST api/pdf/page-count
        [HttpPost("page-count")]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> GetPageCount(
            IFormFile file,
            CancellationToken cancellationToken)
        {
            var validationResult = await ValidateRequiredPdfAsync(file, cancellationToken);
            if (validationResult != null)
                return validationResult;

            using var ms = new MemoryStream();
            await file!.CopyToAsync(ms, cancellationToken);

            var reader = new iTextSharp.text.pdf.PdfReader(ms.ToArray());
            var pageCount = reader.NumberOfPages;
            reader.Close();

            return Ok(new { pageCount });
        }

        // GET api/pdf/status/{jobId}
        [HttpGet("status/{jobId}")]
        public async Task<IActionResult> GetStatus(
            Guid jobId,
            CancellationToken cancellationToken)
        {
            var status = await _pdfService.GetJobStatusAsync(jobId, cancellationToken);
            return Ok(status);
        }

        // GET api/pdf/health
        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow
            });
        }

        // POST api/pdf/delete-pages
        [HttpPost("delete-pages")]
        [RequestSizeLimit(524288000)]
        public async Task<IActionResult> DeletePages(
            IFormFile file,
            [FromForm] string pageNumbers,
            CancellationToken cancellationToken)
        {
            var validationResult = await ValidateRequiredPdfAsync(file, cancellationToken);
            if (validationResult != null)
                return validationResult;

            if (!TryParsePageNumbers(pageNumbers, out var pages, out var parseError))
                return BadRequest(new { error = parseError });

            var request = await BuildPdfRequestAsync(file!, ToolType.DeletePages, cancellationToken);
            request.PageNumbers = pages;

            var response = await _pdfService.ProcessAsync(request, cancellationToken);
            return await BuildSuccessfulFileResponseAsync(
                response, file!, "deleted_pages.pdf", ToolType.DeletePages.ToString(), cancellationToken);
        }

        // POST api/pdf/extract-pages
        [HttpPost("extract-pages")]
        [RequestSizeLimit(524288000)]
        public async Task<IActionResult> ExtractPages(
            IFormFile file,
            [FromForm] string pageNumbers,
            CancellationToken cancellationToken)
        {
            var validationResult = await ValidateRequiredPdfAsync(file, cancellationToken);
            if (validationResult != null)
                return validationResult;

            if (!TryParsePageNumbers(pageNumbers, out var pages, out var parseError))
                return BadRequest(new { error = parseError });

            var request = await BuildPdfRequestAsync(file!, ToolType.ExtractPages, cancellationToken);
            request.PageNumbers = pages;

            var response = await _pdfService.ProcessAsync(request, cancellationToken);
            return await BuildSuccessfulFileResponseAsync(
                response, file!, "extracted_pages.pdf", ToolType.ExtractPages.ToString(), cancellationToken);
        }

        // POST api/pdf/rotate
        [HttpPost("rotate")]
        [RequestSizeLimit(524288000)]
        public async Task<IActionResult> RotatePdf(
            IFormFile file,
            [FromForm] int rotation = 90,
            [FromForm] string? pageNumbers = null,
            CancellationToken cancellationToken = default)
        {
            var validationResult = await ValidateRequiredPdfAsync(file, cancellationToken);
            if (validationResult != null)
                return validationResult;

            List<int>? pages = null;
            if (!string.IsNullOrWhiteSpace(pageNumbers))
            {
                if (!TryParsePageNumbers(pageNumbers, out pages, out var parseError))
                    return BadRequest(new { error = parseError });
            }

            var request = await BuildPdfRequestAsync(file!, ToolType.RotatePdf, cancellationToken);
            request.PageNumbers = pages;
            request.Rotation = rotation;

            var response = await _pdfService.ProcessAsync(request, cancellationToken);
            return await BuildSuccessfulFileResponseAsync(
                response, file!, "rotated.pdf", ToolType.RotatePdf.ToString(), cancellationToken);
        }

        // POST api/pdf/word-to-pdf
        [HttpPost("word-to-pdf")]
        [RequestSizeLimit(524288000)]
        public async Task<IActionResult> WordToPdf(
            IFormFile file,
            CancellationToken cancellationToken)
        {
            var validationResult = await ValidateRequiredFileAsync(file, cancellationToken);
            if (validationResult != null)
                return validationResult;

            var request = await BuildRequestAsync(file!, ToolType.WordToPdf, cancellationToken);

            var response = await _pdfService.ProcessAsync(request, cancellationToken);
            var outName = Path.GetFileNameWithoutExtension(file.FileName) + ".pdf";

            return await BuildSuccessfulFileResponseAsync(
                response, file!, outName, ToolType.WordToPdf.ToString(), cancellationToken);
        }

        // POST api/pdf/ppt-to-pdf
        [HttpPost("ppt-to-pdf")]
        [RequestSizeLimit(524288000)]
        public async Task<IActionResult> PptToPdf(
            IFormFile file,
            CancellationToken cancellationToken)
        {
            var validationResult = await ValidateRequiredFileAsync(file, cancellationToken);
            if (validationResult != null)
                return validationResult;

            var request = await BuildRequestAsync(file!, ToolType.PptToPdf, cancellationToken);

            var response = await _pdfService.ProcessAsync(request, cancellationToken);
            var outName = Path.GetFileNameWithoutExtension(file.FileName) + ".pdf";

            return await BuildSuccessfulFileResponseAsync(
                response, file!, outName, ToolType.PptToPdf.ToString(), cancellationToken);
        }

        // POST api/pdf/excel-to-pdf
        [HttpPost("excel-to-pdf")]
        [RequestSizeLimit(524288000)]
        public async Task<IActionResult> ExcelToPdf(
            IFormFile file,
            CancellationToken cancellationToken)
        {
            var validationResult = await ValidateRequiredFileAsync(file, cancellationToken);
            if (validationResult != null)
                return validationResult;

            var request = await BuildRequestAsync(file!, ToolType.ExcelToPdf, cancellationToken);

            var response = await _pdfService.ProcessAsync(request, cancellationToken);
            var outName = Path.GetFileNameWithoutExtension(file.FileName) + ".pdf";

            return await BuildSuccessfulFileResponseAsync(
                response, file!, outName, ToolType.ExcelToPdf.ToString(), cancellationToken);
        }

        // POST api/pdf/flatten
        [HttpPost("flatten")]
        [RequestSizeLimit(524288000)]
        public async Task<IActionResult> FlattenPdf(
            IFormFile file,
            CancellationToken cancellationToken)
        {
            var validationResult = await ValidateRequiredPdfAsync(file, cancellationToken);
            if (validationResult != null)
                return validationResult;

            var request = await BuildPdfRequestAsync(file!, ToolType.FlattenPdf, cancellationToken);

            var response = await _pdfService.ProcessAsync(request, cancellationToken);
            return await BuildSuccessfulFileResponseAsync(
                response, file!, "flattened.pdf", ToolType.FlattenPdf.ToString(), cancellationToken);
        }

        [HttpPost("crop")]
        [RequestSizeLimit(524288000)]
        public async Task<IActionResult> CropPdf(
            IFormFile file,
            [FromForm] float marginTop = 0f,
            [FromForm] float marginRight = 0f,
            [FromForm] float marginBottom = 0f,
            [FromForm] float marginLeft = 0f,
            [FromForm] string? pageNumbers = null,
            CancellationToken cancellationToken = default)
        {
            var validationResult = await ValidateRequiredPdfAsync(file, cancellationToken);
            if (validationResult != null)
                return validationResult;

            var request = await BuildPdfRequestAsync(file!, ToolType.CropPdf, cancellationToken);

            request.CropMarginTop = marginTop;
            request.CropMarginRight = marginRight;
            request.CropMarginBottom = marginBottom;
            request.CropMarginLeft = marginLeft;

            // Parse comma-separated page numbers if provided: "1,2,3"
            if (!string.IsNullOrWhiteSpace(pageNumbers))
            {
                request.CropPageNumbers = pageNumbers
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => int.TryParse(s, out var n) ? n : 0)
                    .Where(n => n > 0)
                    .ToList();
            }

            var response = await _pdfService.ProcessAsync(request, cancellationToken);
            return await BuildSuccessfulFileResponseAsync(
                response, file!, "cropped.pdf", ToolType.CropPdf.ToString(), cancellationToken);
        }

        // POST api/pdf/watermark
        [HttpPost("watermark")]
        [RequestSizeLimit(524288000)]
        public async Task<IActionResult> WatermarkPdf(
            IFormFile file,
            [FromForm] string watermarkText = "CONFIDENTIAL",
            [FromForm] float opacity = 0.3f,
            [FromForm] float fontSize = 48f,
            CancellationToken cancellationToken = default)
        {
            var validationResult = await ValidateRequiredPdfAsync(file, cancellationToken);
            if (validationResult != null)
                return validationResult;

            var request = await BuildPdfRequestAsync(file!, ToolType.WatermarkPdf, cancellationToken);
            request.WatermarkText = watermarkText;
            request.WatermarkOpacity = opacity;
            request.WatermarkFontSize = fontSize;

            var response = await _pdfService.ProcessAsync(request, cancellationToken);
            return await BuildSuccessfulFileResponseAsync(
                response, file!, "watermarked.pdf", ToolType.WatermarkPdf.ToString(), cancellationToken);
        }

        // POST api/pdf/number-pages
        [HttpPost("number-pages")]
        [RequestSizeLimit(524288000)]
        public async Task<IActionResult> NumberPages(
            IFormFile file,
            [FromForm] string position = "bottom-center",
            [FromForm] int startNumber = 1,
            CancellationToken cancellationToken = default)
        {
            var validationResult = await ValidateRequiredPdfAsync(file, cancellationToken);
            if (validationResult != null)
                return validationResult;

            var request = await BuildPdfRequestAsync(file!, ToolType.NumberPages, cancellationToken);
            request.PageNumberPosition = position;
            request.PageNumberStart = startNumber;

            var response = await _pdfService.ProcessAsync(request, cancellationToken);
            return await BuildSuccessfulFileResponseAsync(
                response, file!, "numbered.pdf", ToolType.NumberPages.ToString(), cancellationToken);
        }

        // POST api/pdf/unlock
        [HttpPost("unlock")]
        [RequestSizeLimit(524288000)]
        public async Task<IActionResult> UnlockPdf(
            IFormFile file,
            [FromForm] string? password = null,
            CancellationToken cancellationToken = default)
        {
            var validationResult = await ValidateRequiredPdfAsync(file, cancellationToken);
            if (validationResult != null)
                return validationResult;

            var request = await BuildPdfRequestAsync(file!, ToolType.UnlockPdf, cancellationToken);
            request.Password = password;

            var response = await _pdfService.ProcessAsync(request, cancellationToken);
            return await BuildSuccessfulFileResponseAsync(
                response, file!, "unlocked.pdf", ToolType.UnlockPdf.ToString(), cancellationToken);
        }

        // POST api/pdf/protect
        [HttpPost("protect")]
        [RequestSizeLimit(524288000)]
        public async Task<IActionResult> ProtectPdf(
            IFormFile file,
            [FromForm] string userPassword = "",
            [FromForm] string ownerPassword = "",
            [FromForm] bool allowPrinting = true,
            [FromForm] bool allowCopying = false,
            CancellationToken cancellationToken = default)
        {
            var validationResult = await ValidateRequiredPdfAsync(file, cancellationToken);
            if (validationResult != null)
                return validationResult;

            var request = await BuildPdfRequestAsync(file!, ToolType.ProtectPdf, cancellationToken);
            request.UserPassword = userPassword;
            request.OwnerPassword = ownerPassword;
            request.AllowPrinting = allowPrinting;
            request.AllowCopying = allowCopying;

            var response = await _pdfService.ProcessAsync(request, cancellationToken);
            return await BuildSuccessfulFileResponseAsync(
                response, file!, "protected.pdf", ToolType.ProtectPdf.ToString(), cancellationToken);
        }

        // POST api/pdf/split
        [HttpPost("split")]
        [RequestSizeLimit(524288000)]
        public async Task<IActionResult> SplitPdf(
            IFormFile file,
            [FromForm] int? fromPage = null,
            [FromForm] int? toPage = null,
            CancellationToken cancellationToken = default)
        {
            var validationResult = await ValidateRequiredPdfAsync(file, cancellationToken);
            if (validationResult != null)
                return validationResult;

            try
            {
                using var ms = new MemoryStream();
                await file!.CopyToAsync(ms, cancellationToken);
                var bytes = ms.ToArray();
                var processor = new SplitPdfProcessor();

                if (fromPage.HasValue && toPage.HasValue)
                {
                    if (fromPage < 1 || toPage < fromPage)
                        return BadRequest(new { error = "Invalid page range." });

                    var result = await processor.SplitRangeAsync(
                        bytes, fromPage.Value, toPage.Value, cancellationToken);

                    await ConsumeLargeFileUnlockIfNeededAsync(
                        file!,
                        ToolType.SplitPdf.ToString(),
                        cancellationToken);

                    return File(result, "application/pdf", $"pages_{fromPage}_{toPage}.pdf");
                }

                var pages = await processor.SplitAsync(bytes, cancellationToken);

                using var zipStream = new MemoryStream();
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    for (int i = 0; i < pages.Count; i++)
                    {
                        var entry = archive.CreateEntry($"page_{i + 1}.pdf", CompressionLevel.Fastest);
                        using var entryStream = entry.Open();
                        await entryStream.WriteAsync(pages[i], cancellationToken);
                    }
                }

                zipStream.Position = 0;
                var baseName = Path.GetFileNameWithoutExtension(file.FileName);

                await ConsumeLargeFileUnlockIfNeededAsync(
                    file,
                    ToolType.SplitPdf.ToString(),
                    cancellationToken);

                return File(zipStream.ToArray(), "application/zip", $"{baseName}_pages.zip");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Split PDF error");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // POST api/pdf/jpg-to-pdf
        [HttpPost("jpg-to-pdf")]
        [RequestSizeLimit(524288000)]
        public async Task<IActionResult> JpgToPdf(
            List<IFormFile> files,
            CancellationToken cancellationToken)
        {
            if (files == null || !files.Any())
                return BadRequest(new { error = "No files provided." });

            var imageBytes = new List<byte[]>();

            foreach (var file in files)
            {
                var validationResult = await ValidateRequiredFileAsync(file, cancellationToken);
                if (validationResult != null)
                    return validationResult;

                using var ms = new MemoryStream();
                await file!.CopyToAsync(ms, cancellationToken);
                imageBytes.Add(ms.ToArray());
            }

            var request = new ProcessRequest
            {
                ToolType = ToolType.JpgToPdf,
                FileBytes = imageBytes[0],
                FileName = files[0].FileName,
                FileSizeBytes = files[0].Length,
                AdditionalFiles = imageBytes.Skip(1).ToList()
            };

            var response = await _pdfService.ProcessAsync(request, cancellationToken);
            if (!response.IsSuccess)
                return UnprocessableEntity(new { error = response.ErrorMessage });

            await ConsumeLargeFileUnlocksIfNeededAsync(
                files,
                ToolType.JpgToPdf.ToString(),
                cancellationToken);

            return File(response.OutputBytes!, "application/pdf", "images.pdf");
        }

        // POST api/pdf/organize
        [HttpPost("organize")]
        [RequestSizeLimit(524288000)]
        public async Task<IActionResult> OrganizePdf(
            IFormFile file,
            [FromForm] string operationsJson,
            CancellationToken cancellationToken)
        {
            var validationResult = await ValidateRequiredPdfAsync(file, cancellationToken);
            if (validationResult != null)
                return validationResult;

            var request = await BuildPdfRequestAsync(file!, ToolType.OrganizePdf, cancellationToken);
            request.PageOperations = System.Text.Json.JsonSerializer
                .Deserialize<List<PageOperationDto>>(operationsJson)
                ?? new List<PageOperationDto>();

            var response = await _pdfService.ProcessAsync(request, cancellationToken);
            return await BuildSuccessfulFileResponseAsync(
                response, file!, "organized.pdf", ToolType.OrganizePdf.ToString(), cancellationToken);
        }

        // POST api/pdf/sign
        [HttpPost("sign")]
        [RequestSizeLimit(524288000)]
        public async Task<IActionResult> SignPdf(
            IFormFile file,
            IFormFile signature,
            [FromForm] float x,
            [FromForm] float y,
            [FromForm] float width,
            [FromForm] float height,
            [FromForm] int pageNumber = 1,
            CancellationToken cancellationToken = default)
        {
            var pdfValidation = await ValidateRequiredPdfAsync(file, cancellationToken);
            if (pdfValidation != null)
                return pdfValidation;

            var sigValidation = await ValidateRequiredFileAsync(signature, cancellationToken);
            if (sigValidation != null)
                return sigValidation;

            var request = await BuildPdfRequestAsync(file!, ToolType.SignPdf, cancellationToken);

            using var sigMs = new MemoryStream();
            await signature!.CopyToAsync(sigMs, cancellationToken);
            request.SignatureBytes = sigMs.ToArray();
            request.SignatureX = x;
            request.SignatureY = y;
            request.SignatureWidth = width;
            request.SignatureHeight = height;
            request.SignaturePageNumber = pageNumber;

            var response = await _pdfService.ProcessAsync(request, cancellationToken);
            if (!response.IsSuccess)
                return UnprocessableEntity(new { error = response.ErrorMessage });

            await ConsumeLargeFileUnlocksIfNeededAsync(
                new[] { file!, signature! },
                ToolType.SignPdf.ToString(),
                cancellationToken);

            return File(response.OutputBytes!, "application/pdf", "signed.pdf");
        }

        // POST api/pdf/edit
        [HttpPost("edit")]
        [RequestSizeLimit(524288000)]
        public async Task<IActionResult> EditPdf(
            IFormFile file,
            [FromForm] string annotationsJson,
            CancellationToken cancellationToken)
        {
            var validationResult = await ValidateRequiredPdfAsync(file, cancellationToken);
            if (validationResult != null)
                return validationResult;

            var request = await BuildPdfRequestAsync(file!, ToolType.EditPdf, cancellationToken);
            request.Annotations = System.Text.Json.JsonSerializer
                .Deserialize<List<PdfAnnotationDto>>(annotationsJson)
                ?? new List<PdfAnnotationDto>();

            var response = await _pdfService.ProcessAsync(request, cancellationToken);
            return await BuildSuccessfulFileResponseAsync(
                response, file!, "edited.pdf", ToolType.EditPdf.ToString(), cancellationToken);
        }

        // POST api/pdf/annotate
        [HttpPost("annotate")]
        [RequestSizeLimit(524288000)]
        public async Task<IActionResult> AnnotatePdf(
            IFormFile file,
            [FromForm] string highlightsJson,
            CancellationToken cancellationToken)
        {
            var validationResult = await ValidateRequiredPdfAsync(file, cancellationToken);
            if (validationResult != null)
                return validationResult;

            var request = await BuildPdfRequestAsync(file!, ToolType.AnnotatePdf, cancellationToken);
            request.Highlights = System.Text.Json.JsonSerializer
                .Deserialize<List<PdfHighlightDto>>(highlightsJson)
                ?? new List<PdfHighlightDto>();

            var response = await _pdfService.ProcessAsync(request, cancellationToken);
            return await BuildSuccessfulFileResponseAsync(
                response, file!, "annotated.pdf", ToolType.AnnotatePdf.ToString(), cancellationToken);
        }

        // POST api/pdf/compare
        [HttpPost("compare")]
        [Authorize]
        [RequestSizeLimit(104857600)] // 100MB total (two files)
        public async Task<IActionResult> ComparePdf(
            IFormFile original,
            IFormFile revised,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Compare endpoint hit");

            var v1 = await ValidateRequiredPdfAsync(original, cancellationToken);
            if (v1 != null) return v1;

            var v2 = await ValidateRequiredPdfAsync(revised, cancellationToken);
            if (v2 != null) return v2;

            var userId = GetRequiredUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var usageCheck = await CheckAiUsageAsync(userId, "compare", "pdf-compare");
            if (usageCheck != null)
                return usageCheck;

            using var ms1 = new MemoryStream();
            await original!.CopyToAsync(ms1, cancellationToken);

            using var ms2 = new MemoryStream();
            await revised!.CopyToAsync(ms2, cancellationToken);

            try
            {
                var processor = new ComparePdfProcessor();
                var result = await processor.CompareAsync(
                    ms1.ToArray(), ms2.ToArray(), cancellationToken);

                Response.Headers["X-Compare-Added"] =
                    result.TotalAddedWords.ToString();
                Response.Headers["X-Compare-Removed"] =
                    result.TotalRemovedWords.ToString();
                Response.Headers["X-Compare-Pages"] =
                    result.TotalPagesCompared.ToString();

                await ConsumeLargeFileUnlocksIfNeededAsync(
                    new[] { original!, revised! },
                    ToolType.ComparePdf.ToString(),
                    cancellationToken);

                return File(result.ReportBytes,
                    "application/pdf", "comparison_report.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Compare PDF error");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // Helpers

        private string? GetRequiredUserId() =>
            User.FindFirst("sub")?.Value ??
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        private async Task<IActionResult> BuildSuccessfulFileResponseAsync(
            ProcessResponse response,
            IFormFile sourceFile,
            string downloadFileName,
            string toolType,
            CancellationToken cancellationToken)
        {
            if (!response.IsSuccess)
                return UnprocessableEntity(new { error = response.ErrorMessage });

            await ConsumeLargeFileUnlockIfNeededAsync(
                sourceFile,
                toolType,
                cancellationToken);

            return File(response.OutputBytes!, "application/pdf", downloadFileName);
        }

        private async Task ConsumeLargeFileUnlockIfNeededAsync(
            IFormFile sourceFile,
            string toolType,
            CancellationToken cancellationToken)
        {
            if (sourceFile.Length <= _fileLimits.FreeTierMaxBytes)
                return;

            cancellationToken.ThrowIfCancellationRequested();

            var userId = GetRequiredUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning(
                    "Skipping LargeFileUnlock consumption for oversized {ToolType} file {FileName}: user id was not available.",
                    toolType,
                    sourceFile.FileName);
                return;
            }

            if (await ResolveIsProAsync())
            {
                _logger.LogDebug(
                    "Skipping LargeFileUnlock consumption for subscribed user {UserId} after {ToolType}. FileSizeBytes: {FileSizeBytes}",
                    userId,
                    toolType,
                    sourceFile.Length);
                return;
            }

            try
            {
                var consumed = await _featureAccessService
                    .ConsumeLargeFileUnlockAsync(userId);

                if (consumed)
                {
                    _logger.LogInformation(
                        "Consumed LargeFileUnlock for user {UserId} after successful {ToolType} processing. FileName: {FileName}, FileSizeBytes: {FileSizeBytes}",
                        userId,
                        toolType,
                        sourceFile.FileName,
                        sourceFile.Length);
                }
                else
                {
                    _logger.LogWarning(
                        "LargeFileUnlock was not consumed for user {UserId} after successful {ToolType} processing because no unused unlock was available. FileName: {FileName}, FileSizeBytes: {FileSizeBytes}",
                        userId,
                        toolType,
                        sourceFile.FileName,
                        sourceFile.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to consume LargeFileUnlock for user {UserId} after successful {ToolType} processing. FileName: {FileName}, FileSizeBytes: {FileSizeBytes}",
                    userId,
                    toolType,
                    sourceFile.FileName,
                    sourceFile.Length);
            }
        }

        private async Task ConsumeLargeFileUnlocksIfNeededAsync(
            IEnumerable<IFormFile> sourceFiles,
            string toolType,
            CancellationToken cancellationToken)
        {
            foreach (var sourceFile in sourceFiles)
            {
                await ConsumeLargeFileUnlockIfNeededAsync(
                    sourceFile,
                    toolType,
                    cancellationToken);
            }
        }

        private async Task<IActionResult?> CheckAiUsageAsync(
            string userId,
            string feature,
            string model)
        {
            try
            {
                var planType = "free";
                if (_subscriptionService != null)
                {
                    var status = await _subscriptionService.GetStatusAsync(userId);
                    planType = status.IsActive ? status.PlanType : "free";
                }

                var (allowed, used, limit) = await _aiUsageService
                    .CheckAndLogAsync(userId, feature, model, planType);

                if (allowed)
                    return null;

                return StatusCode(429, new
                {
                    error = $"You've used your {limit} free AI requests this month. " +
                            "Upgrade to Pro for 200 requests/month.",
                    used,
                    limit,
                    upgradePath = "/pricing"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Usage check failed for AI feature {Feature}", feature);
                return StatusCode(503, new
                {
                    error = "AI usage credits could not be verified. Please try again."
                });
            }
        }

        private async Task<IActionResult?> ValidateRequiredPdfAsync(
    IFormFile? file,
    CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided." });

            var isPro = await ResolveIsProAsync();

            var userId = User.FindFirst("sub")?.Value;

            var validation = await _fileValidationService
                .ValidatePdfAsync(file, userId, isPro, cancellationToken);

            if (!validation.IsValid)
            {
                _logger.LogWarning(
                    "PDF validation failed for {FileName}: {Error}",
                    file.FileName,
                    validation.Error);

                return BadRequest(new { error = validation.Error });
            }

            return null;
        }

        // ── Pro status helper — reads JWT sub claim ───────────────────────────────
        private async Task<bool> ResolveIsProAsync()
        {
            if (_subscriptionService is null)
                return false;

            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return false;

            var status = await _subscriptionService.GetStatusAsync(userId);
            return status.IsActive;
        }

        private async Task<IActionResult?> ValidateRequiredFileAsync(
            IFormFile? file,
            CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided." });

            var isPro = await ResolveIsProAsync();

            var userId = User.FindFirst("sub")?.Value;

            var validation = await _fileValidationService
                .ValidatePdfAsync(file, userId, isPro, cancellationToken);

            if (!validation.IsValid)
            {
                _logger.LogWarning(
                    "File validation failed for {FileName}: {Error}",
                    file.FileName,
                    validation.Error);

                return BadRequest(new { error = validation.Error });
            }

            return null;
        }

        private static async Task<ProcessRequest> BuildRequestAsync(
            IFormFile file,
            ToolType toolType,
            CancellationToken cancellationToken)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);

            return new ProcessRequest
            {
                ToolType = toolType,
                FileBytes = ms.ToArray(),
                FileName = file.FileName,
                FileSizeBytes = file.Length
            };
        }

        private static async Task<ProcessRequest> BuildPdfRequestAsync(
            IFormFile file,
            ToolType toolType,
            CancellationToken cancellationToken)
        {
            return await BuildRequestAsync(file, toolType, cancellationToken);
        }

        private static bool TryParsePageNumbers(
            string input,
            out List<int> pages,
            out string error)
        {
            pages = new List<int>();
            error = string.Empty;

            foreach (var part in input.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(part.Trim(), out var page) || page < 1)
                {
                    error = $"Invalid page number: '{part.Trim()}'";
                    return false;
                }

                pages.Add(page);
            }

            if (!pages.Any())
            {
                error = "No page numbers provided.";
                return false;
            }

            return true;
        }
    }
}
