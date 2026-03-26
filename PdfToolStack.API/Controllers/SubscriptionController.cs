using Microsoft.AspNetCore.Mvc;
using PdfToolStack.Application.DTOs;
using PdfToolStack.Infrastructure.Services;

namespace PdfToolStack.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubscriptionController : ControllerBase
    {
        private readonly SubscriptionService _service;

        public SubscriptionController(
            SubscriptionService service)
        {
            _service = service;
        }

        [HttpGet("status/{userId}")]
        public async Task<IActionResult> GetStatus(
            string userId)
        {
            var status =
                await _service.GetStatusAsync(userId);
            return Ok(status);
        }

        [HttpPost("create-checkout")]
        public async Task<IActionResult> CreateCheckout(
            [FromBody] CreateCheckoutDto dto)
        {
            try
            {
                var url = await _service
                    .CreateCheckoutSessionAsync(dto);
                return Ok(new CheckoutResponseDto
                { Url = url });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("create-portal")]
        public async Task<IActionResult> CreatePortal(
            [FromBody] CreatePortalDto dto)
        {
            try
            {
                var url = await _service
                    .CreatePortalSessionAsync(dto);
                return Ok(new CheckoutResponseDto
                { Url = url });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook()
        {
            var json = await new StreamReader(
                HttpContext.Request.Body)
                .ReadToEndAsync();

            var signature = Request.Headers[
                "Stripe-Signature"].ToString();

            try
            {
                await _service.HandleWebhookAsync(
                    json, signature);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("history/{userId}")]
        public async Task<IActionResult> GetHistory(
            string userId)
        {
            var history = await _service
                .GetDownloadHistoryAsync(userId);
            return Ok(history);
        }
    }
}