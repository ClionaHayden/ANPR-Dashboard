using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace AnprDashboardWasm.Services;

public class DetectionHubClient : IAsyncDisposable
{
    private readonly HubConnection hubConnection;

    public event Action<DetectionRecord>? OnDetection;

    public DetectionHubClient(NavigationManager nav)
    {
        hubConnection = new HubConnectionBuilder()
            .WithUrl($"http://localhost:5131/detectionHub")
            .WithAutomaticReconnect()
            .Build();

        hubConnection.On<DetectionRecord>("ReceiveDetection", (record) =>
        {
            OnDetection?.Invoke(record);
        });
    }

    public async Task StartAsync()
    {
        if (hubConnection.State == HubConnectionState.Disconnected)
        {
            await hubConnection.StartAsync();
            Console.WriteLine("✅ Global hub connection started");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (hubConnection is not null)
            await hubConnection.DisposeAsync();
    }
}

public class DetectionRecord
{
    public int Id { get; set; }
    public string Plate { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string FilePath { get; set; } = string.Empty;
}
