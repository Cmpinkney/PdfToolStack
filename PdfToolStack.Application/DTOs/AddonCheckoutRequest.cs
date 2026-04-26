namespace PdfToolStack.Application.DTOs
{
    /// <summary>
    /// Request body for POST api/subscription/create-addon-checkout.
    /// UserId comes from the client (same pattern as CreateCheckoutDto)
    /// because the API has no auth middleware — no JWT parsing in controllers.
    /// </summary>
    public class AddonCheckoutRequest
    {
        public string PriceId { get; set; } = string.Empty;

        /// <summary>
        /// "large_file" | "ai_day_pass" | "ai_credit_pack" | "batch_unlock"
        /// Written to Stripe session metadata so the webhook can grant the right entitlement.
        /// </summary>
        public string AddonType { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string SuccessUrl { get; set; } = string.Empty;
        public string CancelUrl { get; set; } = string.Empty;
    }
}
