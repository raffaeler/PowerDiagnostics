using CustomEventSource;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TestWebApp
{
    public class RequestHookMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly CustomHeaderEventSource _eventSource;

        public RequestHookMiddleware(RequestDelegate next, CustomHeaderEventSource eventSource)
        {
            this._next = next;
            this._eventSource = eventSource;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            if (httpContext.Request.Headers.ContainsKey(Constants.TriggerHeaderName))
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
