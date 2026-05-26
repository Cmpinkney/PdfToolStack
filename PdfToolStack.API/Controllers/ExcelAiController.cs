using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PdfToolStack.Application.DTOs;
using PdfToolStack.Application.Interfaces;

namespace PdfToolStack.API.Controllers;

[ApiController]
[Route("api/excel-ai")]
public sealed class ExcelAiController : ControllerBase
{
    private readonly IFormulaGenerationService _formulaGenerationService;
    private readonly ILogger<ExcelAiController> _logger;

    public ExcelAiController(
        IFormulaGenerationService formulaGenerationService,
        ILogger<ExcelAiController> logger)
    {
        _formulaGenerationService = formulaGenerationService;
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
}
