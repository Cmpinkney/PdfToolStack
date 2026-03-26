using Microsoft.JSInterop;

namespace PdfToolStack.Web.Services
{
    public class CookieConsentService
    {
        private readonly IJSRuntime _js;
        private const string ConsentKey =
            "pdftoolstack-cookie-consent";

        public CookieConsentService(IJSRuntime js)
        {
            _js = js;
        }

        public async Task<bool> HasConsentAsync()
        {
            try
            {
                var result = await _js.InvokeAsync<string>(
                    "localStorage.getItem", ConsentKey);
                return result == "accepted" ||
                       result == "declined";
            }
            catch { return false; }
        }

        public async Task AcceptAsync()
        {
            await _js.InvokeVoidAsync(
                "localStorage.setItem",
                ConsentKey, "accepted");
        }

        public async Task DeclineAsync()
        {
            await _js.InvokeVoidAsync(
                "localStorage.setItem",
                ConsentKey, "declined");
        }

        public async Task<bool> IsAcceptedAsync()
        {
            try
            {
                var result = await _js.InvokeAsync<string>(
                    "localStorage.getItem", ConsentKey);
                return result == "accepted";
            }
            catch { return false; }
        }
    }
}