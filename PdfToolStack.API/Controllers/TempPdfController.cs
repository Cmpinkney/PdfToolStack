using Microsoft.AspNetCore.Mvc;

namespace PdfToolStack.API.Controllers;

[ApiController]
[Route("api/temp-pdf")]
public class TempPdfController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<TempPdfController> _logger;

    private static readonly TimeSpan FileLifetime = TimeSpan.FromHours(1);
    private const long MaxFileSizeBytes = 524_288_000; // 500 MB

    public TempPdfController(
        IWebHostEnvironment environment,
        ILogger<TempPdfController> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<ActionResult<TempPdfUploadResponse>> UploadPdf(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("No file was uploaded.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest("File is too large.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only PDF files are supported.");
        }

        CleanupExpiredFiles();

        var tempFolder = GetTempFolder();
        Directory.CreateDirectory(tempFolder);

        var id = Guid.NewGuid().ToString("N");
        var safeFileName = Path.GetFileName(file.FileName);
        var storedFileName = $"{id}.pdf";
        var filePath = Path.Combine(tempFolder, storedFileName);

        await using (var stream = System.IO.File.Create(filePath))
        {
            await file.CopyToAsync(stream);
        }

        var relativeUrl = $"/api/temp-pdf/{id}";
        var absoluteUrl = $"{Request.Scheme}://{Request.Host}{relativeUrl}";

        _logger.LogInformation("Stored temporary PDF {OriginalFileName} as {StoredFileName}", safeFileName, storedFileName);

        return Ok(new TempPdfUploadResponse
        {
            Id = id,
            FileName = safeFileName,
            Url = absoluteUrl
        });
    }

    [HttpGet("{id}")]
    public IActionResult GetPdf(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest();
        }

        var tempFolder = GetTempFolder();
        var filePath = Path.Combine(tempFolder, $"{id}.pdf");

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        var fileInfo = new FileInfo(filePath);
        if (DateTime.UtcNow - fileInfo.CreationTimeUtc > FileLifetime)
        {
            TryDelete(filePath);
            return NotFound();
        }

        return PhysicalFile(filePath, "application/pdf", enableRangeProcessing: true);
    }

    private string GetTempFolder()
    {
        return Path.Combine(_environment.ContentRootPath, "App_Data", "TempPdfs");
    }

    private void CleanupExpiredFiles()
    {
        try
        {
            var tempFolder = GetTempFolder();
            if (!Directory.Exists(tempFolder))
            {
                return;
            }

            foreach (var filePath in Directory.GetFiles(tempFolder, "*.pdf"))
            {
                var fileInfo = new FileInfo(filePath);
                if (DateTime.UtcNow - fileInfo.CreationTimeUtc > FileLifetime)
                {
                    TryDelete(filePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed while cleaning up expired temp PDFs.");
        }
    }

    private void TryDelete(string filePath)
    {
        try
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete temp PDF {FilePath}", filePath);
        }
    }

    public sealed class TempPdfUploadResponse
    {
        public string Id { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}