using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ClrDiagnostics;

using DiagnosticInvestigations.Configurations;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiagnosticInvestigations
{
    public class InvestigationState
    {
        private readonly ILogger<InvestigationState> _logger;
        private readonly GeneralConfiguration _generalConfiguration;

        private ConcurrentDictionary<Guid, InvestigationScope> _scopes = new();
        private int _clientRefCount = 0;
        private DateTime? _orphaned = null;

        public InvestigationState(
            ILogger<InvestigationState> logger,
            IOptions<GeneralConfiguration> generalConfigurationOption)
        {
            _logger = logger;
            _generalConfiguration = generalConfigurationOption.Value;
        }

        public IList<InvestigationScope> GetActiveSessions()
        {
            return _scopes.Values.ToList();
        }

        public InvestigationScope? GetInvestigationScope(Guid sessionId)
        {
            if(!_scopes.TryGetValue(sessionId, out var scope)) { return null; }
            return scope;
        }

        public Guid AddSnapshot(DiagnosticAnalyzer analyzer)
        {
            Guid session = Guid.NewGuid();
            InvestigationScope scope = new(session, InvestigationKind.Snapshot, analyzer);
            _scopes[session] = scope;
            return session;
        }

        public Guid AddDump(DiagnosticAnalyzer analyzer)
        {
            Guid session = Guid.NewGuid();
            InvestigationScope scope = new(session, InvestigationKind.Dump, analyzer);
            _scopes[session] = scope;
            return session;
        }

        public int ClientRefCount
        {
            get
            {
                lock (this) { return _clientRefCount; }
            }
        }

        public DateTime? Orphaned
        {
            get
            {
                lock (this) { return _orphaned; } 
            }
        }

        public void MarkClientConnection()
        {
            lock (this)
            {
                _clientRefCount++;
                _orphaned = null;
            }
        }

        public void MarkClientDisconnection()
        {
            lock (this)
            {
                _clientRefCount--;
                if (_clientRefCount == 0)
                {
                    _orphaned = DateTime.Now;
                }
                else
                {
                    _orphaned = null;
                }
            }
        }

        /// <summary>
        /// This is intended to be called periodically from the background service.
        /// After the given timeout all the opened debugging sessions will be cleared out.
        /// We avoid clearing the debugging session synchronously on the disconnection of
        /// the SignalR connection because there may be connectivity issues.
        /// Instead, we give time to the client(s) to reconnect and gain control of the 
        /// debugging sessions again.
        /// </summary>
        public void ClearSessionIfExpired()
        {
            var expiryTime = TimeSpan.FromMinutes(_generalConfiguration.DebuggingSessionsExpirationMinutes);
            var orphaned = Orphaned;
            if(orphaned.HasValue && DateTime.Now - orphaned.Value > expiryTime)
            {
                lock(this)
                {
                    foreach(var scope in _scopes.Values)
                    {
                        scope.DiagnosticAnalyzer.Dispose();
                        if(scope.TemporaryFile != null)
                        {
                            try
                            {
                                File.Delete(scope.TemporaryFile.FullName);
                            }
                            catch (Exception err)
                            {
                                _logger.LogWarning($"The temporary file {scope.TemporaryFile.FullName} could not be deleted: {err.Message}");
                            }
                        }
                    }

                    _scopes.Clear();
                    _logger.LogInformation($"Debugging sessions has been cleared out");
                }
            }
        }


    }
}
