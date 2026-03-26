using PdfToolStack.Application.DTOs;
using Stripe;
using Stripe.Checkout;

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

        public async Task<SubscriptionStatusDto>
            GetStatusAsync(string userId)
        {
            if (_cachedStatus != null &&
                DateTime.UtcNow < _cacheExpiry)
                return _cachedStatus;

            try
            {
                var status = await _api.GetAsync
                    <SubscriptionStatusDto> (
                    $"api/subscription/status/{userId}");

                _cachedStatus = status
                    ?? new SubscriptionStatusDto();
                _cacheExpiry =
                    DateTime.UtcNow.AddMinutes(5);
                return _cachedStatus;
            }
            catch
            {
                return new SubscriptionStatusDto();
            }
        }

        public async Task<string>
            CreateCheckoutSessionAsync(
            string priceId,
            string userId,
            string email,
            string baseUrl)
        {
            var dto = new CreateCheckoutDto
            {
                PriceId = priceId,
                UserId = userId,
                Email = email,
                SuccessUrl = baseUrl +
                    "/subscription/success",
                CancelUrl = baseUrl + "/pricing"
            };

            var response = await _api.PostAsync
                <CreateCheckoutDto,
                CheckoutResponseDto> (
                "api/subscription/create-checkout",
                dto);

            return response?.Url ?? string.Empty;
        }

        public async Task<string>
            CreatePortalSessionAsync(
            string userId,
            string baseUrl)
        {
            var dto = new CreatePortalDto
            {
                UserId = userId,
                ReturnUrl = baseUrl + "/account"
            };

            var response = await _api.PostAsync
                <CreatePortalDto,
                CheckoutResponseDto> (
                "api/subscription/create-portal",
                dto);

            return response?.Url ?? string.Empty;
        }

        public void ClearCache()
        {
            _cachedStatus = null;
        }

        public bool IsPro(
            SubscriptionStatusDto status)
            => status.HasPro;
    }
}