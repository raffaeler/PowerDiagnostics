using ClrDiagnostics.Helpers;

using DiagnosticInvestigations;

using DiagnosticServer.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DiagnosticServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SessionsController : ControllerBase
    {
        private readonly ILogger<SessionsController> _logger;
        private readonly DebuggingSessionService _debuggingSessionService;
        private readonly QueriesService _queriesService;

        public SessionsController(ILogger<SessionsController> logger,
            DebuggingSessionService debuggingSessionService,
            QueriesService queriesService)
        {
            _logger = logger;
            _debuggingSessionService = debuggingSessionService;
            _queriesService = queriesService;
        }

        /// <summary>
        /// Returns a list of all the dotnet runninng processes
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public Task<IActionResult> Get()
        {
            var sessions = _debuggingSessionService.GetActiveSessions()
                .Select(d => new
                {
                    d.SessionId,
                    d.InvestigationKind,
                    d.Created,
                });

            return Task.FromResult<IActionResult>(Ok(sessions));
        }


        [HttpPost("Query/{sessionId}/{query}")]
        public Task<IActionResult> MakeQuery(string sessionId, string query)
        {
            if(!_queriesService.Queries.TryGetValue(query, out var knownQuery))
            {
                return Task.FromResult<IActionResult>(NotFound());
            }

            if (!Guid.TryParse(sessionId, out Guid id))
            {
                return Task.FromResult<IActionResult>(NotFound());
            }

            var scope = _debuggingSessionService.GetInvestigationScope(id);
            if(scope == null)
            {
                return Task.FromResult<IActionResult>(NotFound());
            }

            var analyzer = scope.DiagnosticAnalyzer;

            // TODO: create "command" for the background thread,
            // including the TaskCompletionSource that will be
            // awaited here
            // command content: scope, query

            var result = knownQuery.Populate(analyzer);
            
            return Task.FromResult<IActionResult>(Ok(result));
        }


    }
}
