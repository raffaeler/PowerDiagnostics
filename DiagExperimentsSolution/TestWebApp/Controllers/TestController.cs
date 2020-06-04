using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using TestWebApp.Services;

namespace TestWebApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : Controller
    {
        private readonly SimpleState _simpleState;

        public TestController(SimpleState simpleState)
        {
            this._simpleState = simpleState;
        }

        [HttpGet]
        public string Get() => "Test controller";

        [HttpPost("PostTest")]
        public IActionResult PostTest([FromBody] string data)
        {
            return new OkResult();
        }

        [HttpPost("PostFailTest")]
        public IActionResult PostFailTest([FromBody] string data)
        {
            if (_simpleState.Next() % 250 == 0) throw new Exception("Crashing");
            return new OkResult();
        }

        [HttpPost("PostLongTest")]
        public IActionResult PostLongTest([FromBody] string data)
        {
            if (_simpleState.Next() % 4 == 0) Thread.Sleep(500);
            return new OkResult();
        }
    }
}
