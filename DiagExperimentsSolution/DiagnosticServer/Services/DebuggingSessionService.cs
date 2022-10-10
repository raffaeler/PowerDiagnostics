using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

using ClrDiagnostics;
using ClrDiagnostics.Triggers;

using CustomEventSource;

using DiagnosticInvestigations;

using DiagnosticModels;
using DiagnosticModels.Converters;

using DiagnosticServer.Hubs;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Diagnostics.Tracing.AutomatedAnalysis;
using Microsoft.Extensions.Hosting;

namespace DiagnosticServer.Services
{
    public class DebuggingSessionService : BackgroundService
    {
        private readonly ILogger<DebuggingSessionService> _logger;
        private readonly IHubContext<DiagnosticHub> _diagnosticHubContext;
        private readonly InvestigationState _investigationState;
        private readonly JsonSerializerOptions _jsonOptions;

        private AutoResetEvent _quit = new(false);
        private AutoResetEvent _go = new(false);
        private Thread _worker;
        private TimeSpan _loopTimeout = TimeSpan.FromSeconds(15);

        private ConcurrentQueue<(InvestigationScope scope, KnownQuery query, TaskCompletionSource<IEnumerable>)> _executionQuery = new();

        private TriggerAll? _triggerAll;

        public DebuggingSessionService(
            ILogger<DebuggingSessionService> logger,
            IHostApplicationLifetime applicationLifetime,
            IHubContext<DiagnosticHub> diagnosticHubContext,
            InvestigationState investigationState)
        {
            _logger = logger;
            applicationLifetime.ApplicationStopping.Register(() => _quit.Set());
            _diagnosticHubContext = diagnosticHubContext;
            _investigationState = investigationState;
            _jsonOptions = SetupConverters.CreateOptions();
            _worker = new(Worker);
            _worker.IsBackground = true;
            _worker.Priority = ThreadPriority.BelowNormal;
            _worker.Start();
        }

        public override void Dispose()
        {
            UnsubscribeTriggers();
            base.Dispose();
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"Service Started");
            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Service Stopped");
            return base.StopAsync(cancellationToken);
        }

        private async void Worker()
        {
            _logger.LogInformation($"Worker Started");
            WaitHandle[] handles = new[] { _quit, _go };
            (InvestigationScope scope, KnownQuery query, TaskCompletionSource<IEnumerable> tcs) trio = default;
            while (true)
            {
                //await Task.Delay(1000);
                var wait = WaitHandle.WaitAny(handles, _loopTimeout);
                if (wait == 0)
                {
                    _logger.LogInformation($"Quitting worker thread");
                    return;
                }

                if (wait == 1)
                {
                    while (_executionQuery.TryDequeue(out trio))
                    {
                        Debug.WriteLine($"Worker thread> processing query {trio.query.Name}");

                        var analyzer = trio.scope.DiagnosticAnalyzer;
                        var knownQuery = trio.query;
                        var tcs = trio.tcs;
                        var result = knownQuery.Populate(analyzer);
                        tcs.SetResult(result);
                    }

                    continue;
                }

                await _diagnosticHubContext.Clients.All.SendAsync("onMessage", "userX", "Test " + DateTime.Now);
                _investigationState.ClearSessionIfExpired();
            }
        }

        public Task<IEnumerable> ExecuteAsync(InvestigationScope scope, KnownQuery query)
        {
            var tcs = new TaskCompletionSource<IEnumerable>();
            _executionQuery.Enqueue((scope, query, tcs));
            _go.Set();
            return tcs.Task;
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

        public Guid Snapshot(int pid)
        {
            var analyzer = DiagnosticAnalyzer.FromSnapshot(pid);
            var sessionId = _investigationState.AddSnapshot(analyzer);
            return sessionId;
        }

        public Guid Dump(int pid)
        {
            var analyzer = DiagnosticAnalyzer.FromDump(pid);
            var sessionId = _investigationState.AddDump(analyzer);
            return sessionId;
        }

        public IList<InvestigationScope> GetActiveSessions()
            => _investigationState.GetActiveSessions();

        public InvestigationScope? GetInvestigationScope(Guid sessionId)
            => _investigationState.GetInvestigationScope(sessionId);
    }
}
