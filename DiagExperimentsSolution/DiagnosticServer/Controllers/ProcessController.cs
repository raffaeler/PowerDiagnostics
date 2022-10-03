using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DiagnosticServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProcessController : ControllerBase
    {
        [HttpGet]
        public Task<IActionResult> Get()
        {
            return Task.FromResult<IActionResult>(Ok(new[] { "a", "b", "c" }));
        }
    }
}
