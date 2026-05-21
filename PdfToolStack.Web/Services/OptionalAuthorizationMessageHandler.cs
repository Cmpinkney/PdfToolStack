using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.JSInterop;

namespace PdfToolStack.Web.Services
{
    public sealed class OptionalAuthorizationMessageHandler : DelegatingHandler
    {
        private readonly IAccessTokenProvider _tokenProvider;
        private readonly IJSRuntime _js;
        private readonly IConfiguration _configuration;

        public OptionalAuthorizationMessageHandler(
            IAccessTokenProvider tokenProvider,
            IJSRuntime js,
            IConfiguration configuration)
        {
            _tokenProvider = tokenProvider;
            _js = js;
            _configuration = configuration;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (await HasCachedUserAsync(cancellationToken))
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

        private async Task<bool> HasCachedUserAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await _js.InvokeAsync<bool>(
                    "pdfToolStackAuth.hasCachedUser",
                    cancellationToken,
                    _configuration["Auth0:Authority"],
                    _configuration["Auth0:ClientId"]);
            }
            catch
            {
                return false;
            }
        }
    }
}
