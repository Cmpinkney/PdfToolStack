using PdfToolStack.Application.DTOs;

namespace PdfToolStack.Web.Services
{
    public class SubscriptionService
    {
        private readonly ApiService _api;
        private SubscriptionStatusDto? _cachedStatus;
        private DateTime _cacheExpiry;
        private string _cachedUserId = string.Empty; // ← guard: cache is per user

        public SubscriptionService(ApiService api)
        {
            _api = api;
        }

        // ── Subscription status ───────────────────────────────────────────────────

        public async Task<SubscriptionStatusDto> GetStatusAsync(string userId)
        {
            // Guard: never serve cached status for a different user
            if (string.IsNullOrWhiteSpace(userId))
                return new SubscriptionStatusDto();

            // Invalidate cache if userId changed (e.g. admin → free user in same session)
            if (_cachedUserId != userId)
                ClearCache();

            if (_cachedStatus != null && DateTime.UtcNow < _cacheExpiry)
                return _cachedStatus;

            try
            {
                var status = await _api.GetAsync<SubscriptionStatusDto>(
                    $"api/subscription/status/{userId}");

                _cachedStatus = status ?? new SubscriptionStatusDto();
                _cachedUserId = userId;
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

        public void ClearCache()
        {
            _cachedStatus = null;
            _cachedUserId = string.Empty;
        }

        public bool IsPro(SubscriptionStatusDto status) => status.HasPro;
    }
}