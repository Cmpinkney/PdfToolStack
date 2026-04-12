namespace PdfToolStack.Web.Services
{
    public class SessionUsageService
    {
        private int _toolsCompletedThisSession = 0;
        private readonly HashSet<string> _toolsUsed = new();

        public int ToolsCompleted => _toolsCompletedThisSession;
        public bool ShouldShowUpsell => _toolsCompletedThisSession >= 3;

        public event Action? OnUpsellTriggered;

        public void RecordToolCompletion(string toolName)
        {
            _toolsCompletedThisSession++;
            _toolsUsed.Add(toolName);

            if (_toolsCompletedThisSession == 3)
                OnUpsellTriggered?.Invoke();
        }

        public void Reset() => _toolsCompletedThisSession = 0;
    }
}