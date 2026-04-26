using PdfToolStack.Application.DTOs;

namespace PdfToolStack.Web.Services
{
    public class SubscriptionService
    {
        private readonly ApiService _api;
        private SubscriptionStatusDto? _cachedStatus;
        private DateTime _cacheExpiry;

        public SubscriptionService(ApiService api)
        {
            _api = api;
        }

        // ── Subscription status ───────────────────────────────────────────────────

        public async Task<SubscriptionStatusDto> GetStatusAsync(string userId)
        {
            if (_cachedStatus != null && DateTime.UtcNow < _cacheExpiry)
                return _cachedStatus;

            try
            {
                var status = await _api.GetAsync<SubscriptionStatusDto>(
                    $"api/subscription/status/{userId}");

                _cachedStatus = status ?? new SubscriptionStatusDto();
                _cacheExpiry = DateTime.UtcNow.AddMinutes(5);
                return _cachedStatus;
            }
            catch
            {
                return new SubscriptionStatusDto();
            }
        }

        // ── Subscription checkout (recurring) ─────────────────────────────────────

        public async Task<string> CreateCheckoutSessionAsync(
            string priceId, string userId, string email, string baseUrl)
        {
            var dto = new CreateCheckoutDto
            {
                PriceId = priceId,
                UserId = userId,
                Email = email,
                SuccessUrl = baseUrl + "/subscription/success",
                CancelUrl = baseUrl + "/pricing"
            };

            var response = await _api.PostAsync<CreateCheckoutDto, CheckoutResponseDto>(
                "api/subscription/create-checkout", dto);

            return response?.Url ?? string.Empty;
        }

        // ── Add-on checkout (one-time payment) ────────────────────────────────────

        /// <summary>
        /// Creates a one-time payment checkout for a pay-as-you-go add-on.
        /// UserId and Email are passed in the request body — the API has no auth middleware.
        ///
        /// addonType: "large_file" | "ai_day_pass" | "ai_credit_pack" | "batch_unlock"
        /// returnPath: optional path to redirect back to after success (e.g. "/compress-pdf")
        /// </summary>
        public async Task<string> CreateAddonCheckoutSessionAsync(
            string priceId,
            string addonType,
            string userId,
            string email,
            string baseUrl,
            string? returnPath = null)
        {
            var successUrl = string.IsNullOrEmpty(returnPath)
                ? baseUrl + "/subscription/success"
                : baseUrl + returnPath;

            var cancelUrl = string.IsNullOrEmpty(returnPath)
                ? baseUrl + "/pricing"
                : baseUrl + returnPath;

            var dto = new AddonCheckoutRequest
            {
                PriceId = priceId,
                AddonType = addonType,
                UserId = userId,
                Email = email,
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl
            };

            var response = await _api.PostAsync<AddonCheckoutRequest, CheckoutResponseDto>(
                "api/subscription/create-addon-checkout", dto);

            return response?.Url ?? string.Empty;
        }

        // ── Portal ────────────────────────────────────────────────────────────────

        public async Task<string> CreatePortalSessionAsync(string userId, string baseUrl)
        {
            var dto = new CreatePortalDto
            {
                UserId = userId,
                ReturnUrl = baseUrl + "/account"
            };

            var response = await _api.PostAsync<CreatePortalDto, CheckoutResponseDto>(
                "api/subscription/create-portal", dto);

            return response?.Url ?? string.Empty;
        }

        // ── Cache ─────────────────────────────────────────────────────────────────

        public void ClearCache() => _cachedStatus = null;

        public bool IsPro(SubscriptionStatusDto status) => status.HasPro;
    }
}
