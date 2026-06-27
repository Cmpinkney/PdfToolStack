using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PdfToolStack.Application.DTOs;
using PdfToolStack.Application.Interfaces;
using PdfToolStack.Infrastructure.Services;

namespace PdfToolStack.API.Controllers;

[ApiController]
[Route("api/excel-ai")]
public sealed class ExcelAiController : ControllerBase
{
    private readonly IFormulaGenerationService _formulaGenerationService;
    private readonly AiService _aiService;
    private readonly ILogger<ExcelAiController> _logger;

    public ExcelAiController(
        IFormulaGenerationService formulaGenerationService,
        AiService aiService,
        ILogger<ExcelAiController> logger)
    {
        _formulaGenerationService = formulaGenerationService;
        _aiService = aiService;
        _logger = logger;
    }

    [HttpPost("formula")]
    [AllowAnonymous]
    [EnableRateLimiting("FormulaAiPerIp")]
    public async Task<ActionResult<FormulaResponse>> GenerateFormula(
        [FromBody] FormulaRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            _logger.LogWarning("Formula generation rejected: request body missing.");
            return BadRequest(new { error = "Formula request is required." });
        }

        try
        {
            var response = await _formulaGenerationService.GenerateAsync(
                request,
                cancellationToken);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(
                ex,
                "Formula generation rejected: invalid input. PromptLength: {PromptLength}, Platform: {Platform}",
                request.Prompt?.Length ?? 0,
                request.Platform);

            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Formula generation failed. PromptLength: {PromptLength}, Platform: {Platform}",
                request.Prompt?.Length ?? 0,
                request.Platform);

            return StatusCode(503, new { error = ex.Message });
        }
    }

    // POST api/excel-ai/extract-invoice
    // Anonymous, IP-rate-limited (3/day) free invoice extraction for ExcelToolStack.
    [HttpPost("extract-invoice")]
    [AllowAnonymous]
    [EnableRateLimiting("InvoiceExtractPerIp")]
    [RequestSizeLimit(52428800)]
    public async Task<IActionResult> ExtractInvoiceToExcel(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var fileName = file?.FileName ?? "(missing)";
        var fileSize = file?.Length ?? 0;

        // TODO: Extract IsValidPdf, CountPdfPages, BuildExcel into shared
        // IPdfUtilityService to remove cross-controller static dependency.
        // Tracked as tech debt — both controllers should depend on an abstraction.
        if (!AiController.IsValidPdf(file))
            return BadRequest(new { error = "Please upload a valid PDF file under 50MB." });

        try
        {
            _logger.LogInformation(
                "Anonymous invoice extraction received. FileName: {FileName}, FileSize: {FileSize}, RemoteIp: {RemoteIp}",
                fileName,
                fileSize,
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

            using var ms = new MemoryStream();
            await file!.CopyToAsync(ms, cancellationToken);
            var pdfBytes = ms.ToArray();

            int? pageCount = null;
            try
            {
                pageCount = AiController.CountPdfPages(pdfBytes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Anonymous invoice page count failed. FileName: {FileName}, FileSize: {FileSize}",
                    fileName,
                    fileSize);
            }

            var result = await _aiService.ExtractDataAsync(
                pdfBytes,
                "invoice",
                cancellationToken,
                userId: "anonymous",
                isProUser: false,
                pageCount);

            if (!result.IsSuccess)
            {
                var body = new
                {
                    error = result.ErrorMessage,
                    ocrFallbackUsed = result.OcrFallbackUsed,
                    ocrWarning = result.OcrWarning
                };

                return result.FailureKind switch
                {
                    ExtractionFailureKind.NoReadableText => BadRequest(body),
                    ExtractionFailureKind.RequiresUpgrade => StatusCode(402, body),
                    ExtractionFailureKind.AiConfiguration => StatusCode(503, body),
                    _ => UnprocessableEntity(body)
                };
            }

            var excelBytes = AiController.BuildExcel(result.JsonData, "invoice");
            var outName = $"invoice_extracted_{DateTime.UtcNow:yyyyMMdd}.xlsx";

            _logger.LogInformation(
                "Anonymous invoice extraction completed. FileName: {FileName}, OutputBytes: {OutputBytes}, OcrFallbackUsed: {OcrFallbackUsed}",
                fileName,
                excelBytes.Length,
                result.OcrFallbackUsed);

            return File(excelBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                outName);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Anonymous invoice extraction failed unexpectedly. FileName: {FileName}, FileSize: {FileSize}",
                fileName,
                fileSize);

            return UnprocessableEntity(new
            {
                error = "Unable to extract structured invoice data from this document."
            });
        }
    }
}
