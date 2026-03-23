using iTextSharp.text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PdfToolkit.API.Configuration;
using PdfToolkit.Application.DTOs;
using PdfToolkit.Application.Services;
using PdfToolkit.Domain.Entities;
using PdfToolkit.Domain.Enums;
using PdfToolkit.Infrastructure.Processors;
using DomainFormField = PdfToolkit.Domain.Entities.PdfFormField;
using DomainRedactionRegion = PdfToolkit.Domain.Entities.RedactionRegion;

namespace PdfToolkit.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PdfController : ControllerBase
    {
        private readonly IPdfService _pdfService;
        private readonly ProcessingOptions _options;
        private readonly ILogger<PdfController> _logger;

        public PdfController(
            IPdfService pdfService,
            IOptions<ProcessingOptions> options,
            ILogger<PdfController> logger)
        {
            _pdfService = pdfService;
            _options = options.Value;
            _logger = logger;
        }

        // POST api/pdf/process
        [HttpPost("process")]
        [RequestSizeLimit(52428800)] // 50MB
        public async Task<IActionResult> Process(
        IFormFile file,
        [FromQuery] string toolType,
        CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Process endpoint hit — ToolType: {ToolType}", toolType);

            // Parse toolType — handles both "1" and "CompressPdf"
            if (!Enum.TryParse<ToolType>(toolType, ignoreCase: true,
                out var parsedToolType))
            {
                _logger.LogWarning("Invalid tool type: {ToolType}", toolType);
                return BadRequest(new { error = $"Invalid tool type: {toolType}" });
            }

            _logger.LogInformation(
                "Parsed ToolType: {ParsedToolType}", parsedToolType);

            // Validate file exists
            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("No file provided");
                return BadRequest(new { error = "No file provided." });
            }

            _logger.LogInformation(
                "File received: {FileName}, Size: {Size}",
                file.FileName, file.Length);

            // Validate file size
            if (file.Length > _options.MaxFileSizeBytes)
                return BadRequest(new
                {
                    error = $"File exceeds maximum size of " +
                            $"{_options.MaxFileSizeBytes / 1024 / 1024}MB."
                });

            // Validate file is a PDF
            var buffer = new byte[4];
            await file.OpenReadStream().ReadAsync(
                buffer, 0, 4, cancellationToken);

            _logger.LogInformation(
                "PDF magic bytes: {B0} {B1} {B2} {B3}",
                buffer[0], buffer[1], buffer[2], buffer[3]);

            if (!IsPdf(buffer))
                return BadRequest(new { error = "File must be a valid PDF." });

            // Read file into memory
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream, cancellationToken);

            // Build request
            var request = new ProcessRequest
            {
                ToolType = parsedToolType,
                FileBytes = memoryStream.ToArray(),
                FileName = file.FileName,
                FileSizeBytes = file.Length
            };

            _logger.LogInformation(
                "Calling PdfService.ProcessAsync for job {JobId}",
                request.JobId);

            try
            {
                var response = await _pdfService.ProcessAsync(
                    request, cancellationToken);

                _logger.LogInformation(
                    "PdfService returned — IsSuccess: {IsSuccess}, Error: {Error}",
                    response.IsSuccess,
                    response.ErrorMessage ?? "none");

                if (!response.IsSuccess)
                {
                    _logger.LogWarning(
                        "Processing failed for job {JobId}: {Error}",
                        response.JobId,
                        response.ErrorMessage);

                    return UnprocessableEntity(new
                    {
                        error = response.ErrorMessage
                    });
                }

                _logger.LogInformation(
                    "Job {JobId} completed. Compression ratio: {Ratio}%",
                    response.JobId,
                    response.CompressionRatio);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Exception in Process endpoint: {Message}", ex.Message);

                return StatusCode(500, new
                {
                    error = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }

        // POST api/pdf/merge
        [HttpPost("merge")]
        [RequestSizeLimit(209715200)] // 200MB total
        public async Task<IActionResult> Merge(
            List<IFormFile> files,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Merge endpoint hit — {Count} files", files.Count);

            if (files == null || files.Count < 2)
                return BadRequest(new
                {
                    error = "Please upload at least 2 PDF files."
                });

            if (files.Count > 10)
                return BadRequest(new
                {
                    error = "Maximum 10 files can be merged at once."
                });

            // Validate and read all files
            var fileBytesList = new List<byte[]>();

            foreach (var file in files)
            {
                if (file.Length > _options.MaxFileSizeBytes)
                    return BadRequest(new
                    {
                        error = $"{file.FileName} exceeds 50MB limit."
                    });

                var buffer = new byte[4];
                await file.OpenReadStream()
                    .ReadAsync(buffer, 0, 4, cancellationToken);

                if (!IsPdf(buffer))
                    return BadRequest(new
                    {
                        error = $"{file.FileName} is not a valid PDF."
                    });

                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, cancellationToken);
                fileBytesList.Add(ms.ToArray());
            }

            // Build request with all files
            var request = new ProcessRequest
            {
                ToolType = ToolType.MergePdf,
                FileBytes = fileBytesList[0],
                FileName = files[0].FileName,
                FileSizeBytes = files[0].Length,
                AdditionalFiles = fileBytesList.Skip(1).ToList(),
                AdditionalFileNames = files.Skip(1)
                    .Select(f => f.FileName).ToList()
            };

            try
            {
                // Use MergeStrategy directly with additional files
                var merger = new PdfToolkit.Infrastructure
                    .Processors.PdfMerger(request.AdditionalFiles);

                var outputBytes = await merger.ProcessAsync(
                    request.FileBytes, cancellationToken);

                var result = ProcessingResult.Success(
                    outputBytes, request.FileSizeBytes);

                return File(outputBytes, "application/pdf", "merged.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Merge failed: {Message}", ex.Message);
                return StatusCode(500, new
                {
                    error = ex.Message
                });
            }
        }

        // POST api/pdf/detect-fields
        [HttpPost("detect-fields")]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> DetectFields(
            IFormFile file,
            CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided." });

            var buffer = new byte[4];
            await file.OpenReadStream()
                .ReadAsync(buffer, 0, 4, cancellationToken);

            if (!IsPdf(buffer))
                return BadRequest(new
                {
                    error = "File must be a valid PDF."
                });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);

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
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided." });

            var buffer = new byte[4];
            await file.OpenReadStream()
                .ReadAsync(buffer, 0, 4, cancellationToken);

            if (!IsPdf(buffer))
                return BadRequest(new
                {
                    error = "File must be a valid PDF."
                });

            // Parse field values from JSON
            var fieldValues = System.Text.Json.JsonSerializer
                .Deserialize<Dictionary<string, string>>(fieldsJson)
                ?? new Dictionary<string, string>();

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);

            var filler = new PdfFormFiller(fieldValues);
            var outputBytes = await filler.ProcessAsync(
                ms.ToArray(), cancellationToken);

            return File(outputBytes, "application/pdf",
                "filled_form.pdf");
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

            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided." });

            var buffer = new byte[4];
            await file.OpenReadStream()
                .ReadAsync(buffer, 0, 4, cancellationToken);

            if (!IsPdf(buffer))
                return BadRequest(new
                {
                    error = "File must be a valid PDF."
                });

            // Parse redaction regions
            var domainRegions = System.Text.Json.JsonSerializer
                .Deserialize<List<DomainRedactionRegion>>(regionsJson)
                ?? new List<DomainRedactionRegion>();

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);

            // Convert domain regions to processor regions
            var processorRegions = domainRegions.Select(r =>
            new PdfToolkit.Infrastructure.Processors.RedactionRegion
            {
                X1 = r.X1,
                Y1 = r.Y1,
                X2 = r.X2,
                Y2 = r.Y2,
                PageNumber = r.PageNumber
            });

            var redactor = new PdfRedactor(processorRegions);
            var outputBytes = await redactor.ProcessAsync(
                ms.ToArray(), cancellationToken);

            return File(outputBytes, "application/pdf",
                "redacted.pdf");
        }

        // POST api/pdf/page-count
        [HttpPost("page-count")]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> GetPageCount(
            IFormFile file,
            CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);

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
            var status = await _pdfService.GetJobStatusAsync(
                jobId, cancellationToken);

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

        private static bool IsPdf(byte[] bytes)
        {
            return bytes.Length >= 4 &&
                   bytes[0] == 0x25 &&
                   bytes[1] == 0x50 &&
                   bytes[2] == 0x44 &&
                   bytes[3] == 0x46;
        }

        // POST api/pdf/delete-pages
        [HttpPost("delete-pages")]
        [RequestSizeLimit(524288000)]
        public async Task<IActionResult> DeletePages(
            IFormFile file,
            [FromForm] string pageNumbers)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file provided.");

            try
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var bytes = ms.ToArray();

                var pages = pageNumbers
                    .Split(',', StringSplitOptions
                        .RemoveEmptyEntries)
                    .Select(p => int.Parse(p.Trim()))
                    .ToList();

                var processor = new DeletePagesProcessor();
                var result = await processor.ProcessAsync(
                    bytes, pages);

                return File(result, "application/pdf",
                    "deleted_pages.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delete pages error");
                return StatusCode(500, ex.Message);
            }
        }

        // POST api/pdf/word-to-pdf
        [HttpPost("word-to-pdf")]
        [RequestSizeLimit(524288000)]
        public async Task<IActionResult> WordToPdf(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file provided.");

            try
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var bytes = ms.ToArray();

                var processor = new WordToPdfProcessor();
                var result = await processor.ProcessAsync(bytes);

                return File(result, "application/pdf",
                    Path.GetFileNameWithoutExtension(
                        file.FileName) + ".pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Word to PDF error");
                return StatusCode(500, ex.Message);
            }
        }

        // POST api/pdf/organize
        [HttpPost("organize")]
        [RequestSizeLimit(524288000)]
        public async Task<IActionResult> OrganizePdf(
            IFormFile file,
            [FromForm] string operationsJson)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file provided.");

            try
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var bytes = ms.ToArray();

                var operations = System.Text.Json.JsonSerializer
                    .Deserialize<List<PageOperation>>(
                        operationsJson)
                    ?? new List<PageOperation>();

                var processor = new OrganizePdfProcessor();
                var result = await processor.ProcessAsync(
                    bytes, operations);

                return File(result, "application/pdf",
                    "organized.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Organize PDF error");
                return StatusCode(500, ex.Message);
            }
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
            [FromForm] int pageNumber = 1)
        {
            if (file == null || signature == null)
                return BadRequest("File and signature required.");

            try
            {
                using var fileMs = new MemoryStream();
                await file.CopyToAsync(fileMs);
                var fileBytes = fileMs.ToArray();

                using var sigMs = new MemoryStream();
                await signature.CopyToAsync(sigMs);
                var sigBytes = sigMs.ToArray();

                var processor = new SignPdfProcessor();
                var result = await processor.ProcessAsync(
                    fileBytes, sigBytes,
                    x, y, width, height, pageNumber);

                return File(result, "application/pdf",
                    "signed.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sign PDF error");
                return StatusCode(500, ex.Message);
            }
        }

        // POST api/pdf/edit
        [HttpPost("edit")]
        [RequestSizeLimit(524288000)]
        public async Task<IActionResult> EditPdf(
            IFormFile file,
            [FromForm] string annotationsJson)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file provided.");

            try
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var bytes = ms.ToArray();

                var annotations = System.Text.Json.JsonSerializer
                    .Deserialize<List<PdfAnnotationDto>>(
                        annotationsJson)
                    ?? new List<PdfAnnotationDto>();

                var iTextAnnotations = annotations.Select(a =>
                    new PdfAnnotation
                    {
                        Type = a.Type,
                        PageNumber = a.PageNumber,
                        X = a.X,
                        Y = a.Y,
                        X2 = a.X2,
                        Y2 = a.Y2,
                        Width = a.Width,
                        Height = a.Height,
                        Text = a.Text,
                        FontSize = a.FontSize,
                        LineWidth = a.LineWidth,
                        Color = ParseColor(a.Color)
                    }).ToList();

                var processor = new EditPdfProcessor();
                var result = await processor.ProcessAsync(
                    bytes, iTextAnnotations);

                return File(result, "application/pdf",
                    "edited.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Edit PDF error");
                return StatusCode(500, ex.Message);
            }
        }

        // POST api/pdf/annotate
        [HttpPost("annotate")]
        [RequestSizeLimit(524288000)]
        public async Task<IActionResult> AnnotatePdf(
            IFormFile file,
            [FromForm] string highlightsJson)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file provided.");

            try
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var bytes = ms.ToArray();

                var highlights = System.Text.Json.JsonSerializer
                    .Deserialize<List<PdfHighlightDto>>(
                        highlightsJson)
                    ?? new List<PdfHighlightDto>();

                var iTextHighlights = highlights.Select(h =>
                    new PdfHighlight
                    {
                        Type = h.Type,
                        PageNumber = h.PageNumber,
                        X = h.X,
                        Y = h.Y,
                        Width = h.Width,
                        Height = h.Height,
                        LineWidth = h.LineWidth,
                        StrokeColor = ParseColor(h.Color),
                        Points = h.Points?.Select(p =>
                            new Infrastructure.Processors.PointF
                            {
                                X = p.X,
                                Y = p.Y
                            }).ToList() ?? new()
                    }).ToList();

                var processor = new AnnotatePdfProcessor();
                var result = await processor.ProcessAsync(
                    bytes, iTextHighlights);

                return File(result, "application/pdf",
                    "annotated.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Annotate PDF error");
                return StatusCode(500, ex.Message);
            }
        }

        private static BaseColor ParseColor(string? hex)
        {
            if (string.IsNullOrEmpty(hex)) return new BaseColor(0, 0, 0);
            hex = hex.TrimStart('#');
            if (hex.Length < 6) return new BaseColor(0, 0, 0);
            var r = Convert.ToInt32(hex[..2], 16);
            var g = Convert.ToInt32(hex[2..4], 16);
            var b = Convert.ToInt32(hex[4..6], 16);
            return new BaseColor(r, g, b);
        }

        // DTOs for JSON deserialization
        public class PdfAnnotationDto
        {
            public string Type { get; set; } = "text";
            public int PageNumber { get; set; } = 1;
            public float X { get; set; }
            public float Y { get; set; }
            public float X2 { get; set; }
            public float Y2 { get; set; }
            public float Width { get; set; }
            public float Height { get; set; }
            public string? Text { get; set; }
            public float FontSize { get; set; } = 12f;
            public float LineWidth { get; set; } = 1f;
            public string? Color { get; set; }
        }

        public class PdfHighlightDto
        {
            public string Type { get; set; } = "highlight";
            public int PageNumber { get; set; } = 1;
            public float X { get; set; }
            public float Y { get; set; }
            public float Width { get; set; }
            public float Height { get; set; }
            public float LineWidth { get; set; } = 2f;
            public string? Color { get; set; }
            public List<PointDto>? Points { get; set; }
        }

        public class PointDto
        {
            public float X { get; set; }
            public float Y { get; set; }
        }
    }
}
