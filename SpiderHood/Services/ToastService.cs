// Services/ToastService.cs
using SpiderHood.Models;

namespace SpiderHood.Services
{
    public class ToastService
    {
        public event Action<ToastMessage>? OnShow;

        public void ShowSuccess(string message, string title = "Éxito")
        {
            OnShow?.Invoke(new ToastMessage
            {
                Title = title,
                Message = message,
                Type = ToastType.Success,
                Duration = 3000
            });
        }

        public void ShowError(string message, string title = "Error")
        {
            OnShow?.Invoke(new ToastMessage
            {
                Title = title,
                Message = message,
                Type = ToastType.Error,
                Duration = 5000
            });
        }

        public void ShowWarning(string message, string title = "Advertencia")
        {
            OnShow?.Invoke(new ToastMessage
            {
                Title = title,
                Message = message,
                Type = ToastType.Warning,
                Duration = 4000
            });
        }

        public void ShowInfo(string message, string title = "Información")
        {
            OnShow?.Invoke(new ToastMessage
            {
                Title = title,
                Message = message,
                Type = ToastType.Info,
                Duration = 3000
            });
        }
    }
}