using Microsoft.AspNetCore.Mvc;
using PdfToolStack.Domain.Interfaces;
using System.Security.Claims;

namespace PdfToolStack.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IUserDeletionService? _deletionService;
        private readonly ILogger<UserController> _logger;

        public UserController(
            ILogger<UserController> logger,
            IUserDeletionService? deletionService = null)
        {
            _logger = logger;
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
                    new { error = "Deletion failed. Please contact support@pdftoolstack.com" });
            }
        }
    }
}