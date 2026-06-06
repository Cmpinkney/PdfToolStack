using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PdfToolStack.Domain.Interfaces;
using PdfToolStack.Infrastructure.Data;
using System.Security.Claims;

namespace PdfToolStack.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IUserDeletionService? _deletionService;
        private readonly AppDbContext _db;
        private readonly ILogger<UserController> _logger;

        public UserController(
            ILogger<UserController> logger,
            AppDbContext db,
            IUserDeletionService? deletionService = null)
        {
            _logger = logger;
            _db = db;
            _deletionService = deletionService;
        }

        [HttpDelete("delete-account")]
        public async Task<IActionResult> DeleteAccount(
            CancellationToken cancellationToken)
        {
            if (_deletionService is null)
                return StatusCode(503,
                    new { error = "Database not configured." });

            var userId = User.FindFirst("sub")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(
                    new { error = "User not authenticated." });

            try
            {
                await _deletionService.DeleteUserDataAsync(
                    userId, cancellationToken);

                return Ok(new
                {
                    message = "Your account and all associated data have been permanently deleted."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Account deletion failed for {UserId}", userId);
                return StatusCode(500,
                    new { error = "Deletion failed. Please contact admin@pdftoolstack.com" });
            }
        }

        // GET api/user/memory-settings
        [HttpGet("memory-settings")]
        [Authorize]
        public async Task<IActionResult> GetMemorySettings(
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var settings = await _db.UserMemorySettings
                .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

            return Ok(new
            {
                enabled = settings?.MemoryEnabled ?? false,
                enabledAt = settings?.EnabledAt
            });
        }

        // POST api/user/memory-settings
        [HttpPost("memory-settings")]
        [Authorize]
        public async Task<IActionResult> UpdateMemorySettings(
            [FromBody] MemorySettingsRequest request,
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var settings = await _db.UserMemorySettings
                .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

            if (settings is null)
            {
                settings = new PdfToolStack.Domain.Entities.UserMemorySettings
                {
                    UserId = userId,
                    MemoryEnabled = request.Enabled,
                    EnabledAt = request.Enabled ? DateTime.UtcNow : default,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.UserMemorySettings.Add(settings);
            }
            else
            {
                settings.MemoryEnabled = request.Enabled;
                if (request.Enabled && settings.EnabledAt == default)
                    settings.EnabledAt = DateTime.UtcNow;
                settings.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "[AUDIT] MemorySettings UserId={UserId} Enabled={Enabled}",
                userId, request.Enabled);

            return Ok(new { enabled = settings.MemoryEnabled });
        }

        public record MemorySettingsRequest(bool Enabled);
    }
}
