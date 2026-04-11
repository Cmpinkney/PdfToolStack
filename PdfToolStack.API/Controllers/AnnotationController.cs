using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PdfToolStack.Domain.DTOs;
using PdfToolStack.Infrastructure.Services;

[ApiController]
[Route("api/annotation")]
[AllowAnonymous]
public class AnnotationController : ControllerBase
{
    [HttpPost("apply")]
    public async Task<IActionResult> ApplyAnnotations([FromBody] ApplyAnnotationsRequest request)
    {
        if (request == null) return BadRequest("Request is null");
        if (string.IsNullOrEmpty(request.PdfBase64)) return BadRequest("PdfBase64 is empty");
        if (request.Annotations == null) return BadRequest("Annotations is null");

        try
        {
            var pdfBytes = Convert.FromBase64String(request.PdfBase64);
            var resultBytes = PdfAnnotationService.Apply(pdfBytes, request.Annotations);
            return File(resultBytes, "application/pdf", request.FileName ?? "annotated.pdf");
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message, stack = ex.StackTrace });
        }
    }
}