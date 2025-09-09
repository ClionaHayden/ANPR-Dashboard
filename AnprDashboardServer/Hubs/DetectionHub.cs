using Microsoft.AspNetCore.SignalR;

namespace AnprDashboardServer.Hubs
{
    public class DetectionHub : Hub
    {
        public async Task BroadcastDetection(DetectionRecord record)
        {
            await Clients.All.SendAsync("ReceiveDetection", record);
        }
    }
}
