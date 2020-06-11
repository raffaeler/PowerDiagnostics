using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using TestWebApp.Models;
using TestWebApp.Services;

namespace TestWebApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : Controller
    {
        private readonly ILogger<TestController> _logger;
        private readonly SimpleStateService _simpleState;

        public TestController(ILogger<TestController> logger, SimpleStateService simpleState)
        {
            this._logger = logger;
            this._simpleState = simpleState;
        }

        [HttpGet]
        public string[] Get()
        {
            var sources = EventSource.GetSources()
                .Select(s => $"{s.Name} {s.IsEnabled()}").ToArray();
            return sources;
        }


        [HttpPost("SimplePost")]
        public IActionResult SimplePost([FromBody] string data)
        {
            _logger.LogInformation(nameof(SimplePost));
            return new OkResult();
        }

        [HttpPost("ExceptionOnPost")]
        public IActionResult ExceptionOnPost([FromBody] string data)
        {
            _logger.LogInformation(nameof(ExceptionOnPost));
            if (_simpleState.Next() % 250 == 0) throw new DemoException("Throwing a bad exception");
            return new OkResult();
        }

        [HttpPost("SlowPost")]
        public IActionResult SlowPost([FromBody] string data)
        {
            _logger.LogInformation(nameof(SlowPost));
            if (_simpleState.Next() % 4 == 0) Thread.Sleep(500);
            return new OkResult();
        }
    }
}
