using AnprDashboardShared;
public class ToastService
{
    public enum ToastLevel { Info, Success, Warning, Error }

    public event Action? OnChange;
    public event Func<ToastModel, Task>? OnShow;

    private List<ToastModel> toasts = new();

    public async Task ShowToast(string message, ToastLevel level)
    {
        var toast = new ToastModel
        {
            Message = message,
            Level = level,
            Duration = 5000
        };

        toasts.Add(toast);

        if (OnShow != null)
            await OnShow(toast);

        NotifyStateChanged();
        _ = RemoveAfterDelay(toast);
    }
    public async Task ShowToastAsync(string message, ToastLevel level, int duration = 3000)
    {
        Console.WriteLine("✅ ShowToastAsync");
        var toast = new ToastModel
        {
            Message = message,
            Level = level,
            Duration = duration
        };

        if (OnShow != null)
            await OnShow.Invoke(toast);
    }


    private async Task RemoveAfterDelay(ToastModel toast)
    {
        await Task.Delay(toast.Duration);
        Remove(toast);
    }

    public void Remove(ToastModel toast)
    {
        toasts.Remove(toast);
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}

public class ToastModel
{
    public string Message { get; set; } = string.Empty;
    public ToastService.ToastLevel Level { get; set; } = ToastService.ToastLevel.Info;
    public int Duration { get; set; } = 5000;
}
