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

        [HttpGet("queries")]
        public Task<IActionResult> GetQueries()
        {
            return Task.FromResult<IActionResult>(Ok(_queriesService.Queries.Keys));
        }

        [HttpPost("query/{sessionId}/{query}")]
        public async Task<IActionResult> RunQuery(string sessionId, string query)
        {
            if(!_queriesService.Queries.TryGetValue(query, out var knownQuery))
            {
                return NotFound();
            }

            if (!Guid.TryParse(sessionId, out Guid id))
            {
                return NotFound();
            }

            var scope = _debuggingSessionService.GetInvestigationScope(id);
            if(scope == null)
            {
                return NotFound();
            }

            // We offload the execution to the background service thread,
            // including the TaskCompletionSource that is awaited here
            // This is the synchronous code that we do NOT want to do here
            //   var analyzer = scope.DiagnosticAnalyzer;
            //   var result = knownQuery.Populate(analyzer);

            System.Collections.IEnumerable result;
            _logger.LogInformation($"Offloaded the execution of the query {knownQuery.Name}");
            try
            {
                result = await _debuggingSessionService.ExecuteAsync(scope, knownQuery);
                _logger.LogInformation($"Query {knownQuery.Name} has completed");
            }
            catch(Exception err)
            {
                _logger.LogError(err, $"Query {knownQuery.Name} has faulted");
                throw;
            }

            return Ok(result);
        }


    }
}
