using Microsoft.JSInterop;

namespace PdfToolStack.Web.Services
{
    public class SessionUsageService
    {
        private int _toolsCompletedThisSession = 0;
        private readonly HashSet<string> _toolsUsed = new();
        private readonly IJSRuntime _js;
        private const string StorageKey = "toolsCompleted";

        /// <summary>
        /// Set by MainLayout after auth resolves. RecordToolCompletion returns
        /// early for any paid plan so Pro/Teams users never see the upsell modal.
        /// </summary>
        public string? CurrentPlanType { get; set; }

        public int ToolsCompleted => _toolsCompletedThisSession;
        public bool ShouldShowUpsell => _toolsCompletedThisSession >= 3;

        public event Action? OnUpsellTriggered;

        public SessionUsageService(IJSRuntime js)
        {
            _js = js;
        }

        /// <summary>
        /// Call from MainLayout.OnInitializedAsync to restore the count from the
        /// browser's sessionStorage (resets automatically when the tab closes).
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                var stored = await _js.InvokeAsync<string?>("sessionStorage.getItem", StorageKey);
                if (int.TryParse(stored, out var count))
                    _toolsCompletedThisSession = count;
            }
            catch { }
        }

        public void RecordToolCompletion(string toolName, string? planType = null)
        {
            var effectivePlan = planType ?? CurrentPlanType;
            if (effectivePlan?.ToLower() is "monthly" or "yearly" or "teams")
                return;

            _toolsCompletedThisSession++;
            _toolsUsed.Add(toolName);
            _ = SaveAsync();

            if (_toolsCompletedThisSession == 3)
                OnUpsellTriggered?.Invoke();
        }

        private async Task SaveAsync()
        {
            try
            {
                await _js.InvokeVoidAsync("sessionStorage.setItem",
                    StorageKey, _toolsCompletedThisSession.ToString());
            }
            catch { }
        }

        public void Reset()
        {
            _toolsCompletedThisSession = 0;
            _ = SaveAsync();
        }
    }
}
