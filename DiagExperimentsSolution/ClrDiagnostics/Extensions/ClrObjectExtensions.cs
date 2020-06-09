using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using Microsoft.Diagnostics.Runtime;

namespace ClrDiagnostics.Extensions
{
    public static class ClrObjectExtensions
    {
        public static string PrintAddressAndType(this ClrObject @object,
            string prefix) => $"{prefix}{@object.Address:X16} {@object.Type}";

        public static string PrintAddressTypeAndSize(this ClrObject @object,
            string prefix) => $"{prefix}{@object.Address:X16} {@object.Type} Size:{@object.Size}";

        public static string GetStringValue(this ClrObject @object, int maxLength = int.MaxValue)
        {
            if (@object.Type.ElementType != ClrElementType.String) return null;
            return @object.Type.ClrObjectHelpers.ReadString(@object.Address, maxLength);
        }

        public static UInt64 GetGraphSize(this ClrObject @object)
        {
            ulong size = 0;
            var visited = new HashSet<UInt64>();
            Accumulate(@object, visited, ref size);

            static void Accumulate(ClrObject @object, HashSet<UInt64> visited, ref ulong size)
            {
                if (@object.IsNull) return;
                if (visited.Contains(@object.Address)) return;

                size += @object.Size;
                visited.Add(@object.Address);
                foreach (var childReference in @object.EnumerateReferencesWithFields())
                {
                    Accumulate(childReference.Object, visited, ref size);
                }
            }

            return size;
        }


    }
}
