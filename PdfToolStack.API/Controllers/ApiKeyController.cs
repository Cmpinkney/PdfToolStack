using Microsoft.AspNetCore.Mvc;
using PdfToolStack.Domain.Interfaces;
using System.Security.Claims;

namespace PdfToolStack.API.Controllers
{
    [ApiController]
    [Route("api/keys")]
    public class ApiKeyController : ControllerBase
    {
        private readonly IApiKeyService? _keyService;
        private readonly ILogger<ApiKeyController> _logger;

        public ApiKeyController(
            ILogger<ApiKeyController> logger,
            IApiKeyService? keyService = null)
        {
            _logger = logger;
            _keyService = keyService;
        }

        [HttpGet]
        public async Task<IActionResult> GetKeys(
            CancellationToken ct)
        {
            if (_keyService is null)
                return StatusCode(503,
                    new { error = "Database not configured." });

            var userId = GetUserId();
            if (userId is null) return Unauthorized();

            var keys = await _keyService
                .GetUserKeysAsync(userId, ct);

            return Ok(keys.Select(k => new
            {
                k.Id,
                k.Name,
                k.KeyPrefix,
                k.IsActive,
                k.RequestsThisMonth,
                k.MonthlyLimit,
                k.CreatedAt,
                k.LastUsedAt
            }));
        }

        [HttpPost]
        public async Task<IActionResult> CreateKey(
            [FromBody] CreateApiKeyRequest request,
            CancellationToken ct)
        {
            if (_keyService is null)
                return StatusCode(503,
                    new { error = "Database not configured." });

            var userId = GetUserId();
            if (userId is null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(
                    new { error = "Key name is required." });

            var (key, rawKey) = await _keyService
                .CreateKeyAsync(userId, request.Name, ct);

            _logger.LogInformation(
                "API key created for user {UserId}", userId);

            // Return raw key ONCE — never stored
            return Ok(new
            {
                key.Id,
                key.Name,
                key.KeyPrefix,
                RawKey = rawKey,
                key.MonthlyLimit,
                key.CreatedAt,
                Message = "Store this key securely — it will not be shown again."
            });
        }

        [HttpDelete("{keyId:int}")]
        public async Task<IActionResult> RevokeKey(
            int keyId,
            CancellationToken ct)
        {
            if (_keyService is null)
                return StatusCode(503,
                    new { error = "Database not configured." });

            var userId = GetUserId();
            if (userId is null) return Unauthorized();

            await _keyService.RevokeKeyAsync(keyId, userId, ct);

            return Ok(new { message = "Key revoked." });
        }

        private string? GetUserId() =>
            User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    public class CreateApiKeyRequest
    {
        public string Name { get; set; } = string.Empty;
    }
}