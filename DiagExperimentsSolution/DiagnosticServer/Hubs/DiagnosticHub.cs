using DiagnosticInvestigations;

using Microsoft.AspNetCore.SignalR;

namespace DiagnosticServer.Hubs;

public class DiagnosticHub : Hub
{
    private readonly ILogger<DiagnosticHub> _logger;
    private readonly InvestigationState _investigationState;

    public DiagnosticHub(
        ILogger<DiagnosticHub> logger,
        InvestigationState investigationState)
    {
        _logger = logger;
        _investigationState = investigationState;
    }

    public override Task OnConnectedAsync()
    {
        _investigationState.MarkClientConnection();
        _logger.LogInformation($"New client connected: current refcount is {_investigationState.ClientRefCount}");
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _investigationState.MarkClientDisconnection();
        _logger.LogInformation($"Client disconnected: current refcount is {_investigationState.ClientRefCount}");
        return base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(string user, string message)
    {
        var usr = Clients.User(user);
        await Clients.All.SendAsync("onMessage", user, "received: " + message);
    }
}
