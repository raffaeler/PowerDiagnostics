using System.Text.Json;
using System.Text.Json.Serialization;

using ClrDiagnostics.Triggers;

using CustomEventSource;

using DiagnosticModels;
using DiagnosticModels.Converters;

using DiagnosticServer.Hubs;

using Microsoft.AspNetCore.SignalR;

namespace DiagnosticServer.Services
{
    public class DebuggingSessionService : BackgroundService
    {
        private readonly ILogger<DebuggingSessionService> _logger;
        private readonly IHubContext<DiagnosticHub> _diagnosticHubContext;
        private readonly JsonSerializerOptions _jsonOptions;

        private TriggerAll? _triggerAll;

        public DebuggingSessionService(ILogger<DebuggingSessionService> logger,
            IHubContext<DiagnosticHub> diagnosticHubContext)
        {
            _logger = logger;
            _diagnosticHubContext = diagnosticHubContext;
            _jsonOptions = SetupConverters.CreateOptions();
        }

        public override void Dispose()
        {
            UnsubscribeTriggers();
            base.Dispose();
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

        private void SendTrigger(EvsBase evs)
        {
            // TODO: Avoid serializing when there are not clients connected
            // This requires to monitor connect/disconnect and take our own count
            var evsJson = JsonSerializer.Serialize(evs, _jsonOptions);
            _diagnosticHubContext.Clients.All.SendAsync("onEvs", evsJson);
        }

        public void SubscribeTriggers(int pid)
        {
            UnsubscribeTriggers();
            _triggerAll = new TriggerAll(pid,
                Constants.CustomHeaderEventSourceName,
                Constants.TriggerHeaderCounterName);

            _triggerAll.OnCpu = d => SendTrigger(new EvsCpu(d));
            _triggerAll.OnEventCounterCount = d => SendTrigger(new EvsCustomHeader(d));
            _triggerAll.OnException = d => SendTrigger(new EvsException(d));
            _triggerAll.OnGcAllocation = d => SendTrigger(new EvsGcAllocation(d));
            _triggerAll.OnHttpRequests = d => SendTrigger(new EvsHttpRequests(d));
            _triggerAll.OnWorkingSet = d => SendTrigger(new EvsWorkingSet(d));

            _triggerAll.Start();
        }

        public void UnsubscribeTriggers()
        {
            if (_triggerAll != null)
            {
                _triggerAll.Dispose();
                _triggerAll = null;
            }
        }

    }
}
