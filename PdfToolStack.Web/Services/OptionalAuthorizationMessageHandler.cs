using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace PdfToolStack.Web.Services
{
    public sealed class OptionalAuthorizationMessageHandler : DelegatingHandler
    {
        private readonly IAccessTokenProvider _tokenProvider;

        public OptionalAuthorizationMessageHandler(
            IAccessTokenProvider tokenProvider)
        {
            _tokenProvider = tokenProvider;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var tokenResult = await _tokenProvider.RequestAccessToken();
            if (tokenResult.TryGetToken(out var token))
            {
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer",
                        token.Value);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
