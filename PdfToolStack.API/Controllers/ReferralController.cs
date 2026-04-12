using Microsoft.AspNetCore.Mvc;
using PdfToolStack.Domain.Interfaces;
using System.Security.Claims;

namespace PdfToolStack.API.Controllers
{
    [ApiController]
    [Route("api/referral")]
    public class ReferralController : ControllerBase
    {
        private readonly IReferralService? _referralService;
        private readonly ILogger<ReferralController> _logger;

        public ReferralController(
            ILogger<ReferralController> logger,
            IReferralService? referralService = null)
        {
            _logger = logger;
            _referralService = referralService;
        }

        // GET api/referral/my-code
        [HttpGet("my-code")]
        public async Task<IActionResult> GetMyCode(
            CancellationToken ct)
        {
            if (_referralService is null)
                return StatusCode(503,
                    new { error = "Not configured." });

            var userId = GetUserId();
            if (userId is null) return Unauthorized();

            var code = await _referralService
                .GetOrCreateReferralCodeAsync(userId, ct);

            return Ok(new { code });
        }

        // GET api/referral/stats
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats(
            CancellationToken ct)
        {
            if (_referralService is null)
                return StatusCode(503,
                    new { error = "Not configured." });

            var userId = GetUserId();
            if (userId is null) return Unauthorized();

            var referrals = await _referralService
                .GetReferrerStatsAsync(userId, ct);

            return Ok(new
            {
                Total = referrals.Count,
                Converted = referrals.Count(r =>
                    r.Status >= Domain.Entities
                        .ReferralStatus.Converted),
                Rewarded = referrals.Count(r =>
                    r.Status == Domain.Entities
                        .ReferralStatus.Rewarded),
                Referrals = referrals.Select(r => new
                {
                    r.ReferralCode,
                    r.Status,
                    r.CreatedAt,
                    r.ConvertedAt,
                    r.RewardedAt
                })
            });
        }

        // POST api/referral/track
        [HttpPost("track")]
        public async Task<IActionResult> Track(
            [FromBody] TrackReferralRequest request,
            CancellationToken ct)
        {
            if (_referralService is null)
                return Ok(); // Fail silently

            var userId = GetUserId();
            await _referralService.TrackClickAsync(
                request.Code, userId, ct);

            return Ok();
        }

        private string? GetUserId() =>
            User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    public class TrackReferralRequest
    {
        public string Code { get; set; } = string.Empty;
    }
}