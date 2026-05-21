using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace PdfToolStack.Web.Services
{
    public sealed class OptionalAuthorizationMessageHandler : DelegatingHandler
    {
        private readonly IAccessTokenProvider _tokenProvider;
        private readonly AuthenticationStateProvider _authStateProvider;

        public OptionalAuthorizationMessageHandler(
            IAccessTokenProvider tokenProvider,
            AuthenticationStateProvider authStateProvider)
        {
            _tokenProvider = tokenProvider;
            _authStateProvider = authStateProvider;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            if (authState.User.Identity?.IsAuthenticated == true)
            {
                var tokenResult = await _tokenProvider.RequestAccessToken();
                if (tokenResult.TryGetToken(out var token))
                {
                    request.Headers.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue(
                            "Bearer",
                            token.Value);
                }
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
