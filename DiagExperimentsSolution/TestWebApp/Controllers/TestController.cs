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
        private readonly CpuStressService _cpuStressService;
        private readonly MemoryPressureService _memoryPressureService;

        public TestController(ILogger<TestController> logger,
            SimpleStateService simpleState,
            CpuStressService cpuStressService,
            MemoryPressureService memoryPressureService)
        {
            this._logger = logger;
            this._simpleState = simpleState;
            this._cpuStressService = cpuStressService;
            this._memoryPressureService = memoryPressureService;
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

        [HttpPost("LeakBlob")]
        public IActionResult LeakBlobPost([FromBody] string data)
        {
            _logger.LogInformation(nameof(LeakBlobPost));
            _memoryPressureService.AllocateArray();
            return new OkResult();
        }

        [HttpPost("LeakGraph")]
        public IActionResult LeakGraphPost([FromBody] string data)
        {
            _logger.LogInformation(nameof(LeakGraphPost));
            _memoryPressureService.AllocateGraphRoots();
            return new OkResult();
        }

        [HttpPost("FreeLeaks")]
        public IActionResult FreeLeaksPost([FromBody] string data)
        {
            _logger.LogInformation(nameof(FreeLeaksPost));
            _memoryPressureService.FreeAll();
            return new OkResult();
        }


        [HttpPost("GcCollect")]
        public IActionResult GcCollectPost([FromBody] string data)
        {
            _logger.LogInformation(nameof(GcCollectPost));
            _memoryPressureService.GCCollect();
            return new OkResult();
        }


        [HttpPost("CpuStress")]
        public IActionResult CpuStressPost([FromBody] string data)
        {
            _logger.LogInformation(nameof(CpuStressPost));
            _cpuStressService.CpuLoad(250_000);
            return new OkResult();
        }


    }
}
