using Microsoft.Diagnostics.Runtime;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClrDiagnostics.Extensions
{
    public static class ClrRuntimeExtensions
    {
        /// <summary>
        /// Note: https://github.com/microsoft/clrmd/issues/567#issuecomment-601314348
        /// </summary>
        /// <param name="runtime"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public static IEnumerable<ClrType> GetConstructedTypeDefinitions(this ClrRuntime runtime,
            Func<ClrType, bool> predicate)
        {
            return runtime.EnumerateModules()
                .SelectMany(m => m.EnumerateTypeDefToMethodTableMap())
                .Select(t => runtime.GetTypeByMethodTable(t.MethodTable))
                .Where(predicate);
        }


    }
}
