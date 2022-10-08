using ClrDiagnostics.Helpers;

using DiagnosticInvestigations;

using DiagnosticServer.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DiagnosticServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProcessesController : ControllerBase
    {
        private readonly ILogger<ProcessesController> _logger;
        private readonly DebuggingSessionService _debuggingSessionService;

        public ProcessesController(ILogger<ProcessesController> logger,
            DebuggingSessionService debuggingSessionService)
        {
            _logger = logger;
            _debuggingSessionService = debuggingSessionService;
        }

        /// <summary>
        /// Returns a list of all the dotnet runninng processes
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public Task<IActionResult> Get()
        {
            var processes = ProcessHelper.GetDotnetProcesses()
                .Select(p => new { Id = p.Id, Name = p.ProcessName });

            return Task.FromResult<IActionResult>(Ok(processes));
        }

        //[Route("api/[controller]/{id}")]
        [HttpPost("attach/{id}")]
        public Task<IActionResult> AttachEvents(int id)
        {
            _debuggingSessionService.SubscribeTriggers(id);
            return Task.FromResult<IActionResult>(Ok());
        }

        [HttpPost("detach")]
        public Task<IActionResult> DetachEvents()
        {
            _debuggingSessionService.UnsubscribeTriggers();
            return Task.FromResult<IActionResult>(Ok());
        }

        [HttpPost("snapshot/{id}")]
        public Task<IActionResult> Snapshot(int id)
        {
            var sessionId = _debuggingSessionService.Snapshot(id);
            return Task.FromResult<IActionResult>(Ok(sessionId));
        }

        [HttpPost("dump/{id}")]
        public Task<IActionResult> Dump(int id)
        {
            var sessionId = _debuggingSessionService.Dump(id);
            return Task.FromResult<IActionResult>(Ok(sessionId));
        }

    }
}
