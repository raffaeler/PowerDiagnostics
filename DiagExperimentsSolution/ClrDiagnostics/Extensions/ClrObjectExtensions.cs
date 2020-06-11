using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using Microsoft.Diagnostics.Runtime;

namespace ClrDiagnostics.Extensions
{
    public static class ClrObjectExtensions
    {
        public static string PrintAddressAndType(this ClrObject clrObject,
            string prefix) => $"{prefix}{clrObject.Address:X16} {clrObject.Type}";

        public static string PrintAddressTypeAndSize(this ClrObject clrObject,
            string prefix) => $"{prefix}{clrObject.Address:X16} {clrObject.Type} Size:{clrObject.Size}";

        public static string GetStringValue(this ClrObject clrObject, int maxLength = int.MaxValue)
        {
            return clrObject.Type.IsString ? clrObject.AsString(maxLength) : null;
        }

        public static UInt64 GetGraphSize(this ClrObject clrObject)
        {
            ulong size = 0;
            var visited = new HashSet<UInt64>();
            Accumulate(clrObject, visited, ref size);

            static void Accumulate(ClrObject clrObject, HashSet<UInt64> visited, ref ulong size)
            {
                if (clrObject.IsNull) return;
                if (visited.Contains(clrObject.Address)) return;

                size += clrObject.Size;
                visited.Add(clrObject.Address);
                foreach (var childReference in clrObject.EnumerateReferencesWithFields())
                {
                    Accumulate(childReference.Object, visited, ref size);
                }
            }

            return size;
        }


    }
}
/*
        /// 
        /// dumpheap -type System.AppDomain
        /// dumpheap -mt 00007ff9fd295f60
        /// objsize 0000000003d71590
        /// sizeof(0000000003D71590) = 976 (0x3d0) bytes (System.AppDomain)
        /// 
        /// dumpheap -type System.Globalization.CultureInfo
        /// 00007ff9fd299b18        7          896 System.Globalization.CultureInfo
        /// dumpheap -mt 00007ff9fd299b18
        /// objsize 0000000003d82900

             //var examinedAppDomain = heap.EnumerateObjects()
            //    .First(o => o.Type.Name == "System.AppDomain");
            //var graphSizeAppDomain = examinedAppDomain.GetGraphSize();
            //Assert.Equal(976, (int)graphSizeAppDomain);            

            //var examinedCultureInfo = heap.EnumerateObjects()
            //    .First(o => o.Type.Name == "System.Globalization.CultureInfo");
            //var graphSizeCultureInfo = examinedCultureInfo.GetGraphSize();
            //Assert.Equal(5872, (int)graphSizeCultureInfo);            
 

 */
