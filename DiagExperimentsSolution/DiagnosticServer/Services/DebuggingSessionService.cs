using DiagnosticServer.Hubs;

using Microsoft.AspNetCore.SignalR;

namespace DiagnosticServer.Services
{
    public class DebuggingSessionService : BackgroundService
    {
        private readonly ILogger<DebuggingSessionService> _logger;
        private readonly IHubContext<DiagnosticHub> _diagnosticHubContext;

        public DebuggingSessionService(ILogger<DebuggingSessionService> logger,
            IHubContext<DiagnosticHub> diagnosticHubContext)
        {
            _logger = logger;
            _diagnosticHubContext = diagnosticHubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"Service Started");
            while(true)
            {
                await Task.Delay(1000);
                await _diagnosticHubContext.Clients.All.SendAsync("onMessage", "userX", "Test " + DateTime.Now);
            }
        }
    }
}
