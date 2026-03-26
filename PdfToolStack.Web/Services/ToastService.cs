namespace PdfToolStack.Web.Services
{
    public class ToastService
    {
        public event Action<ToastMessage>? OnToast;

        public void ShowSuccess(string message) =>
            OnToast?.Invoke(new ToastMessage(
                message, ToastType.Success));

        public void ShowError(string message) =>
            OnToast?.Invoke(new ToastMessage(
                message, ToastType.Error));

        public void ShowWarning(string message) =>
            OnToast?.Invoke(new ToastMessage(
                message, ToastType.Warning));

        public void ShowInfo(string message) =>
            OnToast?.Invoke(new ToastMessage(
                message, ToastType.Info));
    }

    public class ToastMessage
    {
        public string Message { get; }
        public ToastType Type { get; }
        public string Id { get; } =
            Guid.NewGuid().ToString();

        public ToastMessage(string message, ToastType type)
        {
            Message = message;
            Type = type;
        }
    }

    public enum ToastType
    {
        Success, Error, Warning, Info
    }
}