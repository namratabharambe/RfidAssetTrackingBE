using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class AssetTrackingHub : Hub
    {
        public async Task SendLiveScan(object scanEvent)
        {
            await Clients.All.SendAsync("ReceiveLiveScan", scanEvent);
        }

        public async Task SendReaderStatus(string readerName, string status)
        {
            await Clients.All.SendAsync("ReceiveReaderStatus", readerName, status);
        }

        public async Task SendDashboardUpdate(object stats)
        {
            await Clients.All.SendAsync("ReceiveDashboardUpdate", stats);
        }

        public async Task SendAlertNotification(object alert)
        {
            await Clients.All.SendAsync("ReceiveAlertNotification", alert);
        }
    }
}
