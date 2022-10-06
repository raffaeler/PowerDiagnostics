using ClrDiagnostics.Helpers;

using DiagnosticServer.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DiagnosticServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProcessesController : ControllerBase
    {
        private readonly DebuggingSessionService _debuggingSessionService;

        public ProcessesController(DebuggingSessionService debuggingSessionService)
        {
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
        [HttpPost("Attach/{id}")]
        public Task<IActionResult> AttachEvents(int id)
        {
            _debuggingSessionService.SubscribeTriggers(id);
            return Task.FromResult<IActionResult>(Ok());
        }

        [HttpPost("Detach")]
        public Task<IActionResult> DetachEvents()
        {
            _debuggingSessionService.UnsubscribeTriggers();
            return Task.FromResult<IActionResult>(Ok());
        }

    }
}
