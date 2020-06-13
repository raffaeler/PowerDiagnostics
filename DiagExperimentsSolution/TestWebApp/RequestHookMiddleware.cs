using CustomEventSource;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TestWebApp
{
    public class RequestHookMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestHookMiddleware> _logger;
        private readonly CustomHeaderEventSource _eventSource;

        public RequestHookMiddleware(RequestDelegate next, 
            ILogger<RequestHookMiddleware> logger,
            CustomHeaderEventSource eventSource)
        {
            this._next = next;
            this._logger = logger;
            this._eventSource = eventSource;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            bool eventSourceIsEnabled = CustomHeaderEventSource.Instance.IsEnabled();
            //if (!eventSourceIsEnabled)
            //{
            //    _logger.LogWarning($"{nameof(CustomHeaderEventSource)} is not enabled");
            //}

            if (eventSourceIsEnabled &&
                httpContext.Request.Headers.ContainsKey(Constants.TriggerHeaderName))
            {
                _eventSource.RaiseTriggerHeaderCounter();
            }

            await _next(httpContext);
        }
    }

    public static class RequesteHookMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestHook(this IApplicationBuilder applicationBuilder)
        {
            return applicationBuilder.UseMiddleware<RequestHookMiddleware>();
        }
    }
}
