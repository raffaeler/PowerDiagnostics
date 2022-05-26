using System;
using System.Collections.Generic;
using System.Runtime.Loader;

using Microsoft.Extensions.Logging;

using TestWebAddonContract;

using TestWebApp.Helpers;

namespace TestWebApp.Services
{
    public class AddonService
    {
        private readonly ILogger<Worker> _logger;

        private AssemblyLoadContext _myLoadContext;
        private WeakReference _isLoadContextAlive;
        private ILeakyAddon _addon;

        private static Dictionary<string, byte[]> _leakyDictionary = new();

        public AddonService(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        public void LoadContext()
        {
            _myLoadContext = new AssemblyLoadContext("MyLoadContext", true);
            _isLoadContextAlive = new WeakReference(_myLoadContext, true);

            var asmName = new System.Reflection.AssemblyName("TestWebAddon");   // see xcopy in post-build action
            var file = System.IO.Path.Combine(GetCurrentExecutablePath(), "TestWebAddon");
            var asm = _myLoadContext.LoadFromAssemblyPath(file);
            var type = asm.GetType("TestWebAddon.LeakyAddon");
            _addon = (ILeakyAddon)Activator.CreateInstance(type);
            _myLoadContext.Unloading += OnMyLoadContextUnloading;

            ContextHelpers.PrintAllContexts("Contexts");
        }

        public void UnloadContext()
        {
            _myLoadContext.Unload();
            _myLoadContext.Unloading -= OnMyLoadContextUnloading;
            _myLoadContext = null;
            // _isLoadContextAlive.IsAlive tells whether the load context is still alive
        }

        public void MakeAddonWork()
        {
            //_addon.LeakSomeMemory(20);
            _leakyDictionary[DateTime.Now.Ticks.ToString()] = _addon.AllocateSomeMemory(1234);
        }


        private void OnMyLoadContextUnloading(AssemblyLoadContext obj)
        {
            _logger.LogInformation("Secondary context unloaded");
        }

        private string GetCurrentExecutablePath()
        {
            return new System.IO.FileInfo(typeof(Startup).Assembly.Location).DirectoryName;
        }
    }
}
