using ClrDiagnostics.Helpers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DiagnosticServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProcessesController : ControllerBase
    {
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
    }
}
