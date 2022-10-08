using Microsoft.AspNetCore.SignalR;

namespace DiagnosticServer.Hubs;

public class DiagnosticHub : Hub
{
    public override Task OnConnectedAsync()
    {
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        return base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(string user, string message)
    {
        var usr = Clients.User(user);
        await Clients.All.SendAsync("onMessage", user, "received: " + message);
    }
}
